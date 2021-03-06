﻿using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;

namespace Laclasse.Doc
{
    public class ArchiveZip
    {
        public static async Task<Item> CreateAsync(Context context, long[] files, long parentId, string name)
        {
            var guid = Guid.NewGuid().ToString();
            string tempFile = Path.Combine(context.tempDir, guid);
            using (var fileStream = File.OpenWrite(tempFile))
            using (var zipStream = new ZipOutputStream(fileStream))
            {
                Func<Item, string, Task> AddItemAsync = null;
                AddItemAsync = async (Item item, string path) =>
                {
                    if (item is Folder)
                    {
                        path += item.node.name + "/";
                        var children = await ((Folder)item).GetFilteredChildrenAsync();
                        foreach (var child in children)
                        {
                            // add if the user has read right
                            if ((await child.RightsAsync()).Read)
                                await AddItemAsync(child, path);
                        }
                    }
                    else
                    {
                        var mtime = item.node.mtime;
                        var zipEntry = new ZipEntry(ZipEntry.CleanName(path + item.node.name));
                        zipEntry.DateTime = mtime;
                        zipEntry.IsUnicodeText = true;
                        zipStream.PutNextEntry(zipEntry);
                        await (await item.GetContentAsync()).CopyToAsync(zipStream);
                        zipStream.CloseEntry();
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

        public static async Task DownloadAsArchiveAsync(Context context, Stream outStream, long[] files)
        {
            using (var zipStream = new ZipOutputStream(outStream))
            {
                Func<Item, string, Task> AddItemAsync = null;
                AddItemAsync = async (Item item, string path) =>
                {
                    if (item is Folder)
                    {
                        path += item.node.name + "/";
                        var children = await ((Folder)item).GetFilteredChildrenAsync();
                        foreach (var child in children)
                        {
                            // add if the user has read right
                            if ((await child.RightsAsync()).Read)
                                await AddItemAsync(child, path);
                        }
                    }
                    else
                    {
                        var mtime = item.node.mtime;
                        var zipEntry = new ZipEntry(ZipEntry.CleanName(path + item.node.name));
                        zipEntry.DateTime = mtime;
                        zipEntry.IsUnicodeText = true;
                        zipStream.PutNextEntry(zipEntry);
                        await (await item.GetContentAsync()).CopyToAsync(zipStream);
                        zipStream.CloseEntry();
                    }
                };
                foreach (var fileId in files)
                {
                    var file = await context.GetByIdAsync(fileId);
                    // check the user rights
                    if (context.user.IsUser && !(await file.RightsAsync()).Read)
                        throw new WebException(403, "User dont have read right");
                    await AddItemAsync(file, "/");
                }
            }
        }

        public static async Task<List<Item>> ExtractAsync(Context context, long fileId, long parentId)
        {
            var res = new List<Item>();
            var file = await context.GetByIdAsync(fileId);
            var parent = (Folder)await context.GetByIdAsync(parentId);
            // check the user rights
            if (context.user.IsUser && !(await file.RightsAsync()).Write)
                throw new WebException(403, "User dont have write right");

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

            using (var zipFile = new ZipFile(await file.GetContentAsync()))
            {
                foreach (ZipEntry zipEntry in zipFile)
                {
                    if (!zipEntry.IsFile)
                        continue;
                    var path = "";
                    var fileName = zipEntry.Name;
                    var lastPos = fileName.LastIndexOf('/');
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
                        Stream = zipFile.GetInputStream(zipEntry),
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
