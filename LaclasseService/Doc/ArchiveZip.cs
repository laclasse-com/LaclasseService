using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
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
    }
}
