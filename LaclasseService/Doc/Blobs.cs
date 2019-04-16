// Blob.cs
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2018-2019 Metropole de Lyon
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Laclasse.Authentication;
using Laclasse.Storage;
using Erasme.Json;
using Erasme.Http;

namespace Laclasse.Doc
{
    [Model(Table = "blob", PrimaryKey = nameof(id), DB = "DOCS")]
    public class Blob : Model
    {
        [ModelField]
        public string id { get { return GetField<string>(nameof(id), null); } set { SetField(nameof(id), value); } }
        [ModelField(ForeignModel = typeof(Blob))]
        public string parent_id { get { return GetField<string>(nameof(parent_id), null); } set { SetField(nameof(parent_id), value); } }
        [ModelField]
        public string name { get { return GetField<string>(nameof(name), null); } set { SetField(nameof(name), value); } }
        [ModelField]
        public string mimetype { get { return GetField<string>(nameof(mimetype), null); } set { SetField(nameof(mimetype), value); } }
        [ModelField]
        public long size { get { return GetField(nameof(size), 0L); } set { SetField(nameof(size), value); } }
        [ModelField]
        public DateTime ctime { get { return GetField(nameof(ctime), DateTime.Now); } set { SetField(nameof(ctime), value); } }
        [ModelField]
        public DateTime? dtime { get { return GetField<DateTime?>(nameof(dtime), null); } set { SetField(nameof(dtime), value); } }
        [ModelField]
        public string sha1 { get { return GetField<string>(nameof(sha1), null); } set { SetField(nameof(sha1), value); } }
        [ModelField]
        public string md5 { get { return GetField<string>(nameof(md5), null); } set { SetField(nameof(md5), value); } }
        [ModelField]
        public string data { get { return GetField<string>(nameof(data), null); } set { SetField(nameof(data), value); } }

        [ModelExpandField(Name = nameof(children), ForeignModel = typeof(Blob))]
        public ModelList<Blob> children { get { return GetField<ModelList<Blob>>(nameof(children), null); } set { SetField(nameof(children), value); } }
    }

    public delegate void ProcessContentHandler(JsonValue data, string contentFilePath);

    public class FileDefinition
    {
        public string Mimetype;
        public long Size;
        public string Name;
        public Stream Stream;
        public ProcessContentHandler ProcessContent;
    }

    public class FileDefinition<T> : FileDefinition
    {
        public T Define;
    }

    public class Blobs : Directory.ModelService<Blob>, IDisposable
    {
        Logger logger;
        string dbUrl;
        string path;
        string tempDir;
        Storage.Storage storage;

        Thread cleanThread;
        object cleanLock = new object();
        bool cleanStop = false;

        public Blobs(Logger logger, string dbUrl, string path, string tempDir) : base(dbUrl)
        {
            this.logger = logger;
            this.dbUrl = dbUrl;
            this.path = path;
            this.tempDir = tempDir;
            this.storage = new Storage.Storage(path, tempDir);

            cleanThread = new Thread(ThreadStart);
            cleanThread.Name = "BlobCleanThread";
            cleanThread.Start();

            BeforeAsync = async (p, c) => await c.EnsureIsSuperAdminAsync();

            PostAsync["/"] = async (p, c) =>
            {
                var fileDefinition = await GetFileDefinitionAsync<Blob>(c);

                string tempFile; Blob blob;
                (blob, tempFile) = await PrepareBlobAsync(fileDefinition, fileDefinition.Define);

                using (var db = await DB.CreateAsync(dbUrl, true))
                {
                    blob = await CreateBlobFromTempFileAsync(db, blob, tempFile);
                    await db.CommitAsync();
                }

                c.Response.StatusCode = 200;
                c.Response.Content = blob.ToJson();
            };

            DeleteAsync["/{id}"] = async (p, c) =>
            {
                bool deleted = false;
                string id = (string)p["id"];
                if (id == null)
                    return;

                using (var db = await DB.CreateAsync(dbUrl, true))
                {
                    deleted = await DeleteBlobAsync(db, id);
                    await db.CommitAsync();
                }
                if (deleted)
                {
                    c.Response.StatusCode = 200;
                    c.Response.Content = "";
                }
            };

            GetAsync["/{id}/content"] = async (p, c) =>
            {
                using (var db = await DB.CreateAsync(dbUrl))
                {
                    var blob = new Blob { id = (string)p["id"] };
                    if (await blob.LoadAsync(db))
                    {
                        var stream = storage[(string)p["id"]];
                        c.Response.Headers["content-type"] = blob.mimetype;
                        c.Response.StatusCode = 200;
                        c.Response.SupportRanges = true;
                        c.Response.Content = stream;
                    }
                }
            };
        }

        void ThreadStart()
        {
            logger.Log(LogLevel.Info, "Blob Clean Thread START");
            bool stop = false;
            lock (cleanLock)
            {
                while (!stop)
                {
                    try
                    {
                        using (var db = DB.Create(dbUrl, true))
                        {
                            var task = db.SelectAsync<Blob>("SELECT * FROM `blob` WHERE `dtime` IS NOT NULL AND TIMESTAMPDIFF(SECOND, `dtime`, NOW()) >= 3600");
                            task.Wait();
                            var removeBlobs = task.Result;

                            logger.Log(LogLevel.Info, $"Blob clean needed: {removeBlobs.Count}");
                            foreach (var blob in removeBlobs)
                            {
                                bool deleted = false;
                                ModelList<Blob> children = null;
                                task = db.SelectAsync<Blob>("SELECT * FROM `blob` WHERE `parent_id` = ?", blob.id);
                                task.Wait();
                                children = task.Result;

                                var deleteChildrenTask = db.DeleteAsync("DELETE FROM `blob` WHERE `parent_id` = ?", blob.id);
                                deleteChildrenTask.Wait();

                                var deleteTask = blob.DeleteAsync(db);
                                deleteTask.Wait();
                                deleted = deleteTask.Result;
                                if (deleted)
                                {
                                    storage.Remove(blob.id);
                                    if (children != null)
                                        children.ForEach((b) => storage.Remove(b.id));
                                }
                            }
                            var commitTask = db.CommitAsync();
                            commitTask.Wait();
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(LogLevel.Error, $"Blob Thread Exception: {e.ToString()}");
                    }
                    Monitor.Wait(cleanLock, TimeSpan.FromHours(1));
                    stop = cleanStop;
                }
            }
            logger.Log(LogLevel.Info, "Blob Thread STOP");
        }


        public async Task<Blob> GetBlobAsync(DB db, string id)
        {
            var blob = new Blob { id = id };
            return (await blob.LoadAsync(db, true)) ? blob : null;
        }

        public async Task<Blob> SearchSameBlobAsync(DB db, Blob blob)
        {
            var blobs = await db.SelectAsync<Blob>("SELECT * FROM `blob` WHERE `parent_id` IS NULL AND `size` = ? AND `sha1` = ? AND `md5` = ? AND `dtime` IS NULL", blob.size, blob.sha1, blob.md5);
            Blob res = null;
            if (blobs.Count > 0)
            {
                res = blobs[0];
                await res.LoadAsync(db, true);
            }
            return res;
        }

        public Stream GetBlobStream(string id)
        {
            return storage[id];
        }

        public async Task<Blob> CreateBlobAsync(DB db, FileDefinition fileDefinition, Blob blob = null)
        {
            string tempFile;
            (blob, tempFile) = await PrepareBlobAsync(fileDefinition, blob);
            return await CreateBlobFromTempFileAsync(db, blob, tempFile);
        }

        public async Task<Blob> CreateBlobFromTempFileAsync(DB db, Blob blob, string tempFile)
        {
            if (tempFile != null)
            {
                // if needed get the file size
                if (blob.size == 0)
                {
                    var fileInfo = new FileInfo(tempFile);
                    blob.size = fileInfo.Length;
                }
                // if needed calc the signatures
                if (blob.sha1 == null || blob.md5 == null)
                {
                    var size = 0;
                    var count = 0;
                    var buffer = new byte[4096];
                    using (Stream fileStream = File.OpenRead(tempFile))
                    using (SHA1 sha1 = SHA1.Create())
                    using (MD5 md5 = MD5.Create())
                    {
                        while ((count = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            size += count;
                            md5.TransformBlock(buffer, 0, count, buffer, 0);
                            sha1.TransformBlock(buffer, 0, count, buffer, 0);
                        }
                        if (size > 0)
                        {
                            md5.TransformFinalBlock(buffer, 0, 0);
                            sha1.TransformFinalBlock(buffer, 0, 0);
                            blob.md5 = BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();
                            blob.sha1 = BitConverter.ToString(sha1.Hash).Replace("-", "").ToLowerInvariant();
                        }
                    }
                }
                storage.Add(blob.id, tempFile);
            }
            ModelList<Blob> removeChildren = null;

            if (blob.parent_id != null && blob.name != null)
            {
                removeChildren = await db.SelectAsync<Blob>("SELECT * FROM `blob` WHERE `parent_id` = ? AND `name` = ? AND `dtime` IS NULL", blob.parent_id, blob.name);
                foreach (var child in removeChildren)
                    await new Blob { id = child.id, dtime = DateTime.Now, parent_id = null }.UpdateAsync(db);
            }
            await blob.SaveAsync(db);
            return blob;
        }

        public async Task<bool> DeleteBlobAsync(DB db, string id)
        {
            bool deleted = false;
            if (id == null)
                return false;
            var blob = new Blob { id = id };
            if (await blob.LoadAsync(db))
            {
                blob.dtime = DateTime.Now;
                deleted = await blob.UpdateAsync(db);
            }
            return deleted;
        }

        public async Task<(Blob, string)> PrepareBlobAsync(FileDefinition fileDefinition, Blob blob = null)
        {
            if (blob == null)
                blob = new Blob();

            var guid = Guid.NewGuid().ToString();

            blob.id = guid;
            if (blob.name == null)
                blob.name = fileDefinition.Name;
            if (blob.mimetype == null)
                blob.mimetype = fileDefinition.Mimetype;

            if (fileDefinition.Stream == null)
            {
                blob.size = 0;
                blob.md5 = null;
                blob.sha1 = null;
                return (blob, null);
            }

            string tempFile = Path.Combine(tempDir, guid);

            var buffer = new byte[16384];
            int count = 0;
            long size = 0;
            string md5sum = "";
            string sha1sum = "";

            using (FileStream fileStream = new FileStream(tempFile, FileMode.CreateNew))
            using (SHA1 sha1 = SHA1.Create())
            using (MD5 md5 = MD5.Create())
            {
                while ((count = await fileDefinition.Stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    size += count;
                    await fileStream.WriteAsync(buffer, 0, count);
                    md5.TransformBlock(buffer, 0, count, buffer, 0);
                    sha1.TransformBlock(buffer, 0, count, buffer, 0);
                }
                if (size > 0)
                {
                    md5.TransformFinalBlock(buffer, 0, 0);
                    sha1.TransformFinalBlock(buffer, 0, 0);
                    md5sum = BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();
                    sha1sum = BitConverter.ToString(sha1.Hash).Replace("-", "").ToLowerInvariant();
                }
            }

            blob.size = size;
            blob.md5 = md5sum;
            blob.sha1 = sha1sum;

            return (blob, tempFile);
        }

        public static async Task<FileDefinition<T>> GetFileDefinitionAsync<T>(HttpContext context) where T : Model, new()
        {
            string filename = null;
            string mimetype = null;
            long size = 0;
            T define = null;
            string fileContentType = null;
            Stream fileContentStream = null;

            string contentType = context.Request.Headers["content-type"];
            if (contentType.IndexOf("multipart/form-data", StringComparison.InvariantCulture) >= 0)
            {
                MultipartReader reader = context.Request.ReadAsMultipart();
                MultipartPart part;

                while ((part = await reader.ReadPartAsync()) != null)
                {
                    // the JSON define part
                    if (part.Headers.ContentDisposition["name"] == "define")
                    {
                        StreamReader streamReader = new StreamReader(part.Stream, Encoding.UTF8);
                        string jsonString = await streamReader.ReadToEndAsync();
                        define = new T();
                        define.FromJson(JsonValue.Parse(jsonString) as JsonObject, null, context);
                    }
                    // the file content
                    else if (part.Headers.ContentDisposition["name"] == "file")
                    {
                        if ((filename == null) && part.Headers.ContentDisposition.ContainsKey("filename"))
                            filename = part.Headers.ContentDisposition["filename"];
                        if (part.Headers.ContainsKey("content-type"))
                            fileContentType = part.Headers["content-type"];

                        fileContentStream = part.Stream;
                        // file part MUST BE THE LAST ONE
                        break;
                    }
                }
            }
            else
            {
                define = new T();
                define.FromJson(await context.Request.ReadAsJsonAsync() as JsonObject, null, context);
            }

            if (filename == null)
            {
                filename = "unknown";
                if (mimetype == null)
                    mimetype = "application/octet-stream";
            }
            else if (mimetype == null)
            {
                // if mimetype was not given in the define part, decide it from
                // the file extension
                mimetype = FileContent.MimeType(filename);
                // if not found from the file extension, decide it from the Content-Type
                if ((mimetype == "application/octet-stream") && (fileContentType != null))
                    mimetype = fileContentType;
            }

            return new FileDefinition<T>()
            {
                Mimetype = mimetype,
                Size = size,
                Name = filename,
                Stream = fileContentStream,
                Define = define
            };
        }

        public void Dispose()
        {
            lock (cleanLock)
            {
                cleanStop = true;
                Monitor.PulseAll(cleanLock);
            }
        }
    }
}
