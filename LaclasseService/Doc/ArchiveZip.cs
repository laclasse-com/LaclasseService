using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace Laclasse.Doc
{
    public class ArchiveZip
    {
        public static async Task<Item> CreateAsync(Context context, long[] files, long parentId, string name)
        {
            var guid = Guid.NewGuid().ToString();
            string tempFile = Path.Combine(context.tempDir, guid);
            using (var zip = File.OpenWrite(tempFile))
            using (var zipWriter = WriterFactory.Open(zip, ArchiveType.Zip, CompressionType.Deflate))
            {
                Func<Item, string, Task> AddItemAsync = null;
                AddItemAsync = async (Item item, string path) =>
                {
                    if (item is Folder)
                    {
                        path += item.node.name + "/";
                        var children = await ((Folder)item).GetFilteredChildrenAsync();
                        foreach (var child in children)
                            await AddItemAsync(child, path);
                    }
                    else
                    {
                        var mtime = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(item.node.mtime);
                        zipWriter.Write(path + item.node.name, await item.GetContentAsync(), mtime);
                    }
                };
                foreach (var fileId in files)
                {
                    var file = await context.GetByIdAsync(fileId);
                    await AddItemAsync(file, "/");
                }
            }
            return await Item.CreateAsync(context, new FileDefinition<Node>
            {
                Name = name,
                Mimetype = "application/zip",
                Stream = File.OpenRead(tempFile),
                Define = new Node
                {
                    name = name,
                    mime = "application/zip",
                    parent_id = parentId
                }
            });
        }

        public static async Task<Stream> DownloadAsArchiveAsync(Context context, long[] files)
        {
            var guid = Guid.NewGuid().ToString();
            string tempFile = Path.Combine(context.tempDir, guid);
            using (var zip = File.OpenWrite(tempFile))
            using (var zipWriter = WriterFactory.Open(zip, ArchiveType.Zip, CompressionType.Deflate))
            {
                Func<Item, string, Task> AddItemAsync = null;
                AddItemAsync = async (Item item, string path) =>
                {
                    if (item is Folder)
                    {
                        path += item.node.name + "/";
                        var children = await ((Folder)item).GetFilteredChildrenAsync();
                        foreach (var child in children)
                            await AddItemAsync(child, path);
                    }
                    else
                    {
                        var mtime = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(item.node.mtime);
                        zipWriter.Write(path + item.node.name, await item.GetContentAsync(), mtime);
                    }
                };
                foreach (var fileId in files)
                {
                    var file = await context.GetByIdAsync(fileId);
                    await AddItemAsync(file, "/");
                }
            }
            var stream = File.OpenRead(tempFile);
            File.Delete(tempFile);
            return stream;
        }

        public static async Task<List<Item>> ExtractAsync(Context context, long fileId, long parentId, string name)
        {
            var res = new List<Item>();
            var file = await context.GetByIdAsync(fileId);
            var parent = (Folder)await context.GetByIdAsync(parentId);

            Func<Folder, string, Task<Folder>> FindOrCreateFolderAsync = null;
            FindOrCreateFolderAsync = async (Folder dir, string path) =>
            {
                if (path == "")
                    return dir;
                var children = await dir.GetFilteredChildrenAsync();
                var currentFolder = path;
                var remainPath = "";
                var firstPos = path.IndexOf('/');
                if (firstPos != -1)
                {
                    currentFolder = path.Substring(0, firstPos);
                    remainPath = path.Substring(firstPos + 1);
                }
                var folder = children.FirstOrDefault((c) => c.node.name == currentFolder && c is Folder) as Folder;
                // not found, create it
                if (folder == null)
                {
                    folder = await Folder.CreateAsync(context, currentFolder, dir.node.id);
                    res.Add(folder);
                }
                // recursive search / create
                return await FindOrCreateFolderAsync(folder, remainPath);
            };


            using (var reader = ReaderFactory.Open(await file.GetContentAsync()))
            {
                while (reader.MoveToNextEntry())
                {
                    var path = "";
                    var fileName = reader.Entry.Key;
                    var lastPos = reader.Entry.Key.LastIndexOf('/');
                    if (lastPos != -1)
                    {
                        path = fileName.Substring(0, lastPos);
                        fileName = fileName.Substring(lastPos + 1);
                    }

                    Folder dir = parent;
                    if (path != "")
                        dir = await FindOrCreateFolderAsync(parent, path);

                    var fileDefinition = new FileDefinition<Node>
                    {
                        Name = fileName,
                        Stream = reader.OpenEntryStream(),
                        Define = new Node
                        {
                            parent_id = dir.node.id
                        }
                    };
                    var item = await Item.CreateAsync(context, fileDefinition);
                    res.Add(item);
                }
            }
            return res;
        }
    }
}
