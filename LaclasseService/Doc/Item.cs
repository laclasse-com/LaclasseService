using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Laclasse.Authentication;
using Erasme.Json;

namespace Laclasse.Doc
{
    public class Context
    {
        public string tempDir;
        public string directoryDbUrl;
        public AuthenticatedUser user;
        public DB db;
        public Blobs blobs;
        public Dictionary<long, Item> items = new Dictionary<long, Item>();

        public async Task<Item> GetByIdAsync(long id)
        {
            if (items.ContainsKey(id))
                return items[id];
            var node = new Node { id = id };
            if (!await node.LoadAsync(db, true))
                node = null;
            if (node != null)
            {
                var item = Item.ByNode(this, node);
                items[id] = item;
                return item;
            }
            return null;
        }

        public Item GetByNode(Node node)
        {
            var item = Item.ByNode(this, node);
            items[node.id] = item;
            return item;
        }
    }

    public enum ItemRight
    {
        Owner,
        Reader,
        Writer
    }

    public class Item
    {
        public readonly Node node;
        protected Context context;

        public Item(Context context, Node node)
        {
            this.context = context;
            this.node = node;
        }

        public async Task<Item> GetParentAsync()
        {
            return (node.parent_id != null) ? await context.GetByIdAsync((long)node.parent_id) : null;
        }

        public async Task<List<Item>> GetParentsAsync()
        {
            List<Item> parents = new List<Item>();
            Item current = this;
            while (true)
            {
                Item parent = await current.GetParentAsync();
                if (parent == null)
                    return parents;
                current = parent;
                parents.Insert(0, parent);
            }
        }

        public async Task<Item> GetRootAsync()
        {
            Item current = this;
            while (true)
            {
                Item parent = await current.GetParentAsync();
                if (parent == null)
                    return current;
                current = parent;
            }
        }

        public virtual Task<ItemRight> RightsAsync()
        {
            return Task.FromResult(ItemRight.Owner);
        }

        public virtual void ProcessAdvancedRights()
        {

        }

        public string SanitizeName()
        {
            return "";
        }

        public void Create()
        {
        }

        public virtual Task ChangeAsync()
        {
            return Task.FromResult(0);
        }

        // Delete an Item
        public virtual async Task DeleteAsync()
        {
            await node.DeleteAsync(context.db);
            context.items.Remove(node.id);
            if (node.blob_id != null)
                await context.blobs.DeleteBlobAsync(context.db, node.blob_id);
        }

        public virtual async Task<JsonObject> ToJsonAsync(bool expand)
        {
            var json = node.ToJson();
            json["right"] = (await RightsAsync()).ToString();
            return json;
        }

        public static Item ByNode(Context context, Node node)
        {
            if (types.ContainsKey(node.mime))
                return types[node.mime](context, node);
            return types["*"](context, node);
        }

        public static async Task<Item> Create(Context context, FileDefinition<Node> fileDefinition)
        {
            Docs.GenerateDefaultContent(fileDefinition);
            Blob blob; string tempFile;
            (blob, tempFile) = await context.blobs.PrepareBlobAsync(fileDefinition);

            Node node = fileDefinition.Define;
            if (node == null)
                node = new Node();
            if (node.name == null)
                node.name = blob.name;
            if (node.mime == null)
                node.mime = blob.mimetype;
            node.size = blob.size;
            node.rev = 0;
            node.mtime = (long)(DateTime.Now - DateTime.Parse("1970-01-01T00:00:00Z")).TotalSeconds;

            if (context.user.IsUser)
            {
                node.owner = context.user.user.id;
                node.owner_firstname = context.user.user.firstname;
                node.owner_lastname = context.user.user.lastname;
            }

            string thumbnailTempFile = null;
            Blob thumbnailBlob = null;

            if (tempFile != null)
                Docs.BuildThumbnail(context.tempDir, tempFile, node.mime, out thumbnailTempFile, out thumbnailBlob);

            if (tempFile != null)
            {
                blob = await context.blobs.CreateBlobFromTempFileAsync(context.db, blob, tempFile);
                node.blob_id = blob.id;
            }
            if (thumbnailTempFile != null)
            {
                thumbnailBlob.parent_id = blob.id;
                thumbnailBlob = await context.blobs.CreateBlobFromTempFileAsync(context.db, thumbnailBlob, thumbnailTempFile);
                node.has_tmb = true;
            }
            await node.SaveAsync(context.db, true);
            return ByNode(context, node);
        }

        static Dictionary<string, Func<Context, Node, Item>> types = new Dictionary<string, Func<Context, Node, Item>>();

        public static void Register(string mimetype, Func<Context, Node, Item> creator)
        {
            types[mimetype] = creator;
        }

        public static void Register(IEnumerable<string> mimetypes, Func<Context, Node, Item> creator)
        {
            foreach (var mimetype in mimetypes)
                types[mimetype] = creator;
        }

        static Item()
        {
            Register("classes", (context, node) => new Classes(context, node));
            Register("classe", (context, node) => new Classe(context, node));
            Register("groupes", (context, node) => new Groupes(context, node));
            Register("groupe", (context, node) => new Groupe(context, node));
            Register("ct", (context, node) => new CahierTexte(context, node));
            Register("etablissement", (context, node) => new Structure(context, node));
            Register("cartable", (context, node) => new Cartable(context, node));
            Register("directory", (context, node) => new Folder(context, node));
            Register("text/uri-list", (context, node) => new WebLink(context, node));
            Register("*", (context, node) => new Document(context, node));
        }
    }

    public class Document : Item
    {
        public Document(Context context, Node node) : base(context, node)
        {
        }
    }

    public class WebLink : Document
    {
        public WebLink(Context context, Node node) : base(context, node)
        {
        }

        public override async Task<JsonObject> ToJsonAsync(bool expand)
        {
            var json = await base.ToJsonAsync(expand);
            json["DANIEL"] = "WEBLINK";
            return json;
        }
    }

    public class Folder : Item
    {
        public Folder(Context context, Node node) : base(context, node)
        {
        }

        public override async Task DeleteAsync()
        {
            // delete children first
            var children = await GetChildrenAsync();
            foreach (var child in children)
                await child.DeleteAsync();
            await base.DeleteAsync();
        }

        public virtual async Task<IEnumerable<Item>> GetChildrenAsync()
        {
            // load node children if needed
            if (!node.Fields.ContainsKey(nameof(node.children)))
                await node.LoadExpandFieldAsync(context.db, nameof(node.children));
            var children = new List<Item>();
            return node.children.Select((child) => ByNode(context, child));
        }

        public override async Task<JsonObject> ToJsonAsync(bool expand)
        {
            var json = await base.ToJsonAsync(expand);
            JsonArray children = new JsonArray();
            json["children"] = children;
            foreach (var item in await GetChildrenAsync())
                children.Add(await item.ToJsonAsync(false));
            return json;
        }
    }

    public class Cartable : Folder
    {
        public Cartable(Context context, Node node) : base(context, node)
        {
        }

        public override async Task<JsonObject> ToJsonAsync(bool expand)
        {
            var json = await base.ToJsonAsync(expand);
            json["name"] = "Mon Cartable";
            return json;
        }
    }

    public class Structure : Folder
    {
        Directory.Structure _structure = null;

        public Structure(Context context, Node node) : base(context, node)
        {
        }

        public async Task<Directory.Structure> GetStructureAsync()
        {
            if (_structure != null)
                return _structure;
            using (var db = await DB.CreateAsync(context.directoryDbUrl))
            {
                _structure = new Directory.Structure { id = node.etablissement_uai };
                if (!await _structure.LoadAsync(db))
                    _structure = null;
            }
            return _structure;
        }

        public override async Task<JsonObject> ToJsonAsync(bool expand)
        {
            var json = await base.ToJsonAsync(expand);
            var structure = await GetStructureAsync();
            if (structure != null)
                json["name"] = structure.name;
            return json;
        }
    }

    public class Classes : Folder
    {
        public Classes(Context context, Node node) : base(context, node)
        {
        }

        public override async Task<IEnumerable<Item>> GetChildrenAsync()
        {
            var children = await base.GetChildrenAsync();
            var root = await GetRootAsync();
            if (root is Structure)
            {
                var rootStructure = root as Structure;
                var structure = await rootStructure.GetStructureAsync();
                if (structure != null)
                {
                    // load structure's groups if needed
                    if (!structure.Fields.ContainsKey(nameof(structure.groups)))
                    {
                        using (var db = await DB.CreateAsync(context.directoryDbUrl))
                        {
                            await structure.LoadExpandFieldAsync(db, nameof(structure.groups));
                        }
                        // check if all classes exists and create missing ones
                        var newGroups = structure.groups.FindAll((group) => group.type == Directory.GroupType.CLS && !children.Any((child) => child.node.name == group.name));
                        foreach (var group in newGroups)
                        {
                            var fileDefinition = new FileDefinition<Node>
                            {
                                Define = new Node
                                {
                                    mime = "classe",
                                    parent_id = node.id,
                                    name = group.name
                                }
                            };
                            var groupItem = await Item.Create(context, fileDefinition);
                            context.items[groupItem.node.id] = groupItem;
                            children.Append(groupItem);
                        }
                    }
                }
            }
            return children;
        }
    }

    public class Classe : Folder
    {
        public Classe(Context context, Node node) : base(context, node)
        {
        }

        public override async Task<IEnumerable<Item>> GetChildrenAsync()
        {
            var children = await base.GetChildrenAsync();
            if (!children.Any((child) => child.node.mime == "ct"))
            {
                var fileDefinition = new FileDefinition<Node>
                {
                    Define = new Node
                    {
                        mime = "ct",
                        parent_id = node.id,
                        name = "Cahier de textes.ct"
                    }
                };
                var ctItem = await Item.Create(context, fileDefinition);
                context.items[ctItem.node.id] = ctItem;
                children.Append(ctItem);
            }
            return children;
        }
    }

    public class Groupes : Folder
    {
        public Groupes(Context context, Node node) : base(context, node)
        {
        }
    }

    public class Groupe : Folder
    {
        public Groupe(Context context, Node node) : base(context, node)
        {
        }
    }

    public class CahierTexte : Folder
    {
        public CahierTexte(Context context, Node node) : base(context, node)
        {
        }
    }
}
