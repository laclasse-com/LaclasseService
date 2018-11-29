using System;
using System.IO;
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

    public struct ItemRight
    {
        public bool Read;
        public bool Write;
        public bool Locked;
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

        public virtual async Task<ItemRight> RightsAsync()
        {
            ItemRight rights = new ItemRight { Read = true, Write = true, Locked = false };
            if (!context.user.IsSuperAdmin)
            {
                // rights are herited from the parent is any
                var parent = await GetParentAsync();
                if (parent != null)
                {
                    rights = await parent.RightsAsync();
                    rights.Locked = false;
                }
                rights = await ProcessAdvancedRightsAsync(rights);
            }
            Console.WriteLine($"{this}.RightsAsync() {node.name}, read: {rights.Read}");
            return rights;
        }

        public virtual async Task<ItemRight> ProcessAdvancedRightsAsync(ItemRight rights)
        {
            // handle advanced rights        
            // advanced right only supported for structure and free groups
            var root = await GetRootAsync();

            if (!(root is Structure || root is GroupeLibre))
                return rights;

            IEnumerable<Directory.UserProfile> profiles = new List<Directory.UserProfile>();
            if (root is Structure)
                profiles = context.user.user.profiles.Where((p) => p.structure_id == root.node.etablissement_uai);
            else if (root is GroupeLibre)
                profiles = context.user.user.profiles;

            bool? advRead = null;
            bool? advWrite = null;

            var profilesString = "";
            profiles.ForEach((p) => profilesString += p.type + ",");
            Console.WriteLine($"{this}.ProcessAdvancedRightsAsync user.profiles: {profilesString}");

            if (node.rights != null && node.rights.Count > 0)
            {
                foreach (var advRight in node.rights)
                {
                    Console.WriteLine($"{this}.ProcessAdvancedRightsAsync {advRight.profile} advRight: {advRight.read}");
                    if (profiles.Any((p) => StringToRightProfile(p.type) == advRight.profile))
                    {
                        if (advRead != true || advRead == null)
                            advRead = advRight.read;
                        if (advWrite != true || advWrite == null)
                            advWrite = advRight.write;
                    }
                }
            }

            if (advRead != null)
                rights.Read = (bool)advRead;
            if (advWrite != null)
                rights.Write = (bool)advWrite;

            // handle special rights for the owner
            if (node.owner != null && node.owner == context.user.user.id)
            {
                rights.Read = true;
                rights.Write = true;
            }
            Console.WriteLine($"{this}.ProcessAdvancedRightsAsync advRead: {advRead}");
            //r[:advanced_rights] = true
            //r[:advanced_admin] = profiles.any ? { | p | p['type'] != 'ELV' && p['type'] != 'TUT' }
            return rights;
        }

        public string SanitizeName()
        {
            return "";
        }

        public virtual Task ChangeAsync()
        {
            return Task.FromResult(0);
        }

        // Delete an Item
        public virtual async Task DeleteAsync()
        {
            var rights = await RightsAsync();
            if (!rights.Write || rights.Locked)
                throw new WebException(403, "Rights needed");
            await node.DeleteAsync(context.db);
            context.items.Remove(node.id);
            if (node.blob_id != null)
                await context.blobs.DeleteBlobAsync(context.db, node.blob_id);
        }

        public virtual async Task<JsonObject> ToJsonAsync(bool expand)
        {
            var json = node.ToJson();
            var rights = await RightsAsync();
            json["read"] = rights.Read;
            json["write"] = rights.Write;
            json["locked"] = rights.Locked;
            return json;
        }

        public static Item ByNode(Context context, Node node)
        {
            if (types.ContainsKey(node.mime))
                return types[node.mime](context, node);
            return types["*"](context, node);
        }

        public static async Task<Item> CreateAsync(Context context, FileDefinition<Node> fileDefinition)
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

        public static RightProfile StringToRightProfile(string profile)
        {
            if (profile == "TUT")
                return RightProfile.TUT;
            if (profile == "ELV")
                return RightProfile.ELV;
            return RightProfile.TUT;
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
            Register("groupe_libre", (context, node) => new GroupeLibre(context, node));
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
            if (node.blob_id != null)
            {
                using (var stream = context.blobs.GetBlobStream(node.blob_id))
                using (var streamReader = new StreamReader(stream))
                {
                    json["url"] = await streamReader.ReadLineAsync();
                }
            }
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
            // get all children by their id because the node need to by
            // fully loaded with the node's expanded field to have a valid Item
            foreach (var child in node.children)
                children.Add(await context.GetByIdAsync(child.id));
            return children;
        }

        public virtual Task<IEnumerable<Item>> GetFilteredChildrenAsync()
        {
            return GetChildrenAsync();
        }

        public override async Task<JsonObject> ToJsonAsync(bool expand)
        {
            Console.WriteLine($"Folder.ToJsonAsync {node.name}");
            var json = await base.ToJsonAsync(expand);
            json.Remove("children");
            if (expand)
            {
                var rights = await RightsAsync();
                Console.WriteLine($"Folder.ToJsonAsync {node.name} Read: {rights.Read}");
                JsonArray children = new JsonArray();
                json["children"] = children;
                if (rights.Read)
                {
                    foreach (var item in await GetFilteredChildrenAsync())
                        children.Add(await item.ToJsonAsync(false));
                }
            }
            return json;
        }
    }

    public class Cartable : Folder
    {
        public Cartable(Context context, Node node) : base(context, node)
        {
        }

        public override Task<ItemRight> RightsAsync()
        {
            var rights = new ItemRight { Read = false, Write = false, Locked = true };
            if (context.user.IsSuperAdmin)
                rights = new ItemRight { Read = true, Write = true, Locked = false };
            else if (context.user.IsUser && context.user.user.id == node.cartable_uid)
                rights = new ItemRight { Read = true, Write = true, Locked = true };
            return Task.FromResult(rights);
        }

        public override async Task<JsonObject> ToJsonAsync(bool expand)
        {
            var json = await base.ToJsonAsync(expand);
            json["name"] = "Mon Cartable";
            return json;
        }

        public static async Task<Cartable> CreateAsync(Context context, string userId)
        {
            return await Item.CreateAsync(context, new FileDefinition<Node>
            {
                Define = new Node
                {
                    mime = "cartable",
                    parent_id = null,
                    name = userId,
                    cartable_uid = userId
                }
            }) as Cartable;
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

        public override Task<ItemRight> RightsAsync()
        {
            var rights = new ItemRight { Read = false, Write = false, Locked = true };
            if (context.user.IsSuperAdmin)
                rights = new ItemRight { Read = true, Write = true, Locked = false };
            else if (context.user.IsUser)
            {
                if (context.user.user.profiles.Any((p) => p.structure_id == node.etablissement_uai))
                    rights.Read = true;
                if (context.user.user.profiles.Any((p) => p.structure_id == node.etablissement_uai && p.type != "TUT" && p.type != "ELV"))
                    rights.Write = true;
            }
            return Task.FromResult(rights);
        }

        public override async Task<JsonObject> ToJsonAsync(bool expand)
        {
            var json = await base.ToJsonAsync(expand);
            var structure = await GetStructureAsync();
            if (structure != null)
                json["name"] = structure.name;
            return json;
        }

        public override async Task<IEnumerable<Item>> GetChildrenAsync()
        {
            var children = await base.GetChildrenAsync();
            // ensure the "classes" folder exists
            if (!children.Any((c) => c is Classes))
            {
                var fileDefinition = new FileDefinition<Node>
                {
                    Define = new Node
                    {
                        mime = "classes",
                        parent_id = node.id,
                        name = "classes"
                    }
                };
                var classesItem = await Item.CreateAsync(context, fileDefinition);
                context.items[classesItem.node.id] = classesItem;
                children.Append(classesItem);
            }
            // ensure the "groupes" folder exists
            if (!children.Any((c) => c is Groupes))
            {
                var fileDefinition = new FileDefinition<Node>
                {
                    Define = new Node
                    {
                        mime = "groupes",
                        parent_id = node.id,
                        name = "groupes"
                    }
                };
                var groupesItem = await Item.CreateAsync(context, fileDefinition);
                context.items[groupesItem.node.id] = groupesItem;
                children.Append(groupesItem);
            }
            return children;
        }


        public static async Task<Structure> CreateAsync(Context context, string structureId)
        {
            return await Item.CreateAsync(context, new FileDefinition<Node>
            {
                Define = new Node
                {
                    mime = "etablissement",
                    parent_id = null,
                    name = structureId,
                    etablissement_uai = structureId
                }
            }) as Structure;
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
                                    name = group.name,
                                    classe_id = group.id
                                }
                            };
                            var groupItem = await Item.CreateAsync(context, fileDefinition);
                            context.items[groupItem.node.id] = groupItem;
                            children.Append(groupItem);
                        }
                    }
                }
            }
            return children;
        }

        public override async Task<IEnumerable<Item>> GetFilteredChildrenAsync()
        {
            var children = await GetChildrenAsync();
            var root = await GetRootAsync();
            if (root is Structure && !context.user.IsSuperAdmin)
            {
                var profiles = context.user.user.profiles.FindAll((p) => p.structure_id == root.node.etablissement_uai).Select((p) => p.type);
                // admin see every thing
                if (!profiles.Contains("DIR") && !profiles.Contains("ADM"))
                {
                    var structGroups = (await ((Structure)root).GetStructureAsync()).groups.Where((g) => g.type == Directory.GroupType.CLS);

                    // users that are not TUT, ELV or ENS sees all existing groups
                    if (profiles.Any((p) => p != "TUT" && p != "ELV" && p != "ENS"))
                        children = children.Where((child) => structGroups.Any((g) => g.name == child.node.name));
                    // TUT, ELV and ENS only sees their groups
                    else
                    {
                        var userGroups = structGroups.Where((g) => context.user.user.groups.Any((ug) => ug.group_id == g.id) || context.user.user.children_groups.Any((ug) => ug.group_id == g.id));
                        children = children.Where((child) => userGroups.Any((g) => g.name == child.node.name));
                    }
                }

            }
            return children;
        }

        public override async Task<ItemRight> RightsAsync()
        {
            var rights = await base.RightsAsync();
            if (context.user.IsSuperAdmin)
                rights = new ItemRight { Read = true, Write = true, Locked = false };
            else
                rights.Locked = true;
            return rights;
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
                var ctItem = await Item.CreateAsync(context, fileDefinition);
                context.items[ctItem.node.id] = ctItem;
                children.Append(ctItem);
            }
            return children;
        }

        public override async Task<ItemRight> RightsAsync()
        {
            var rights = await base.RightsAsync();
            if (context.user.IsSuperAdmin)
                rights = new ItemRight { Read = true, Write = true, Locked = false };
            else
            {
                rights.Locked = true;
                var root = await GetRootAsync();
                if (root is Structure)
                {
                    // only students, teachers and management staff are allowed to write
                    var allowedProfiles = new string[] { "ENS", "ELV", "DOC", "DIR", "ADM" };
                    if (context.user.user.profiles.Any((p) => p.structure_id == root.node.etablissement_uai && allowedProfiles.Contains(p.type)))
                        rights.Write = true;
                }
                rights = await ProcessAdvancedRightsAsync(rights);
            }
            Console.WriteLine($"Classe.RightsAsync Read: {rights.Read}");
            return rights;
        }
    }

    public class Groupes : Folder
    {
        public Groupes(Context context, Node node) : base(context, node)
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
                        var newGroups = structure.groups.FindAll((group) => group.type == Directory.GroupType.GRP && !children.Any((child) => child.node.name == group.name));
                        foreach (var group in newGroups)
                        {
                            var fileDefinition = new FileDefinition<Node>
                            {
                                Define = new Node
                                {
                                    mime = "groupe",
                                    parent_id = node.id,
                                    name = group.name,
                                    groupe_id = group.id
                                }
                            };
                            var groupItem = await Item.CreateAsync(context, fileDefinition);
                            context.items[groupItem.node.id] = groupItem;
                            children.Append(groupItem);
                        }
                    }
                }
            }
            return children;
        }

        public override async Task<IEnumerable<Item>> GetFilteredChildrenAsync()
        {
            var children = await GetChildrenAsync();
            var root = await GetRootAsync();
            if (root is Structure && !context.user.IsSuperAdmin)
            {
                var profiles = context.user.user.profiles.FindAll((p) => p.structure_id == root.node.etablissement_uai).Select((p) => p.type);
                // admin see every thing
                if (!profiles.Contains("DIR") && !profiles.Contains("ADM"))
                {
                    var structGroups = (await ((Structure)root).GetStructureAsync()).groups.Where((g) => g.type == Directory.GroupType.GRP);

                    // users that are not TUT, ELV or ENS sees all existing groups
                    if (profiles.Any((p) => p != "TUT" && p != "ELV" && p != "ENS"))
                        children = children.Where((child) => structGroups.Any((g) => g.name == child.node.name));
                    // TUT, ELV and ENS only sees their groups
                    else
                    {
                        var userGroups = structGroups.Where((g) => context.user.user.groups.Any((ug) => ug.group_id == g.id) || context.user.user.children_groups.Any((ug) => ug.group_id == g.id));
                        children = children.Where((child) => userGroups.Any((g) => g.name == child.node.name));
                    }
                }

            }
            return children;
        }

        public override async Task<ItemRight> RightsAsync()
        {
            var rights = await base.RightsAsync();
            if (context.user.IsSuperAdmin)
                rights = new ItemRight { Read = true, Write = true, Locked = false };
            else
                rights.Locked = true;
            return rights;
        }
    }

    public class Groupe : Folder
    {
        public Groupe(Context context, Node node) : base(context, node)
        {
        }

        public override async Task<ItemRight> RightsAsync()
        {
            var rights = await base.RightsAsync();
            if (context.user.IsSuperAdmin)
                rights = new ItemRight { Read = true, Write = true, Locked = false };
            else
            {
                rights.Locked = true;
                var root = await GetRootAsync();
                if (root is Structure)
                {
                    // only students, teachers and management staff are allowed to write
                    var allowedProfiles = new string[] { "ENS", "ELV", "DOC", "DIR", "ADM" };
                    if (context.user.user.profiles.Any((p) => p.structure_id == root.node.etablissement_uai && allowedProfiles.Contains(p.type)))
                        rights.Write = true;
                }
            }
            return rights;
        }
    }

    public class CahierTexte : Folder
    {
        public CahierTexte(Context context, Node node) : base(context, node)
        {
        }

        public override async Task<ItemRight> RightsAsync()
        {
            var rights = await base.RightsAsync();
            if (context.user.IsSuperAdmin)
                rights = new ItemRight { Read = true, Write = true, Locked = false };
            else
            {
                rights.Locked = true;
                var root = await GetRootAsync();
                if (root is Structure)
                {
                    rights.Read = false;
                    // a profile on the "etablissement" = read right
                    if (context.user.user.profiles.Any((p) => p.structure_id == root.node.etablissement_uai))
                        rights.Read = true;
                    rights.Write = false;
                    // students and parents are not allowed to write
                    var allowedProfiles = new string[] { "ENS", "DOC", "DIR", "ADM" };
                    if (context.user.user.profiles.Any((p) => p.structure_id == root.node.etablissement_uai && allowedProfiles.Contains(p.type)))
                        rights.Write = true;
                }
            }
            return rights;
        }
    }

    public class GroupeLibre : Folder
    {
        public GroupeLibre(Context context, Node node) : base(context, node)
        {
        }

        public override Task<ItemRight> RightsAsync()
        {
            ItemRight rights;
            if (context.user.IsSuperAdmin)
                rights = new ItemRight { Read = true, Write = true, Locked = false };
            else
                rights = new ItemRight { Read = true, Locked = true, Write = context.user.user.groups.Any((g) => node.groupe_libre_id == g.group_id) };
            return Task.FromResult(rights);
        }

        public static async Task<GroupeLibre> CreateAsync(Context context, int groupId, string groupName)
        {
            return await Item.CreateAsync(context, new FileDefinition<Node>
            {
                Define = new Node
                {
                    mime = "groupe_libre",
                    parent_id = null,
                    name = groupName,
                    groupe_libre_id = groupId
                }
            }) as GroupeLibre;
        }

        public static async Task<GroupeLibre> GetOrCreateAsync(Context context, int groupId, string groupName)
        {
            var groupsNodes = await context.db.SelectAsync<Node>("SELECT * FROM `node` WHERE `groupe_libre_id`= ?", groupId);
            if (groupsNodes.Count > 0)
                return Item.ByNode(context, groupsNodes[0]) as GroupeLibre;
            return await CreateAsync(context, groupId, groupName);
        }
    }

}
