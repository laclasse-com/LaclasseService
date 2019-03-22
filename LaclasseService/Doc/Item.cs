using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Erasme.Json;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Doc
{
    public class Context
    {
        public DocSetup setup;
        public string storageDir;
        public string tempDir;
        public string directoryDbUrl;
        public AuthenticatedUser user;
        public DB db;
        public Blobs blobs;
        public Docs docs;
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

    public class ItemRight
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
                await ProcessAdvancedRightsAsync(rights);
                await ProcessAdvancedParentRightsAsync(rights);
            }
            return rights;
        }

        public virtual async Task ProcessAdvancedRightsAsync(ItemRight rights)
        {
            // handle advanced rights        
            // advanced right only supported for structure and free groups
            var root = await GetRootAsync();

            IEnumerable<Directory.UserProfile> profiles = new List<Directory.UserProfile>();
            if (root is Structure)
                profiles = context.user.user.profiles.Where((p) => p.structure_id == root.node.etablissement_uai);
            else
                profiles = context.user.user.profiles;

            bool? advRead = null;
            bool? advWrite = null;

            if (node.rights != null && node.rights.Count > 0)
            {
                foreach (var advRight in node.rights)
                {
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
        }

        public virtual async Task ProcessAdvancedParentRightsAsync(ItemRight rights)
        {
            var parent = await GetParentAsync();
            if (parent != null)
                await parent.ProcessAdvancedParentRightsAsync(rights);
        }

        public virtual Task<Stream> GetContentAsync()
        {
            Stream stream = null;
            if (node.content != null)
            {
                var fullPath = Path.GetFullPath(ContentToPath(node.content));
                // check if full path is in the base directory
                if (!fullPath.StartsWith(context.storageDir, StringComparison.InvariantCulture))
                    throw new WebException(403, "Invalid file path");
                if (!File.Exists(fullPath))
                    throw new WebException(404, "Content not found");
                stream = File.OpenRead(fullPath);
            }
            else if (node.blob_id != null)
                stream = context.blobs.GetBlobStream(node.blob_id);
            else
                stream = new MemoryStream();
            return Task.FromResult(stream);
        }

        /// <summary>
        /// Generates the BLOB async. TEMPORARY. ONLY NEED FOR MIGRATION PURPOSE
        /// </summary>
        /// <returns>The BLOB async.</returns>
        public async virtual Task GenerateBlobAsync()
        {
            // migrate node without content
            if (this is Folder || node.size == 0)
            {
                await (new Node { id = node.id, content = null }).UpdateAsync(context.db);
            }
            else
            {
                var stream = await GetContentAsync();
                if (stream == null)
                    return;
                var fileDefinition = new FileDefinition<Node>
                {
                    Stream = stream,
                    Mimetype = node.mime,
                    Name = node.name,
                    Size = node.size
                };
                Blob blob = null; string tempFile = null;
                (blob, tempFile) = await context.blobs.PrepareBlobAsync(fileDefinition);
                if (blob != null)
                {
                    // merge blob if possible
                    var sameBlob = await context.blobs.SearchSameBlobAsync(context.db, blob);
                    if (sameBlob != null)
                    {
                        blob = sameBlob;
                        node.blob_id = sameBlob.id;
                        if (sameBlob.children.Any(b => b.name == "thumbnail"))
                            node.has_tmb = true;
                        // delete the temporary file if commit is done
                        context.db.AddCommitAction(() =>
                        {
                            File.Delete(tempFile);
                        });
                    }
                    else
                    {
                        blob = await context.blobs.CreateBlobFromTempFileAsync(context.db, blob, tempFile);
                        node.blob_id = blob.id;
                    }
                    node.content = null;
                }
                await node.UpdateAsync(context.db);
                await node.LoadAsync(context.db, true);
            }
        }

        public async virtual Task ChangeAsync(FileDefinition<Node> fileDefinition)
        {
            if (context.user.IsUser)
            {
                if (!(await RightsAsync()).Write)
                    throw new WebException(403, "User dont have write right");
            }

            Blob blob = null; string tempFile = null;
            if (fileDefinition.Stream != null)
                (blob, tempFile) = await context.blobs.PrepareBlobAsync(fileDefinition);

            string thumbnailTempFile = null;
            Blob thumbnailBlob = null;

            if (tempFile != null)
                context.docs.BuildThumbnail(context.tempDir, tempFile, blob.mimetype, out thumbnailTempFile, out thumbnailBlob);

            string oldBlobId = null;
            if (blob != null)
            {
                oldBlobId = node.blob_id;
                // merge blob if possible
                var sameBlob = await context.blobs.SearchSameBlobAsync(context.db, blob);
                if (sameBlob != null)
                {
                    blob = sameBlob;
                    node.blob_id = sameBlob.id;
                    // delete the temporary file if commit is done
                    context.db.AddCommitAction(() =>
                    {
                        File.Delete(tempFile);
                    });
                }
                else
                {
                    blob = await context.blobs.CreateBlobFromTempFileAsync(context.db, blob, tempFile);
                    node.blob_id = blob.id;
                }
                if (oldBlobId == node.blob_id)
                    oldBlobId = null;

                node.size = blob.size;
                node.rev++;
                if (context.user.IsUser)
                {
                    // update the last owner
                    node.owner = context.user.user.id;
                    node.owner_firstname = context.user.user.firstname;
                    node.owner_lastname = context.user.user.lastname;
                    node.mtime = (long)(DateTime.Now - DateTime.Parse("1970-01-01T00:00:00Z")).TotalSeconds;
                }
            }

            var oldParent = node.parent_id != null ? await context.GetByIdAsync((long)node.parent_id) : null;
            var parent = oldParent;

            if (fileDefinition.Define != null)
            {
                var define = fileDefinition.Define;
                if (define.IsSet(nameof(Node.name)))
                    node.name = define.name;
                if (define.IsSet(nameof(Node.parent_id)))
                {
                    // check destination rights
                    if (context.user.IsUser)
                    {
                        if ((define.parent_id == null || node.parent_id == null) && !context.user.IsSuperAdmin)
                            throw new WebException(403, "User dont have write to modify root nodes");
                        if (!(await oldParent.RightsAsync()).Write)
                            throw new WebException(403, "User dont have write right on the source folder");
                    }
                    if (define.parent_id != null)
                        parent = await context.GetByIdAsync((long)define.parent_id);
                    if (context.user.IsUser)
                    {
                        if (!(await parent.RightsAsync()).Write)
                            throw new WebException(403, "User dont have write right on the destination");
                    }
                    // check if destination folder is not the node or its children
                    if (parent != null)
                    {
                        if (parent.node.id == node.id)
                            throw new WebException(400, "Cant move a node inside itself");
                        var parents = await parent.GetParentsAsync();
                        if (parents.Any((p) => p.node.id == node.id))
                            throw new WebException(400, "Cant move a node inside subfolders");
                    }
                    node.parent_id = define.parent_id;
                }
                if (define.IsSet(nameof(Node.rights)))
                {
                    node.rights = define.rights;
                }
                if (define.IsSet(nameof(Node.return_date)) && node.mime == "rendu")
                {
                    // restricted user can change this
                    if (context.user.IsRestrictedUser)
                        throw new WebException(403, "Insufficient rights");
                    node.return_date = define.return_date;
                }
            }

            if (thumbnailTempFile != null)
            {
                thumbnailBlob.parent_id = blob.id;
                thumbnailBlob = await context.blobs.CreateBlobFromTempFileAsync(context.db, thumbnailBlob, thumbnailTempFile);
                node.has_tmb = true;
            }

            if (oldParent != null)
            {
                if (oldParent != parent)
                {
                    await oldParent.OnBeforeChildChangedAsync(this, ChildAction.MoveOut, fileDefinition);
                    await parent.OnBeforeChildChangedAsync(this, ChildAction.MoveIn, fileDefinition);
                }
                else
                    await parent.OnBeforeChildChangedAsync(this, ChildAction.Update, fileDefinition);
            }

            await node.UpdateAsync(context.db);
            await node.LoadAsync(context.db, true);

            // delete old blob if any
            if (oldBlobId != null)
            {
                // delete the blob if this is the last ref file
                var blobRefCount = await context.db.ExecuteScalarAsync("SELECT COUNT(id) FROM `node` WHERE `blob_id` = ?", oldBlobId);
                if ((long)blobRefCount == 0)
                    await context.blobs.DeleteBlobAsync(context.db, oldBlobId);
            }

            if (oldParent != null)
            {
                if (oldParent != parent)
                {
                    await oldParent.OnChildChangedAsync(this, ChildAction.MoveOut);
                    await parent.OnChildChangedAsync(this, ChildAction.MoveIn);
                }
                else
                    await parent.OnChildChangedAsync(this, ChildAction.Update);
            }
        }

        // Delete an Item
        public virtual async Task DeleteAsync()
        {
            var rights = await RightsAsync();
            if (!rights.Write || rights.Locked)
                throw new WebException(403, "Rights needed");
            Item parent = null;
            if (node.parent_id != null)
                parent = await context.GetByIdAsync((long)node.parent_id);
            // check parent rights (write right need on the parent)
            if (context.user.IsUser && !context.user.IsSuperAdmin)
            {
                if (parent == null)
                    throw new WebException(403, "Rights needed");
                if (!(await parent.RightsAsync()).Write)
                    throw new WebException(403, "Rights needed");
            }
            if (parent != null)
                await parent.OnBeforeChildChangedAsync(this, ChildAction.Delete, null);
            await node.DeleteAsync(context.db);
            context.items.Remove(node.id);
            if (node.blob_id != null)
            {
                // delete the blob if this is the last ref file
                var blobRefCount = await context.db.ExecuteScalarAsync("SELECT COUNT(id) FROM `node` WHERE `blob_id` = ? AND `id` != ?", node.blob_id, node.id);
                if ((long)blobRefCount == 0)
                    await context.blobs.DeleteBlobAsync(context.db, node.blob_id);
            }
            if (parent != null)
                await parent.OnChildChangedAsync(this, ChildAction.Delete);
        }

        public async virtual Task<bool> GenerateThumbnailAsync()
        {
            var done = false;
            if (node.blob_id == null)
                return done;
            if (context.user.IsUser)
            {
                if (!(await RightsAsync()).Write)
                    throw new WebException(403, "User dont have write right");
            }
            var stream = await GetContentAsync();
            if (stream != null)
            {
                using (stream)
                {
                    var tempFile = Path.Combine(context.tempDir, Guid.NewGuid().ToString());
                    using (var tmpStream = File.OpenWrite(tempFile))
                        await stream.CopyToAsync(tmpStream);
                    try
                    {
                        string thumbnailTempFile = null;
                        Blob thumbnailBlob = null;
                        context.docs.BuildThumbnail(context.tempDir, tempFile, node.mime, out thumbnailTempFile, out thumbnailBlob);

                        if (thumbnailTempFile != null)
                        {
                            thumbnailBlob.parent_id = node.blob_id;
                            thumbnailBlob = await context.blobs.CreateBlobFromTempFileAsync(context.db, thumbnailBlob, thumbnailTempFile);
                            node.has_tmb = true;
                            await (new Node { id = node.id, has_tmb = true }).UpdateAsync(context.db);
                            done = true;
                        }
                    }
                    finally
                    {
                        File.Delete(tempFile);
                    }
                }
            }
            return done;
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

        public virtual async Task CreateAsync(FileDefinition<Node> fileDefinition)
        {
            // ensure "name" dont have character '/'
            if (node.name.IndexOf('/') != -1)
                throw new WebException(400, "Character '/' not valid in name");

            var parent = node.parent_id != null ? await context.GetByIdAsync((long)node.parent_id) : null;
            if (parent != null)
                await parent.OnBeforeChildChangedAsync(this, ChildAction.Create, fileDefinition);
            await node.SaveAsync(context.db, true);
            if (parent != null)
                await parent.OnChildChangedAsync(this, ChildAction.Create);
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
            {
                var mime = FileNameToMimeType(node.name);
                if (mime == "application/octet-stream")
                    mime = blob.mimetype;
                node.mime = mime;
            }
            node.size = blob.size;
            node.rev = 0;
            node.mtime = (long)(DateTime.Now - DateTime.Parse("1970-01-01T00:00:00Z")).TotalSeconds;

            if (context.user.IsUser)
            {
                node.owner = context.user.user.id;
                node.owner_firstname = context.user.user.firstname;
                node.owner_lastname = context.user.user.lastname;

                // check right for parent_id
                if (!context.user.IsSuperAdmin)
                {
                    if (node.parent_id == null)
                        throw new WebException(403, "User cant create root nodes");

                    var parent = await context.GetByIdAsync((long)node.parent_id);
                    if (!(await parent.RightsAsync()).Write)
                        throw new WebException(403, "User dont have write right");
                }
            }

            if (tempFile != null)
            {
                var sameBlob = await context.blobs.SearchSameBlobAsync(context.db, blob);
                if (sameBlob != null)
                {
                    blob = sameBlob;
                    node.blob_id = sameBlob.id;
                    if (sameBlob.children.Any(b => b.name == "thumbnail"))
                        node.has_tmb = true;
                    // delete the temporary file if commit is done
                    context.db.AddCommitAction(() =>
                    {
                        File.Delete(tempFile);
                    });
                }
                else
                {
                    string thumbnailTempFile = null;
                    Blob thumbnailBlob = null;

                    if (tempFile != null)
                        context.docs.BuildThumbnail(context.tempDir, tempFile, node.mime, out thumbnailTempFile, out thumbnailBlob);

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
                }
            }

            var item = ByNode(context, node);
            await item.CreateAsync(fileDefinition);
            return item;
        }

        protected enum ChildAction
        {
            Create,
            Delete,
            MoveIn,
            MoveOut,
            Update
        }

        protected virtual Task OnBeforeChildChangedAsync(Item child, ChildAction action, FileDefinition<Node> fileDefinition)
        {
            Console.WriteLine($"Item.BeforeOnChildChanged {action.ToString()}");
            return Task.FromResult(true);
        }

        protected virtual Task OnChildChangedAsync(Item child, ChildAction action)
        {
            Console.WriteLine($"Item.OnChildChanged {action.ToString()}");
            return Task.FromResult(true);
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
            return RightProfile.ENS;
        }

        static readonly Dictionary<string, string> extensionToMimeTypes = new Dictionary<string, string>();

        public static void RegisterFileExtension(string extension, string mimetype)
        {
            extensionToMimeTypes[extension.ToLowerInvariant()] = mimetype;
        }

        public static string FileNameToMimeType(string shortName)
        {
            var extension = Path.GetExtension(shortName);
            if (extensionToMimeTypes.ContainsKey(extension))
                return extensionToMimeTypes[extension];
            var mimetype = FileContent.MimeType(shortName);
            return mimetype;
        }

        string ContentToPath(string content)
        {
            string basePath = context.storageDir;
            string filePath = null;
            var tab = content.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (tab[0] == "users")
            {
                var userId = tab[1].ToLowerInvariant();
                var userPath = $"{userId[0]}/{userId[1]}/{userId[2]}/{userId.Substring(3, 2)}/{userId.Substring(5)}";
                var remainPath = String.Join("/", tab, 2, tab.Length - 2);
                filePath = $"{basePath}users/{userPath}/{remainPath}";
            }
            else if (tab[0] == "etablissements")
            {
                var structureId = tab[1].ToLowerInvariant();
                var remainPath = String.Join("/", tab, 2, tab.Length - 2);
                filePath = $"{basePath}etablissements/{structureId}/{remainPath}";
            }
            else if (tab[0] == "groupes_libres")
            {
                var groupId = tab[1];
                var remainPath = String.Join("/", tab, 2, tab.Length - 2);
                filePath = $"{basePath}groupes_libres/{groupId}/{remainPath}";
            }
            return filePath;
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

            Register("rendu", (context, node) => new Rendu(context, node));

            Register("profs", (context, node) => new Profs(context, node));

            Register("directory", (context, node) => new Folder(context, node));

            RegisterFileExtension(".url", "text/uri-list");
            Register("text/uri-list", (context, node) => new WebLink(context, node));

            RegisterFileExtension(".pad", "application/x-laclasse-pad");
            Register("application/x-laclasse-pad", (context, node) => new Pad(context, node));

            RegisterFileExtension(".webapp", "application/x-laclasse-webapp");

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
            using (var stream = await GetContentAsync())
            using (var streamReader = new StreamReader(stream))
            {
                json["url"] = await streamReader.ReadLineAsync();
            }
            return json;
        }
    }

    public class Folder : Item
    {
        private bool _childrenLoaded = false;
        private readonly List<Item> _children = new List<Item>();

        public Folder(Context context, Node node) : base(context, node)
        {
        }

        public override async Task DeleteAsync()
        {
            // delete children first
            var children = new List<Item>(await GetFilteredChildrenAsync());
            foreach (var child in children)
            {
                await child.DeleteAsync();
                _children.Remove(child);
            }
            if (!_children.Any())
                await base.DeleteAsync();
        }

        public virtual async Task<IEnumerable<Item>> GetChildrenAsync()
        {
            if (!_childrenLoaded)
            {
                // load node children if needed
                if (!node.Fields.ContainsKey(nameof(node.children)))
                    await node.LoadExpandFieldAsync(context.db, nameof(node.children));
                // get all children by their id because the node need to by
                // fully loaded with the node's expanded field to have a valid Item
                foreach (var child in node.children)
                    _children.Add(await context.GetByIdAsync(child.id));
                _childrenLoaded = true;
            }
            return _children;
        }

        public virtual Task<IEnumerable<Item>> GetFilteredChildrenAsync()
        {
            return GetChildrenAsync();
        }

        public override async Task<JsonObject> ToJsonAsync(bool expand)
        {
            var json = await base.ToJsonAsync(expand);
            json.Remove("children");
            if (expand)
            {
                var rights = await RightsAsync();
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

        //
        // Return a new name for the given name. A name
        // which avoid conflict with an existing child
        // ex: test.txt => test 1.txt
        //
        protected async Task<string> ChildsConflictFreeNameAsync(string name, string renamePrefix = "")
        {
            var children = await GetChildrenAsync();

            var extension = Path.GetExtension(name);
            var prefix = name.Substring(0, name.Length - extension.Length);
            var lastIndex = 0;

            // test if the name is already a conflict free name
            var match = Regex.Match(name, $"^(.*){renamePrefix} ([0-9]+){extension}$");
            if (match.Success)
            {
                prefix = match.Groups[1].Value;
                lastIndex = int.Parse(match.Groups[2].Value);
            }

            var renameNeeded = false;
            // search for existing copies
            foreach (var child in children)
            {
                var childName = child.node.name;
                if (childName == name)
                    renameNeeded = true;
                var childMatch = Regex.Match(childName, $"^{prefix}{renamePrefix} ([0-9]+){extension}$");
                if (childMatch.Success)
                {
                    var index = int.Parse(childMatch.Groups[1].Value);
                    if (index > lastIndex)
                        lastIndex = index;
                }
            }
            return renameNeeded ? $"{prefix}{renamePrefix} {lastIndex + 1}{extension}" : name;
        }

        public static async Task<Folder> CreateAsync(Context context, string name, long parentId)
        {
            return await Item.CreateAsync(context, new FileDefinition<Node>
            {
                Define = new Node
                {
                    mime = "directory",
                    parent_id = parentId,
                    name = name,
                }
            }) as Folder;
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

        public override async Task ProcessAdvancedParentRightsAsync(ItemRight rights)
        {
            if (context.user.IsSuperAdmin)
            {
                rights.Read = true;
                rights.Write = true;
            }
            else
            {
                // if the Cartable is a root node and the current user is the owner
                // ensure read and write rights are set
                var parent = await GetParentAsync();
                if (parent == null)
                {
                    if (context.user.user.id == node.cartable_uid)
                    {
                        rights.Read = true;
                        rights.Write = true;
                    }
                }
                else
                    await parent.ProcessAdvancedParentRightsAsync(rights);
            }
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

        public override async Task ProcessAdvancedParentRightsAsync(ItemRight rights)
        {
            if (context.user.IsSuperAdmin)
            {
                rights.Read = true;
                rights.Write = true;
            }
            else
            {
                // if the sturcture is a root node and the current user has ADM or DIR profile
                // ensure read and write rights are set
                var parent = await GetParentAsync();
                if (parent == null)
                {
                    if (context.user.user.profiles.Any((p) => p.structure_id == node.etablissement_uai && (p.type == "ADM" || p.type == "DIR")))
                    {
                        rights.Read = true;
                        rights.Write = true;
                    }
                }
                else
                    await parent.ProcessAdvancedParentRightsAsync(rights);
            }
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
                await ProcessAdvancedRightsAsync(rights);
                await ProcessAdvancedParentRightsAsync(rights);
            }
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
                await ProcessAdvancedRightsAsync(rights);
                await ProcessAdvancedParentRightsAsync(rights);
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

    public class Pad : Folder
    {
        public Pad(Context context, Node node) : base(context, node)
        {
        }

        public async override Task CreateAsync(FileDefinition<Node> fileDefinition)
        {
            // parent create before because we need the node id first
            await base.CreateAsync(fileDefinition);

            var html = await (new StreamReader(await base.GetContentAsync())).ReadToEndAsync();

            // create the remote pad if possible
            var url = new Uri(context.setup.etherpad.url);
            using (var client = await HttpClient.CreateAsync(url))
            {
                var request = new HttpClientRequest
                {
                    Method = "GET",
                    Path = $"{url.AbsolutePath}/api/1.2.1/createPad"
                };
                request.QueryString["padID"] = node.id.ToString();
                request.QueryString["apikey"] = context.setup.etherpad.apiKey;
                await client.SendRequestAsync(request);
                var response = await client.GetResponseAsync();
            }
            // set the HTML content
            using (var client = await HttpClient.CreateAsync(url))
            {
                var request = new HttpClientRequest
                {
                    Method = "GET",
                    Path = $"{url.AbsolutePath}/api/1.2.1/setHTML"
                };
                request.QueryString["padID"] = node.id.ToString();
                request.QueryString["apikey"] = context.setup.etherpad.apiKey;
                request.QueryString["html"] = html;
                await client.SendRequestAsync(request);
                var response = await client.GetResponseAsync();
            }
        }

        public override async Task DeleteAsync()
        {
            var id = node.id;
            await base.DeleteAsync();
            context.db.AddCommitAction(async () =>
            {
                // delete the remote pad if possible
                var url = new Uri(context.setup.etherpad.url);
                using (HttpClient client = await HttpClient.CreateAsync(url))
                {
                    HttpClientRequest request = new HttpClientRequest
                    {
                        Method = "GET",
                        Path = $"{url.AbsolutePath}/api/1.2.1/deletePad"
                    };
                    request.QueryString["padID"] = id.ToString();
                    request.QueryString["apikey"] = context.setup.etherpad.apiKey;
                    await client.SendRequestAsync(request);
                    HttpClientResponse response = await client.GetResponseAsync();
                }
            });
        }

        public override async Task<Stream> GetContentAsync()
        {
            var stream = new MemoryStream();
            var url = new Uri(context.setup.etherpad.url);
            using (HttpClient client = await HttpClient.CreateAsync(url))
            {
                HttpClientRequest request = new HttpClientRequest
                {
                    Method = "GET",
                    Path = $"{url.AbsolutePath}/api/1.2.1/getHTML"
                };
                request.QueryString["padID"] = node.id.ToString();
                request.QueryString["apikey"] = context.setup.etherpad.apiKey;
                await client.SendRequestAsync(request);
                HttpClientResponse response = await client.GetResponseAsync();
                if (response.StatusCode == 200)
                {
                    var json = response.ReadAsJson();
                    if (json.ContainsKey("data") && json["data"].ContainsKey("html"))
                    {
                        var bytes = System.Text.Encoding.UTF8.GetBytes((json["data"] as JsonObject)["html"]);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                    stream.Seek(0, SeekOrigin.Begin);
                }
                else
                    throw new WebException(404, "Content not found");
            }
            return stream;
        }
    }

    public class Rendu : Folder
    {
        public Rendu(Context context, Node node) : base(context, node)
        {
        }

        public override async Task<IEnumerable<Item>> GetFilteredChildrenAsync()
        {
            var children = await GetChildrenAsync();
            if (context.user.IsSuperAdmin || await HasSeeAllRightAsync())
                return children;
            // the owner of the file and its parents are allowed to see
            return children.Where(
                child => child.node.owner == null ||
                child.node.owner == context.user.user.id ||
                context.user.user.children.Any(c => c.child_id == child.node.owner));
        }

        // Return true if the given user can view all files
        async Task<bool> HasSeeAllRightAsync()
        {
            var allowedProfiles = new string[] { "ACA", "ETA", "EVS", "ENS", "DOC", "DIR", "ADM" };
            var root = await GetRootAsync();
            if (root is Structure)
                return context.user.user.profiles.Where(p => p.structure_id == ((Structure)root).node.etablissement_uai).Any(p => allowedProfiles.Contains(p.type));
            else if (root is GroupeLibre)
                return context.user.user.profiles.Any(p => allowedProfiles.Contains(p.type));
            return true;
        }

        // Return true if the given user is a student
        async Task<bool> NeedForceWriteAsync()
        {
            var root = await GetRootAsync();
            if (root is Structure)
                return context.user.user.profiles.Where(p => p.structure_id == ((Structure)root).node.etablissement_uai).Any(p => p.type == "ELV");
            else if (root is GroupeLibre)
                return context.user.user.profiles.Any(p => p.type == "ELV");
            return true;
        }

        public override async Task<ItemRight> RightsAsync()
        {
            var rights = await base.RightsAsync();
            if (context.user.IsSuperAdmin)
                rights = new ItemRight { Read = true, Write = true, Locked = false };
            else
            {
                if (!await HasSeeAllRightAsync())
                    rights.Locked = true;
                if (await NeedForceWriteAsync())
                    rights.Write = node.return_date == null || node.return_date > DateTime.Now;
            }
            return rights;
        }

        static string CleanName(string name, string lastname, string firstname)
        {
            var match = Regex.Match(name, $"^{lastname} {firstname} \\- [0-9]{1,2}\\-[0-9]{1,2}\\-[0-9]{4} \\- (.*)$");
            return match.Success ? match.Groups[1].Value : name;
        }

        static string BuildChildName(string name, string lastname, string firstname)
        {
            name = CleanName(name, lastname, firstname);
            return $"{lastname} {firstname} - {DateTime.Now.ToString("dd-MM-yyyy")} - {name}";
        }

        protected override async Task OnBeforeChildChangedAsync(Item child, ChildAction action, FileDefinition<Node> fileDefinition)
        {
            await base.OnBeforeChildChangedAsync(child, action, fileDefinition);
            var name = child.node.name;
            if (action == ChildAction.Create || action == ChildAction.MoveIn || action == ChildAction.Update)
            {
                name = BuildChildName(name, context.user.user.lastname, context.user.user.firstname);
                child.node.name = await ChildsConflictFreeNameAsync(name, " version");
            }
        }
    }

    public class Profs : Folder
    {
        public Profs(Context context, Node node) : base(context, node)
        {
        }

        // Return true if the given user can view all files
        async Task<bool> IsEnsAsync()
        {
            var allowedProfiles = new string[] { "ACA", "ETA", "EVS", "ENS", "DOC", "DIR", "ADM" };
            var root = await GetRootAsync();
            if (root is Structure)
                return context.user.user.profiles.Where(p => p.structure_id == ((Structure)root).node.etablissement_uai).Any(p => allowedProfiles.Contains(p.type));
            else if (root is GroupeLibre)
                return context.user.user.profiles.Any(p => allowedProfiles.Contains(p.type));
            return true;
        }

        public override async Task<ItemRight> RightsAsync()
        {
            var rights = await base.RightsAsync();
            if (context.user.IsSuperAdmin)
                rights = new ItemRight { Read = true, Write = true, Locked = false };
            else
            {
                if (await IsEnsAsync())
                {
                    rights.Read = true;
                    rights.Write = true;
                }
                else
                {
                    rights.Read = false;
                    rights.Write = false;
                }
            }
            return rights;
        }
    }


}
