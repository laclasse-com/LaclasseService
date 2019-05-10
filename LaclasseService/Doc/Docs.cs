// Log.cs
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2018 Metropole de Lyon
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using ICSharpCode.SharpZipLib.Zip;
using Laclasse.Authentication;

namespace Laclasse.Doc
{
    public enum OnlyOfficeMode
    {
        Desktop,
        Mobile
    }

    public enum OnlyOfficeFileType
    {
        // text
        docx, // application/vnd.openxmlformats-officedocument.wordprocessingml.document
        doc,  // application/msword
        epub, // application/epub+zip
        odt,  // application/vnd.oasis.opendocument.text 
        rtf,  // application/rtf
        txt,  // text/plain
        xps,  // application/vnd.ms-xpsdocument
        dotx, // application/vnd.openxmlformats-officedocument.wordprocessingml.template

        // spreadsheet
        xlsx, // application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
        xls,  // application/vnd.ms-excel
        ods,  // application/vnd.oasis.opendocument.spreadsheet
        csv,  // text/csv

        // presentation
        pptx, // application/vnd.openxmlformats-officedocument.presentationml.presentation
        ppt,  // application/vnd.ms-powerpoint
        odp   // application/vnd.oasis.opendocument.presentation
    }

    public enum OnlyOfficeDocumentType
    {
        text,
        spreadsheet,
        presentation
    }

    public partial class OnlyOfficeView
    {
        public OnlyOfficeDocumentType documentType = OnlyOfficeDocumentType.text;
        public OnlyOfficeFileType fileType = OnlyOfficeFileType.docx;
        public OnlyOfficeMode mode = OnlyOfficeMode.Desktop;
        public Node node = null;
        public Directory.User user = null;
        public string downloadUrl = null;
        public string callbackUrl = null;
        public bool edit = true;
    }

    public enum RightProfile
    {
        TUT,
        ENS,
        ELV
    }

    [Model(Table = "right", PrimaryKey = nameof(id), DB = "DOCS")]
    public class Right : Model
    {
        [ModelField]
        public long id { get { return GetField(nameof(id), 0L); } set { SetField(nameof(id), value); } }
        [ModelField(Required = true, ForeignModel = typeof(Node))]
        public long? node_id { get { return GetField<long?>(nameof(node_id), null); } set { SetField(nameof(node_id), value); } }
        [ModelField]
        public RightProfile profile { get { return GetField<RightProfile>(nameof(profile), RightProfile.ELV); } set { SetField(nameof(profile), value); } }
        [ModelField]
        public bool read { get { return GetField(nameof(read), true); } set { SetField(nameof(read), value); } }
        [ModelField]
        public bool write { get { return GetField(nameof(write), true); } set { SetField(nameof(write), value); } }
    }


    [Model(Table = "node", PrimaryKey = nameof(id), DB = "DOCS")]
    public class Node : Model
    {
        [ModelField]
        public long id { get { return GetField(nameof(id), 0L); } set { SetField(nameof(id), value); } }
        [ModelField(Required = false, ForeignModel = typeof(Node))]
        public long? parent_id { get { return GetField<long?>(nameof(parent_id), null); } set { SetField(nameof(parent_id), value); } }
        [ModelField]
        public string name { get { return GetField<string>(nameof(name), null); } set { SetField(nameof(name), value); } }
        [ModelField]
        public string content { get { return GetField<string>(nameof(content), null); } set { SetField(nameof(content), value); } }
        [ModelField]
        public long size { get { return GetField(nameof(size), 0L); } set { SetField(nameof(size), value); } }
        [ModelField]
        public DateTime ctime { get { return GetField(nameof(ctime), DateTime.Now); } set { SetField(nameof(ctime), value); } }
        [ModelField]
        public DateTime mtime { get { return GetField(nameof(mtime), DateTime.Now); } set { SetField(nameof(mtime), value); } }
        [ModelField]
        public string mime { get { return GetField<string>(nameof(mime), null); } set { SetField(nameof(mime), value); } }
        [ModelField]
        public bool read { get { return GetField(nameof(read), true); } set { SetField(nameof(read), value); } }
        [ModelField]
        public bool write { get { return GetField(nameof(write), true); } set { SetField(nameof(write), value); } }
        [ModelField]
        public bool locked { get { return GetField(nameof(locked), false); } set { SetField(nameof(locked), value); } }
        [ModelField]
        public bool hidden { get { return GetField(nameof(hidden), false); } set { SetField(nameof(hidden), value); } }
        [ModelField]
        public int width { get { return GetField(nameof(width), 0); } set { SetField(nameof(width), value); } }
        [ModelField]
        public int height { get { return GetField(nameof(height), 0); } set { SetField(nameof(height), value); } }
        [ModelField]
        public string owner { get { return GetField<string>(nameof(owner), null); } set { SetField(nameof(owner), value); } }
        [ModelField]
        public string owner_firstname { get { return GetField<string>(nameof(owner_firstname), null); } set { SetField(nameof(owner_firstname), value); } }
        [ModelField]
        public string owner_lastname { get { return GetField<string>(nameof(owner_lastname), null); } set { SetField(nameof(owner_lastname), value); } }
        [ModelField]
        public string cartable_uid { get { return GetField<string>(nameof(cartable_uid), null); } set { SetField(nameof(cartable_uid), value); } }
        [ModelField]
        public string etablissement_uai { get { return GetField<string>(nameof(etablissement_uai), null); } set { SetField(nameof(etablissement_uai), value); } }
        [ModelField]
        public int? classe_id { get { return GetField<int?>(nameof(classe_id), null); } set { SetField(nameof(classe_id), value); } }
        [ModelField]
        public int? groupe_id { get { return GetField<int?>(nameof(groupe_id), null); } set { SetField(nameof(groupe_id), value); } }
        [ModelField]
        public int? groupe_libre_id { get { return GetField<int?>(nameof(groupe_libre_id), null); } set { SetField(nameof(groupe_libre_id), value); } }
        [ModelField]
        public DateTime? return_date { get { return GetField<DateTime?>(nameof(return_date), null); } set { SetField(nameof(return_date), value); } }
        [ModelField]
        public int rev { get { return GetField(nameof(rev), 0); } set { SetField(nameof(rev), value); } }
        [ModelField(ForeignModel = typeof(Blob))]
        public string blob_id { get { return GetField<string>(nameof(blob_id), null); } set { SetField(nameof(blob_id), value); } }
        [ModelField]
        public bool has_tmb { get { return GetField(nameof(has_tmb), false); } set { SetField(nameof(has_tmb), value); } }

        [ModelExpandField(Name = nameof(children), ForeignModel = typeof(Node))]
        public ModelList<Node> children { get { return GetField<ModelList<Node>>(nameof(children), null); } set { SetField(nameof(children), value); } }
        [ModelExpandField(Name = nameof(rights), ForeignModel = typeof(Right))]
        public ModelList<Right> rights { get { return GetField<ModelList<Right>>(nameof(rights), null); } set { SetField(nameof(rights), value); } }

        [ModelExpandField(Name = nameof(blob), ForeignModel = typeof(Blob))]
        public Blob blob { get { return GetField<Blob>(nameof(blob), null); } set { SetField(nameof(blob), value); } }
    }

    public class DocStructure : Model
    {
        [ModelField]
        public string structure_id { get { return GetField<string>(nameof(structure_id), null); } set { SetField(nameof(structure_id), value); } }
        [ModelField]
        public long node_id { get { return GetField(nameof(node_id), 0L); } set { SetField(nameof(node_id), value); } }
        [ModelField]
        public string name { get { return GetField<string>(nameof(name), null); } set { SetField(nameof(name), value); } }
        [ModelField]
        public long size { get { return GetField(nameof(size), 0L); } set { SetField(nameof(size), value); } }
        [ModelField]
        public DateTime mtime { get { return GetField(nameof(mtime), DateTime.MinValue); } set { SetField(nameof(mtime), value); } }
        [ModelField]
        public bool is_deleted { get { return GetField(nameof(is_deleted), false); } set { SetField(nameof(is_deleted), value); } }
    }

    public class DocUser : Model
    {
        [ModelField]
        public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
        [ModelField]
        public long node_id { get { return GetField(nameof(node_id), 0L); } set { SetField(nameof(node_id), value); } }
        [ModelField]
        public string firstname { get { return GetField<string>(nameof(firstname), null); } set { SetField(nameof(firstname), value); } }
        [ModelField]
        public string lastname { get { return GetField<string>(nameof(lastname), null); } set { SetField(nameof(lastname), value); } }
        [ModelField]
        public long size { get { return GetField(nameof(size), 0L); } set { SetField(nameof(size), value); } }
        [ModelField]
        public DateTime mtime { get { return GetField(nameof(mtime), DateTime.MinValue); } set { SetField(nameof(mtime), value); } }
        [ModelField]
        public bool is_deleted { get { return GetField(nameof(is_deleted), false); } set { SetField(nameof(is_deleted), value); } }
    }

    public class DocGroup : Model
    {
        [ModelField]
        public int group_id { get { return GetField(nameof(group_id), 0); } set { SetField(nameof(group_id), value); } }
        [ModelField]
        public long node_id { get { return GetField(nameof(node_id), 0L); } set { SetField(nameof(node_id), value); } }
        [ModelField]
        public string name { get { return GetField<string>(nameof(name), null); } set { SetField(nameof(name), value); } }
        [ModelField]
        public long size { get { return GetField(nameof(size), 0L); } set { SetField(nameof(size), value); } }
        [ModelField]
        public DateTime mtime { get { return GetField(nameof(mtime), DateTime.MinValue); } set { SetField(nameof(mtime), value); } }
        [ModelField]
        public bool is_deleted { get { return GetField(nameof(is_deleted), false); } set { SetField(nameof(is_deleted), value); } }
    }

    [Model(Table = "onlyoffice_session", PrimaryKey = nameof(id), DB = "DOCS")]
    public class OnlyOfficeSession : Model
    {
        [ModelField]
        public string id { get { return GetField<string>(nameof(id), null); } set { SetField(nameof(id), value); } }
        [ModelField]
        public string key { get { return GetField<string>(nameof(key), null); } set { SetField(nameof(key), value); } }
        [ModelField]
        public long node_id { get { return GetField(nameof(node_id), 0L); } set { SetField(nameof(node_id), value); } }
        [ModelField]
        public DateTime ctime { get { return GetField(nameof(ctime), DateTime.MinValue); } set { SetField(nameof(ctime), value); } }
        [ModelField]
        public bool write { get { return GetField(nameof(write), false); } set { SetField(nameof(write), value); } }
        [ModelField]
        public int rev { get { return GetField(nameof(rev), 0); } set { SetField(nameof(rev), value); } }
    }

    /// <summary>
    /// Create a Seekable Stream from a ZIP Entry. This allow support
    /// for bytes ranges.
    /// </summary>
    public class ZipEntryStream : Stream
    {
        readonly ZipFile file;
        readonly ZipEntry entry;
        Stream stream;
        long position;
        long length;

        public ZipEntryStream(ZipFile file, ZipEntry entry)
        {
            this.file = file;
            this.entry = entry;
            stream = file.GetInputStream(entry);
            length = entry.Size;
            position = 0;
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                return length;
            }
        }

        public override long Position
        {
            get
            {
                return position;
            }
            set
            {
                if (value != this.position)
                    Seek(value, SeekOrigin.Begin);
            }
        }

        public override int ReadByte()
        {
            if (length - position <= 0)
                return -1;

            position++;
            return stream.ReadByte();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var task = ReadAsync(buffer, offset, count);
            task.Wait();
            return task.Result;
        }

        public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var remains = (int)(length - position);
            if (remains <= 0)
                return 0;
            var size = await stream.ReadAsync(buffer, offset, Math.Min(count, remains));
            position += size;
            return size;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var position = this.position;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    position = offset;
                    break;
                case SeekOrigin.End:
                    position = length - offset;
                    break;
                case SeekOrigin.Current:
                    position += offset;
                    break;
            }
            position = Math.Max(0, Math.Min(length, position));
            var delta = position - this.position;
            //  need to go back, reopen the stream
            if (delta < 0)
            {
                stream = file.GetInputStream(entry);
                this.position = 0;
                delta = position;
            }
            if (delta > 0)
            {
                var buffer = new byte[4096];
                while (delta > 0)
                    delta -= stream.Read(buffer, 0, (int)Math.Min(buffer.LongLength, delta));
            }
            this.position = position;
            return this.position;
        }

        public override void SetLength(long value)
        {
            length = Math.Min(value, entry.Size);
        }
    }

    public class Docs : HttpRouting
    {
        string dbUrl;
        string path;
        string tempDir;
        string directoryDbUrl;
        internal Setup globalSetup;
        internal DocSetup setup;
        internal Blobs blobs;
        Dictionary<string, List<IFilePlugin>> mimePlugins = new Dictionary<string, List<IFilePlugin>>();
        List<IFilePlugin> allPlugins = new List<IFilePlugin>();
        readonly SemaphoreSlim nodeChangeLock = new SemaphoreSlim(1, 1);

        internal class TempFile
        {
            public string Id;
            public Stream Stream;
            public string Mime;
        }

        internal object tempFilesLock = new object();
        internal Dictionary<string, TempFile> tempFiles = new Dictionary<string, TempFile>();

        public class OnlyOfficeCallbackData : TaskCompletionSource<OnlyOffice>
        {
            public OnlyOfficeCallbackData(int timeoutms)
            {
                var ct = new CancellationTokenSource(timeoutms);
                ct.Token.Register(() => TrySetCanceled());
            }
        }

        internal object onlyOfficeCallbacksLock = new object();
        internal Dictionary<long, List<OnlyOfficeCallbackData>> onlyOfficeCallbacks = new Dictionary<long, List<OnlyOfficeCallbackData>>();

        public static Dictionary<string, OnlyOfficeFileType> OnlyOfficeMimes = new Dictionary<string, OnlyOfficeFileType>
        {
            // text
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = OnlyOfficeFileType.docx,
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.template"] = OnlyOfficeFileType.dotx,
            ["application/msword"] = OnlyOfficeFileType.doc,
            ["application/epub+zip"] = OnlyOfficeFileType.epub,
            ["application/vnd.oasis.opendocument.text"] = OnlyOfficeFileType.odt,
            ["application/rtf"] = OnlyOfficeFileType.rtf,
            ["text/plain"] = OnlyOfficeFileType.txt,
            ["application/vnd.ms-xpsdocument"] = OnlyOfficeFileType.xps,

            // spreadsheet
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = OnlyOfficeFileType.xlsx,
            ["application/vnd.ms-excel"] = OnlyOfficeFileType.xls,
            ["application/vnd.oasis.opendocument.spreadsheet"] = OnlyOfficeFileType.ods,
            ["text/csv"] = OnlyOfficeFileType.csv,

            // presentation
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = OnlyOfficeFileType.pptx,
            ["application/vnd.ms-powerpoint"] = OnlyOfficeFileType.ppt,
            ["application/vnd.oasis.opendocument.presentation"] = OnlyOfficeFileType.odp
        };


        public Docs(string dbUrl, string path, string tempDir, Blobs blobs, int cacheDuration, string directoryDbUrl, Setup globalSetup)
        {
            this.dbUrl = dbUrl;
            this.path = path;
            this.tempDir = tempDir;
            this.blobs = blobs;
            this.globalSetup = globalSetup;
            this.setup = globalSetup.doc;
            this.directoryDbUrl = directoryDbUrl;

            BeforeAsync = async (p, c) =>
            {
                // no authentication for onlyoffice GET or POST
                if ((c.Request.Method == "POST" || c.Request.Method == "GET") && Regex.IsMatch(c.Request.Path, "^/[0-9]+/onlyoffice$"))
                    return;
                // no authentication for tempFile
                if (c.Request.Path.StartsWith("/tempFile/", StringComparison.InvariantCulture))
                    return;
                // no authentication for onlyoffice GET file
                if (c.Request.Method == "GET" && Regex.IsMatch(c.Request.Path, "^/[0-9]+/onlyoffice/file$"))
                    return;
                // no authentication when getting file content
                if (c.Request.Method == "GET" && Regex.IsMatch(c.Request.Path, "^/[0-9]+/content$"))
                    return;
                // authentication needed
                await c.EnsureIsAuthenticatedAsync();
            };

            GetAsync["/migration3to4"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                await Migration3To4Async(c);
            };

            PostAsync["/generateblob"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                var json = await c.Request.ReadAsJsonAsync();

                using (DB db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                    foreach (var id in json as JsonArray)
                    {
                        var item = await context.GetByIdAsync(id);
                        if (item != null)
                        {
                            // try our best
                            try
                            {
                                await item.GenerateBlobAsync();
                            }
                            catch (WebException) { }
                        }
                    }
                    await db.CommitAsync();
                }
                c.Response.StatusCode = 200;
            };

            PostAsync["/rebuildsize"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                var json = await c.Request.ReadAsJsonAsync();
                foreach (var jsonNodeId in json as JsonArray)
                {
                    var nodeId = (long)jsonNodeId;
                    using (DB db = await DB.CreateAsync(dbUrl, true))
                    {
                        Func<Node, Task<Tuple<long, DateTime>>> UpdateNodeAsync = null;
                        UpdateNodeAsync = async (Node node) =>
                        {
                            var children = await db.SelectAsync<Node>($"SELECT * FROM `node` WHERE `parent_id`=?", node.id);
                            long totalSize = 0;
                            long totalCount = 0;
                            DateTime maxTime = DateTime.MinValue;
                            foreach (var child in children)
                            {
                                var res = await UpdateNodeAsync(child);
                                totalSize += res.Item1;
                                totalCount++;
                                maxTime = res.Item2 > maxTime ? res.Item2 : maxTime;
                            }
                            if (totalCount == 0 && node.mime.Contains("/"))
                                totalSize = node.size;
                            maxTime = node.mtime > maxTime ? node.mtime : maxTime;

                            var nodeDiff = new Node { id = node.id, size = totalSize, mtime = maxTime };
                            await nodeDiff.UpdateAsync(db);

                            return new Tuple<long, DateTime>(totalSize, maxTime);
                        };

                        var rootNodes = await db.SelectAsync<Node>($"SELECT * FROM `node` WHERE `id`=?", nodeId);
                        if (rootNodes.Count == 1)
                            await UpdateNodeAsync(rootNodes[0]);
                        await db.CommitAsync();
                    }
                }
                c.Response.StatusCode = 200;
            };

            GetAsync["/generatemissingtmb"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                await GenerateMissingThumbnails(c);
            };

            PostAsync["/generatetmb"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                var json = await c.Request.ReadAsJsonAsync();

                using (DB db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                    foreach (var id in json as JsonArray)
                    {
                        var item = await context.GetByIdAsync(id);
                        if (item != null && item.node.size > 0)
                        {
                            try
                            {
                                await item.GenerateThumbnailAsync();
                            }
                            catch (WebException) { }
                        }
                    }
                    await db.CommitAsync();
                }
                c.Response.StatusCode = 200;
            };

            GetAsync["/stats"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                var json = new JsonObject();
                using (DB db = await DB.CreateAsync(dbUrl, true))
                {
                    var res = await db.SelectAsync("SELECT * FROM(SELECT CONCAT(size,':',sha1,':',md5) AS str, COUNT(id) AS count, SUM(size) AS size FROM `blob` WHERE `parent_id` IS NULL GROUP BY CONCAT(size, ':', sha1, ':', md5)) AS t WHERE t.count > 1 ORDER BY count DESC");
                    long duplicateBlobs = 0;
                    long lostSize = 0;
                    foreach (var row in res)
                    {
                        duplicateBlobs++;
                        var count = Convert.ToInt64(row["count"]);
                        var size = Convert.ToInt64(row["count"]);
                        lostSize += size * (count - 1);
                    }

                    json["files"] = new JsonObject
                    {
                        ["count"] = Convert.ToInt64(await db.ExecuteScalarAsync("SELECT COUNT(*) FROM `node` WHERE `mime` LIKE '%/%'")),
                        ["size"] = Convert.ToInt64(await db.ExecuteScalarAsync("SELECT SUM(size) FROM `node` WHERE `mime` LIKE '%/%'")),
                    };
                    json["blobs"] = new JsonObject
                    {
                        ["count"] = Convert.ToInt64(await db.ExecuteScalarAsync("SELECT COUNT(*) FROM `blob`")),
                        ["size"] = Convert.ToInt64(await db.ExecuteScalarAsync("SELECT SUM(size) FROM `blob`")),
                        ["dataCount"] = Convert.ToInt64(await db.ExecuteScalarAsync("SELECT COUNT(*) FROM `blob` WHERE `parent_id` IS NULL")),
                        ["dataSize"] = Convert.ToInt64(await db.ExecuteScalarAsync("SELECT SUM(size) FROM `blob` WHERE `parent_id` IS NULL")),
                        ["metaCount"] = Convert.ToInt64(await db.ExecuteScalarAsync("SELECT COUNT(*) FROM `blob` WHERE `parent_id` IS NOT NULL")),
                        ["metaSize"] = Convert.ToInt64(await db.ExecuteScalarAsync("SELECT SUM(size) FROM `blob` WHERE `parent_id` IS NOT NULL")),
                        ["duplicateCount"] = duplicateBlobs,
                        ["duplicateLostSize"] = lostSize,
                    };
                }
                c.Response.StatusCode = 200;
                c.Response.Content = json;
            };

            Get["/tempFile/{id}"] = (p, c) =>
            {
                string id = (string)p["id"];
                TempFile file = null;
                lock (tempFilesLock)
                {
                    if (tempFiles.ContainsKey(id))
                        file = tempFiles[id];
                }
                if (file != null)
                {
                    c.Response.StatusCode = 200;
                    c.Response.Headers["content-type"] = file.Mime;
                    c.Response.Content = file.Stream;
                }
            };

            GetAsync["/roots"] = async (p, c) =>
            {
                using (DB db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                    var roots = await GetRootsAsync(context);
                    var jsonArray = new JsonArray();
                    foreach (var r in roots)
                        jsonArray.Add(await r.ToJsonAsync(true));
                    c.Response.Content = jsonArray;
                    await db.CommitAsync();
                }
                c.Response.StatusCode = 200;
            };


            GetAsync["/nodes"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                bool expand = true;
                if (c.Request.QueryString.ContainsKey("expand"))
                    expand = Convert.ToBoolean(c.Request.QueryString["expand"]);
                using (DB db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                    var result = await Model.SearchAsync<Node>(db, c);
                    c.Response.Content = result;
                }
                c.Response.StatusCode = 200;
            };

            GetAsync["/structures"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                var result = new ModelList<DocStructure>();
                ModelList<Node> nodes;
                using (DB db = await DB.CreateAsync(dbUrl, false))
                {
                    nodes = await db.SelectAsync<Node>($"SELECT * FROM `node` WHERE {nameof(Node.parent_id)} IS NULL AND {nameof(Node.etablissement_uai)} IS NOT NULL");
                }
                var structureIds = nodes.Select(s => s.etablissement_uai);
                ModelList<Directory.Structure> structures;
                using (DB db = await DB.CreateAsync(directoryDbUrl, false))
                {
                    structures = await db.SelectAsync<Directory.Structure>($"SELECT * FROM `structure` WHERE {DB.InFilter(nameof(Directory.Structure.id), structureIds)}");
                }
                var structuresDict = new Dictionary<string, Directory.Structure>();
                foreach (var structure in structures)
                {
                    structuresDict[structure.id] = structure;
                }
                foreach (var node in nodes)
                {
                    var docStruct = new DocStructure
                    {
                        structure_id = node.etablissement_uai,
                        node_id = node.id,
                        size = node.size,
                        mtime = node.mtime,
                        is_deleted = true
                    };
                    if (structuresDict.ContainsKey(node.etablissement_uai))
                    {
                        var structure = structuresDict[node.etablissement_uai];
                        docStruct.name = structure.name;
                        docStruct.is_deleted = false;
                    }
                    result.Add(docStruct);
                }
                c.Response.StatusCode = 200;
                c.Response.Content = result.Search(c).ToJson(c);
            };

            GetAsync["/users"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                var result = new ModelList<DocUser>();
                ModelList<Node> nodes;
                using (DB db = await DB.CreateAsync(dbUrl, false))
                {
                    nodes = await db.SelectAsync<Node>($"SELECT * FROM `node` WHERE {nameof(Node.parent_id)} IS NULL AND {nameof(Node.cartable_uid)} IS NOT NULL");
                }
                var userIds = nodes.Select(s => s.cartable_uid);
                var usersDict = new Dictionary<string, Directory.User>();
                using (DB db = await DB.CreateAsync(directoryDbUrl, false))
                {
                    var tmpIds = new List<string>();
                    var idsCount = 0;

                    Func<Task> loadUsers = async delegate
                    {
                        var sql = $"SELECT * FROM `user` WHERE {DB.InFilter(nameof(Directory.User.id), tmpIds)} ORDER BY `{nameof(Directory.User.id)}`";
                        foreach (var user in await db.SelectAsync<Directory.User>(sql))
                            usersDict[user.id] = user;
                    };
                    // batch request by a group of 200 because it is too slowest for bigger id group
                    foreach (var id in userIds)
                    {
                        tmpIds.Add(id);
                        idsCount++;
                        if (idsCount > 200)
                        {
                            await loadUsers();
                            tmpIds.Clear();
                            idsCount = 0;
                        }
                    }
                    if (idsCount > 0)
                        await loadUsers();
                }
                foreach (var node in nodes)
                {
                    var docUser = new DocUser
                    {
                        user_id = node.cartable_uid,
                        node_id = node.id,
                        firstname = node.owner_firstname,
                        lastname = node.owner_lastname,
                        size = node.size,
                        mtime = node.mtime,
                        is_deleted = true
                    };
                    if (usersDict.ContainsKey(node.cartable_uid))
                    {
                        var user = usersDict[node.cartable_uid];
                        docUser.firstname = user.firstname;
                        docUser.lastname = user.lastname;
                        docUser.is_deleted = false;
                    }
                    result.Add(docUser);
                }
                c.Response.StatusCode = 200;
                c.Response.Content = result.Search(c).ToJson(c);

            };

            GetAsync["/groups"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                var result = new ModelList<DocGroup>();
                ModelList<Node> nodes;
                using (DB db = await DB.CreateAsync(dbUrl, false))
                {
                    nodes = await db.SelectAsync<Node>($"SELECT * FROM `node` WHERE {nameof(Node.parent_id)} IS NULL AND {nameof(Node.groupe_libre_id)} IS NOT NULL");
                }
                var groupIds = nodes.Select(s => s.groupe_libre_id);
                ModelList<Directory.Group> groups;
                using (DB db = await DB.CreateAsync(directoryDbUrl, false))
                {
                    groups = await db.SelectAsync<Directory.Group>($"SELECT * FROM `group` WHERE {DB.InFilter(nameof(Directory.Group.id), groupIds)}");
                }
                var groupsDict = new Dictionary<long, Directory.Group>();
                foreach (var group in groups)
                {
                    groupsDict[group.id] = group;
                }
                foreach (var node in nodes)
                {
                    var docGroup = new DocGroup
                    {
                        group_id = (int)node.groupe_libre_id,
                        node_id = node.id,
                        name = node.name,
                        size = node.size,
                        mtime = node.mtime,
                        is_deleted = true
                    };
                    if (groupsDict.ContainsKey((int)node.groupe_libre_id))
                    {
                        var group = groupsDict[(int)node.groupe_libre_id];
                        docGroup.name = group.name;
                        docGroup.is_deleted = false;
                    }
                    result.Add(docGroup);
                }
                c.Response.StatusCode = 200;
                c.Response.Content = result.Search(c).ToJson(c);
            };

            GetAsync["/"] = async (p, c) =>
            {
                // NAIVE ALGO. IMPROVE THIS
                using (DB db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                    var filter = c.ToFilter();
                    var expand = c.Request.QueryString.ContainsKey("expand") && bool.Parse(c.Request.QueryString["expand"]);
                    filter.Remove("expand");
                    var limit = 100;
                    if (c.Request.QueryString.ContainsKey("limit"))
                        limit = Math.Min(500, int.Parse(c.Request.QueryString["limit"]));
                    filter.Remove("limit");
                    var json = new JsonArray();

                    if (c.Request.QueryStringArray.ContainsKey("id"))
                    {
                        filter.Remove("id");
                        var nodes = await db.SelectExpandAsync<Node>($"SELECT * FROM `node` WHERE {DB.InFilter("id", c.Request.QueryStringArray["id"])}", new object[0]);
                        foreach (var node in nodes)
                        {
                            if (node.IsFilterMatch(filter))
                            {
                                var item = context.GetByNode(node);
                                var right = await item.RightsAsync();
                                if (right.Read)
                                    json.Add(await item.ToJsonAsync(expand));
                            }
                        }
                    }
                    else
                    {
                        IEnumerable<Item> roots;
                        if (c.Request.QueryStringArray.ContainsKey("roots"))
                        {
                            roots = new List<Item>();
                            foreach (var rootId in c.Request.QueryStringArray["roots"])
                            {
                                var root = await context.GetByIdAsync(long.Parse(rootId));
                                if (root != null)
                                {
                                    var rights = await root.RightsAsync();
                                    if (rights.Read)
                                        (roots as List<Item>).Add(root);
                                }
                            }
                        }
                        else
                            roots = await GetRootsAsync(context);
                        var result = new List<Item>();

                        filter.Remove("roots");
                        await SearchItemsAsync(context, roots, filter, result, limit);
                        foreach (var item in result)
                            json.Add(await item.ToJsonAsync(expand));
                    }
                    c.Response.StatusCode = 200;
                    c.Response.Content = json;
                }
            };

            GetAsync["/downloadasarchive"] = async (p, c) =>
            {
                if (c.Request.QueryStringArray.ContainsKey("id"))
                {
                    var ids = c.Request.QueryStringArray["id"].Select(a => long.Parse(a)).ToArray();
                    var name = "archive.zip";
                    if (c.Request.QueryString.ContainsKey("name"))
                        name = c.Request.QueryString["name"].Replace('"', ' ');
                    c.Response.StatusCode = 200;
                    c.Response.Headers["content-type"] = "application/zip";
                    c.Response.Headers["content-disposition"] = $"attachment; filename=\"{name}\"";
                    using (var db = await DB.CreateAsync(dbUrl))
                    {
                        var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                        c.Response.Content = await ArchiveZip.DownloadAsArchiveAsync(context, ids);
                    }
                }
            };

            GetAsync["/{id}"] = async (p, c) =>
            {
                bool expand = true;
                if (c.Request.QueryString.ContainsKey("expand"))
                    expand = Convert.ToBoolean(c.Request.QueryString["expand"]);
                var id = long.Parse((string)p["id"]);

                using (DB db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                    var item = await context.GetByIdAsync(id);
                    if (item != null)
                    {
                        if (context.user.IsUser)
                        {
                            // a user can see the meta data if he has read right on
                            // the parent folder or read right on the item if there is no parent
                            var parent = await item.GetParentAsync();
                            if (parent != null)
                            {
                                if (!(await parent.RightsAsync()).Read)
                                    throw new WebException(403, "User dont have read right");
                            }
                            else if (!(await item.RightsAsync()).Read)
                                throw new WebException(403, "User dont have read right");
                        }

                        c.Response.StatusCode = 200;
                        c.Response.Content = await item.ToJsonAsync(expand);
                    }
                    await db.CommitAsync();
                }
            };

            DeleteAsync["/{id}"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                using (var db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                    var item = await context.GetByIdAsync(id);
                    if (item != null)
                        await item.DeleteAsync();
                    await db.CommitAsync();
                }
                c.Response.StatusCode = 200;
                c.Response.Content = "";
            };

            DeleteAsync["/"] = async (p, c) =>
            {
                var json = await c.Request.ReadAsJsonAsync();
                if (json is JsonArray)
                {
                    using (var db = await DB.CreateAsync(dbUrl, true))
                    {
                        var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                        foreach (var idValue in json as JsonArray)
                        {
                            var id = long.Parse(idValue);
                            var item = await context.GetByIdAsync(id);
                            if (item != null)
                                await item.DeleteAsync();
                        }
                        await db.CommitAsync();
                    }
                    c.Response.StatusCode = 200;
                    c.Response.Content = "";
                }
            };

            PostAsync["/"] = async (p, c) =>
            {
                var fileDefinition = await Blobs.GetFileDefinitionAsync<Node>(c);
                await nodeChangeLock.WaitAsync();
                try
                {
                    try
                    {
                        using (var db = await DB.CreateAsync(dbUrl, true))
                        {
                            var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                            var item = await Item.CreateAsync(context, fileDefinition);
                            await db.CommitAsync();
                            c.Response.StatusCode = 200;
                            c.Response.Content = await item.ToJsonAsync(true);
                        }
                    }
                    // catch MySQL duplicate name exception
                    catch (MySql.Data.MySqlClient.MySqlException e)
                    {
                        if (e.Number == 1062)
                        {
                            c.Response.StatusCode = 400;
                            c.Response.Content = new JsonObject { ["error"] = "Duplicate name", ["code"] = 1 };
                        }
                        else
                            throw;
                    }
                }
                finally
                {
                    nodeChangeLock.Release();
                }
            };

            PutAsync["/{id}"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                var fileDefinition = await Blobs.GetFileDefinitionAsync<Node>(c);
                await nodeChangeLock.WaitAsync();
                try
                {
                    try
                    {
                        using (DB db = await DB.CreateAsync(dbUrl, true))
                        {
                            var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                            var item = await context.GetByIdAsync(id);
                            await item.ChangeAsync(fileDefinition);
                            await db.CommitAsync();
                            if (item != null)
                            {
                                c.Response.StatusCode = 200;
                                c.Response.Content = await item.ToJsonAsync(true);
                            }
                        }
                    }
                    // catch MySQL duplicate name exception
                    catch (MySql.Data.MySqlClient.MySqlException e)
                    {
                        if (e.Number == 1062)
                        {
                            c.Response.StatusCode = 400;
                            c.Response.Content = new JsonObject { ["error"] = "Duplicate name", ["code"] = 1 };
                        }
                        else
                            throw;
                    }
                }
                finally
                {
                    nodeChangeLock.Release();
                }
            };

            GetAsync["/{id}/onlyoffice"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);

                // if user is not authenticated, ask for authentication
                var authUser = await c.GetAuthenticatedUserAsync();
                if (authUser == null)
                {
                    c.Response.StatusCode = 302;
                    c.Response.Headers["location"] = "/sso/login?ticket=false&service=" + HttpUtility.UrlEncode(c.SelfURL());
                    return;
                }

                OnlyOffice item;
                ItemRight rights = null;
                using (DB db = await DB.CreateAsync(dbUrl))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                    item = await context.GetByIdAsync(id) as OnlyOffice;
                    if (item != null)
                        rights = await item.RightsAsync();
                }
                if (item != null)
                {
                    var mode = OnlyOfficeMode.Desktop;
                    if (c.Request.Headers.ContainsKey("user-agent") && Regex.IsMatch(c.Request.Headers["user-agent"], "(Android|iPhone|iPad)"))
                        mode = OnlyOfficeMode.Mobile;

                    OnlyOfficeDocumentType documentType = OnlyOfficeDocumentType.text;
                    OnlyOfficeFileType fileType = OnlyOfficeFileType.docx;
                    OnlyOffice.NodeToFileType(item.node, out documentType, out fileType);

                    if (!rights.Read)
                        throw new WebException(403, "Rights needed");

                    var session = await CreateOnlyOfficeSessionAsync(item, rights.Write);
                    c.Response.StatusCode = 200;
                    c.Response.Headers["content-type"] = "text/html; charset=utf-8";
                    c.Response.Content = new OnlyOfficeView
                    {
                        mode = mode,
                        documentType = documentType,
                        fileType = fileType,
                        node = item.node,
                        user = authUser.user,
                        edit = rights.Write,
                        downloadUrl = $"{c.SelfURL()}/file?session={session.id}",
                        callbackUrl = $"{c.SelfURL()}?session={session.id}"
                    }.TransformText();
                }
            };

            GetAsync["/{id}/onlyoffice/file"] = async (p, c) =>
            {
                if (!c.Request.QueryString.ContainsKey("session"))
                    throw new WebException(403, "Insufficient rights");

                var session = await GetOnlyOfficeSessionAsync(c.Request.QueryString["session"]);
                if (session == null)
                    throw new WebException(403, "Invalid session");

                var id = long.Parse((string)p["id"]);
                if (id != session.node_id)
                    throw new WebException(403, "Invalid session node");

                using (DB db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                    var item = await context.GetByIdAsync(id);
                    if (item != null)
                    {
                        c.Response.StatusCode = 200;
                        c.Response.SupportRanges = true;
                        c.Response.Headers["content-type"] = item.node.mime;
                        c.Response.Content = await item.GetContentAsync();
                    }
                    await db.CommitAsync();
                }
            };

            PostAsync["/{id}/onlyoffice"] = async (p, c) =>
            {
                if (!c.Request.QueryString.ContainsKey("session"))
                    throw new WebException(403, "Insufficient rights");

                var session = await GetOnlyOfficeSessionAsync(c.Request.QueryString["session"]);
                if (session == null)
                    throw new WebException(403, "Invalid session");

                var id = long.Parse((string)p["id"]);
                if (id != session.node_id)
                    throw new WebException(403, "Invalid session node");

                var json = await c.Request.ReadAsJsonAsync();
                // document close with no changes
                if (json.ContainsKey("status") && json["status"] == 4)
                {
                    await DeleteOnlyOfficeSessionForNodeAsync(session.node_id);
                }
                // final save or intermediate forcesave
                else if (json.ContainsKey("status") && json.ContainsKey("url") && (json["status"] == 2 || json["status"] == 6))
                {
                    // if save is need check is the session has the right
                    if (!session.write)
                        throw new WebException(403, "Insufficient rights");

                    Node node = null;
                    using (var db = await DB.CreateAsync(dbUrl, true))
                    {
                        node = new Node { id = id };
                        if (!await node.LoadAsync(db))
                            return;
                    }

                    HttpClient client;
                    HttpClientResponse response;
                    var uri = new Uri(json["url"]);
                    client = await HttpClient.CreateAsync(uri);
                    try
                    {
                        HttpClientRequest request = new HttpClientRequest();
                        request.Method = "GET";
                        request.Path = uri.PathAndQuery;
                        await client.SendRequestAsync(request);
                        response = await client.GetResponseAsync();
                        if (response.StatusCode != 200)
                            return;
                        // check if convert is needed
                        if (node.mime != response.Headers["content-type"])
                        {
                            OnlyOfficeFileType fromFileType;
                            OnlyOfficeDocumentType fromDocumentType;
                            OnlyOffice.MimeToFileType(response.Headers["content-type"], out fromDocumentType, out fromFileType);
                            OnlyOfficeFileType toFileType;
                            OnlyOfficeDocumentType toDocumentType;
                            OnlyOffice.MimeToFileType(node.mime, out toDocumentType, out toFileType);
                            var convertUrl = await OnlyOffice.ConvertAsync(c, json["url"], fromFileType, toFileType);
                            if (convertUrl == null)
                                return;
                            client.Dispose();
                            uri = new Uri(convertUrl);
                            client = await HttpClient.CreateAsync(uri);
                            request = new HttpClientRequest();
                            request.Method = "GET";
                            request.Path = uri.PathAndQuery;
                            await client.SendRequestAsync(request);
                            response = await client.GetResponseAsync();
                            if (response.StatusCode != 200)
                                return;
                        }

                        // update the file
                        var fileDefinition = new FileDefinition<Node>
                        {
                            Name = "content",
                            Mimetype = response.Headers["content-type"],
                            Stream = response.InputStream
                        };

                        OnlyOffice item = null;
                        using (DB db = await DB.CreateAsync(dbUrl, true))
                        {
                            var authUser = new AuthenticatedUser { application = new Directory.Application() };
                            var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = authUser, directoryDbUrl = directoryDbUrl, httpContext = c };
                            item = await context.GetByIdAsync(id) as OnlyOffice;
                            if (item != null)
                                await item.ChangeAsync(fileDefinition);
                            await db.CommitAsync();
                        }
                        // signal that the document content was updated
                        lock (onlyOfficeCallbacksLock)
                        {
                            if (onlyOfficeCallbacks.ContainsKey(id))
                            {
                                var list = onlyOfficeCallbacks[id];
                                onlyOfficeCallbacks.Remove(id);
                                foreach (var cb in list)
                                    cb.SetResult(item);
                            }
                        }
                    }
                    finally
                    {
                        client.Dispose();
                    }
                    // if it was the last save, delete sessions
                    if (json["status"] == 2)
                        await DeleteOnlyOfficeSessionForNodeAsync(session.node_id);
                }
                c.Response.StatusCode = 200;
                c.Response.Content = new JsonObject { ["error"] = 0 };
            };

            PostAsync["/{id}/copy"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                var expand = c.Request.QueryString.ContainsKey("expand") ? bool.Parse(c.Request.QueryString["expand"]) : true;
                var json = await c.Request.ReadAsJsonAsync();
                var dstNode = new Node
                {
                    rev = 0,
                    ctime = DateTime.Now,
                    mtime = DateTime.Now
                };
                if (json.ContainsKey("name"))
                    dstNode.name = (string)json["name"];
                if (!json.ContainsKey("parent_id"))
                    throw new WebException(400, "A target parent_id is needed for a copy");
                dstNode.parent_id = long.Parse(json["parent_id"]);

                using (var db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };

                    var parentItem = await context.GetByIdAsync((long)dstNode.parent_id);
                    var item = await context.GetByIdAsync(id);
                    if (item != null && parentItem != null)
                    {
                        // check file read right
                        var rights = await item.RightsAsync();
                        if (!rights.Read)
                            throw new WebException(403, "Insufficient rights");
                        // check destination write rights 
                        var parentRights = await parentItem.RightsAsync();
                        if (!parentRights.Write)
                            throw new WebException(403, "Insufficient rights");
                        // check if destination folder is not the node or its children
                        if (parentItem.node.id == item.node.id)
                            throw new WebException(400, "Cant copy a node inside itself");
                        var parents = await parentItem.GetParentsAsync();
                        if (parents.Any((pI) => pI.node.id == item.node.id))
                            throw new WebException(400, "Cant copy a node inside subfolders");

                        var fileDefinition = new FileDefinition<Node>
                        {
                            Define = dstNode,
                            Mimetype = item.node.mime,
                            Size = item.node.size,
                            Name = item.node.name,
                            Stream = await item.GetContentAsync()
                        };

                        var resItem = await Item.CreateAsync(context, fileDefinition);
                        c.Response.StatusCode = 200;
                        c.Response.Content = await resItem.ToJsonAsync(expand);
                    }
                    await db.CommitAsync();
                }
            };

            PutAsync["/{id}/copy/{src}"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                var src = long.Parse((string)p["src"]);
                var expand = c.Request.QueryString.ContainsKey("expand") ? bool.Parse(c.Request.QueryString["expand"]) : true;

                using (var db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                    var srcItem = await context.GetByIdAsync(src);
                    var dstItem = await context.GetByIdAsync(id);

                    if (srcItem != null && dstItem != null)
                    {
                        var srcRights = await srcItem.RightsAsync();
                        var dstRights = await dstItem.RightsAsync();
                        if (!srcRights.Read || !dstRights.Write)
                            throw new WebException(403, "Insufficient rights");

                        var fileDefinition = new FileDefinition<Node>
                        {
                            Stream = await srcItem.GetContentAsync(),
                            Mimetype = srcItem.node.mime
                        };

                        await dstItem.ChangeAsync(fileDefinition);
                        c.Response.StatusCode = 200;
                        c.Response.Content = await dstItem.ToJsonAsync(expand);
                    }
                    await db.CommitAsync();
                }
            };

            GetAsync["/{id}/content"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                long argRev = -1;
                if (c.Request.QueryString.ContainsKey("rev"))
                    argRev = Convert.ToInt64(c.Request.QueryString["rev"]);

                // if user is not authenticated, ask for authentication
                var authUser = await c.GetAuthenticatedUserAsync();
                if (authUser == null)
                {
                    c.Response.StatusCode = 302;
                    c.Response.Headers["location"] = "/sso/login?ticket=false&service=" + HttpUtility.UrlEncode(c.SelfURL());
                    return;
                }

                using (var db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = authUser, directoryDbUrl = directoryDbUrl, httpContext = c };
                    var item = await context.GetByIdAsync(id);
                    if (item != null)
                    {
                        var rights = await item.RightsAsync();
                        if (!rights.Read)
                        {
                            c.Response.StatusCode = 403;
                        }
                        else
                        {
                            if (item.node.rev != argRev)
                            {
                                string attachment = "";
                                if (c.Request.QueryString.ContainsKey("attachment"))
                                    attachment = "&attachment";
                                c.Response.StatusCode = 307;
                                c.Response.Headers["location"] = $"?rev={item.node.rev}{attachment}";
                            }
                            else
                            {
                                c.Response.StatusCode = 200;
                                c.Response.SupportRanges = true;
                                c.Response.Headers["content-type"] = item.node.mime;
                                if (!c.Request.QueryString.ContainsKey("nocache") && argRev == item.node.rev)
                                    c.Response.Headers["cache-control"] = "max-age=" + cacheDuration;
                                if (c.Request.QueryString.ContainsKey("attachment"))
                                    c.Response.Headers["content-disposition"] = $"attachment; filename=\"{item.node.name.Replace('"', ' ')}\"";
                                else
                                    c.Response.Headers["content-disposition"] = $"filename=\"{item.node.name.Replace('"', ' ')}\"";
                                c.Response.Content = await item.GetContentAsync();
                            }
                        }
                    }
                    await db.CommitAsync();
                }
            };

            GetAsync["/{id}/tmb"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                long argRev = -1;
                if (c.Request.QueryString.ContainsKey("rev"))
                    argRev = Convert.ToInt64(c.Request.QueryString["rev"]);

                using (var db = await DB.CreateAsync(dbUrl))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                    var item = await context.GetByIdAsync(id);

                    if (item != null)
                    {
                        var rights = await item.RightsAsync();
                        if (!rights.Read)
                        {
                            c.Response.StatusCode = 403;
                        }
                        else if (item.node.blob_id != null)
                        {
                            var blob = new Blob { id = item.node.blob_id };
                            if (await blob.LoadAsync(db, true))
                            {
                                var thumbnailBlob = blob.children.SingleOrDefault((child) => child.name == "thumbnail");
                                if (thumbnailBlob != null)
                                {
                                    if (argRev != item.node.rev)
                                    {
                                        c.Response.StatusCode = 307;
                                        c.Response.Headers["location"] = $"tmb?rev={item.node.rev}";
                                    }
                                    else
                                    {
                                        c.Response.StatusCode = 200;
                                        c.Response.SupportRanges = true;
                                        c.Response.Headers["content-type"] = thumbnailBlob.mimetype;
                                        if (!c.Request.QueryString.ContainsKey("nocache"))
                                            c.Response.Headers["cache-control"] = "max-age=" + cacheDuration;
                                        c.Response.Content = blobs.GetBlobStream(thumbnailBlob.id);
                                    }
                                }
                            }
                        }
                    }
                }
            };

            PostAsync["/archive"] = async (p, c) =>
            {
                var json = await c.Request.ReadAsJsonAsync();
                var files = (json["files"] as JsonArray).Select(f => (long)f).ToArray();
                var parentId = (long)json["parent_id"];
                var name = json["name"];

                using (var db = await DB.CreateAsync(dbUrl))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                    var item = await ArchiveZip.CreateAsync(context, files, parentId, name);
                    c.Response.StatusCode = 200;
                    c.Response.Content = await item.ToJsonAsync(false);
                }
            };

            PostAsync["/unarchive"] = async (p, c) =>
            {
                var json = await c.Request.ReadAsJsonAsync();
                var file = (long)json["file"];
                var parentId = (long)json["parent_id"];

                try
                {
                    using (var db = await DB.CreateAsync(dbUrl))
                    {
                        var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                        var items = await ArchiveZip.ExtractAsync(context, file, parentId);
                        c.Response.StatusCode = 200;
                        var jsonResult = new JsonArray();
                        foreach (var item in items)
                            jsonResult.Add(await item.ToJsonAsync(false));
                        c.Response.Content = jsonResult;
                    }
                }
                // catch MySQL duplicate name exception
                catch (MySql.Data.MySqlClient.MySqlException e)
                {
                    if (e.Number == 1062)
                    {
                        c.Response.StatusCode = 400;
                        c.Response.Content = new JsonObject { ["error"] = "Duplicate name", ["code"] = 1 };
                    }
                    else
                        throw;
                }
            };
        }

        internal async Task<string> OnlyOfficeGenerateThumbAsync(string fileUrl, OnlyOfficeFileType fileType)
        {
            string resFileUrl = null;

            var uri = new Uri(new Uri(globalSetup.server.publicUrl), "/onlyoffice/ConvertService.ashx");
            using (HttpClient client = await HttpClient.CreateAsync(uri))
            {
                HttpClientRequest request = new HttpClientRequest();
                request.Method = "POST";
                request.Path = uri.PathAndQuery;
                request.Headers["accept"] = "application/json";
                request.Content = new JsonObject
                {
                    ["async"] = false,
                    ["codePage"] = 65001,
                    ["filetype"] = fileType.ToString(),
                    ["key"] = Guid.NewGuid().ToString(),
                    ["outputtype"] = "jpg",
                    ["url"] = fileUrl,
                    ["thumbnail"] = new JsonObject
                    {
                        ["first"] = true,
                        ["aspect"] = 1,
                        ["width"] = 128,
                        ["height"] = 128,
                    }
                };
                await client.SendRequestAsync(request);
                HttpClientResponse response = await client.GetResponseAsync();
                if (response.StatusCode == 200)
                {
                    var json = await response.ReadAsJsonAsync();
                    if (json.ContainsKey("fileUrl"))
                        resFileUrl = json["fileUrl"];
                }
            }
            return resFileUrl;
        }


        async Task SearchItemsAsync(Context context, IEnumerable<Item> roots, Dictionary<string, List<string>> filter, List<Item> result, int limit)
        {
            var found = 0;
            foreach (var item in roots)
            {
                var rights = await item.RightsAsync();
                if (rights.Read)
                {
                    var count = await RecursiveSearchItemsAsync(context, item, filter, result, limit - found);
                    found += count;
                    if (found >= limit)
                        break;
                }
            }
        }

        async Task<int> RecursiveSearchItemsAsync(Context context, Item item, Dictionary<string, List<string>> filter, List<Item> result, int limit)
        {
            int found = 0;
            if (item is Folder)
            {
                var folder = item as Folder;
                var children = await folder.GetFilteredChildrenAsync();
                foreach (var child in children)
                {
                    var count = await RecursiveSearchItemsAsync(context, child, filter, result, limit - found);
                    found += count;
                    if (found >= limit)
                        break;
                }
            }
            if (item.node.IsFilterMatch(filter))
            {
                found++;
                result.Add(item);
            }
            return found;
        }

        public override async Task ProcessRequestAsync(HttpContext context)
        {
            var match = Regex.Match(context.Request.Path, @"^/zip/(\d+)/content/(.*)$");
            if (match.Success)
            {
                // if user is not authenticated, ask for authentication
                var authUser = await context.GetAuthenticatedUserAsync();
                if (authUser == null)
                {
                    context.Response.StatusCode = 302;
                    context.Response.Headers["location"] = "/sso/login?ticket=false&service=" + HttpUtility.UrlEncode(context.SelfURL());
                    return;
                }

                string fileId = match.Groups[1].Value;
                string remainPath = match.Groups[2].Value;

                using (var db = await DB.CreateAsync(dbUrl))
                {
                    var ctx = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await context.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = context };
                    var item = await ctx.GetByIdAsync(long.Parse(fileId));

                    if (item != null)
                    {
                        if (!(await item.RightsAsync()).Read)
                            throw new WebException(403, "Insufficient rights");

                        using (var zipFile = new ZipFile(await item.GetContentAsync()))
                        {
                            // special case, if the path is not set, look for an index file
                            if (remainPath == "")
                            {
                                foreach (ZipEntry e in zipFile)
                                {
                                    if ((e.Name == "index.html") ||
                                        (e.Name == "index.xhtml") ||
                                        (e.Name == "index.htm"))
                                    {
                                        context.Response.StatusCode = 302;
                                        context.Response.Headers["location"] = e.Name;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                var entry = zipFile.GetEntry(remainPath);
                                if (entry != null)
                                {
                                    var entryStream = new ZipEntryStream(zipFile, entry);
                                    var fileContent = new FileContent(entryStream);
                                    var splittedPath = remainPath.Split('/');
                                    fileContent.FileName = splittedPath[splittedPath.Length - 1];
                                    context.Response.SupportRanges = true;
                                    context.Response.Content = fileContent;
                                    await context.SendResponseAsync();
                                }
                            }
                        }
                    }
                }
            }

            // if the request is not handled
            if (!context.Response.Sent && context.Response.StatusCode == -1)
                await base.ProcessRequestAsync(context);
        }

        public void BuildThumbnail(string tempDir, string tempFile, string mimetype, out string thumbnailTempFile, out Blob thumbnailBlob)
        {
            Console.WriteLine($"BuildThumbnail FOR {tempFile}");

            thumbnailTempFile = null;
            thumbnailBlob = null;
            // build the thumbnail
            try
            {
                // use onlyoffice for thumbnail
                if (OnlyOfficeMimes.ContainsKey(mimetype))
                {
                    Console.WriteLine($"GENERATE THUMB WITH OFFICE FOR {tempFile}");
                    using (var fileStream = File.OpenRead(tempFile))
                    {
                        var file = new TempFile
                        {
                            Id = Guid.NewGuid() + "-" + StringExt.RandomString(),
                            Mime = mimetype,
                            Stream = fileStream
                        };
                        lock (tempFilesLock)
                        {
                            tempFiles[file.Id] = file;
                        }
                        try
                        {
                            var fileUri = new Uri(new Uri(globalSetup.server.publicUrl), $"/api/docs/tempFile/{file.Id}");
                            var task = OnlyOfficeGenerateThumbAsync(fileUri.AbsoluteUri, OnlyOfficeMimes[mimetype]);
                            task.Wait();
                            Console.WriteLine($"THUMB URL: {task.Result}");
                            // TODO: dowload the file

                            var thumbUri = new Uri(task.Result);
                            using (var client = HttpClient.Create(thumbUri))
                            {
                                HttpClientResponse response;
                                HttpClientRequest request = new HttpClientRequest();
                                request.Method = "GET";
                                request.Path = thumbUri.PathAndQuery;
                                client.SendRequest(request);
                                response = client.GetResponse();
                                if (response.StatusCode == 200)
                                {
                                    thumbnailTempFile = Path.Combine(tempDir, file.Id);
                                    using (var fileWriteStream = System.IO.File.OpenWrite(thumbnailTempFile))
                                    {
                                        response.InputStream.CopyTo(fileWriteStream);
                                    }
                                    thumbnailBlob = new Blob
                                    {
                                        id = Guid.NewGuid().ToString(),
                                        name = "thumbnail",
                                        mimetype = "image/jpeg"
                                    };
                                }
                            }
                        }
                        finally
                        {
                            lock (tempFilesLock)
                            {
                                tempFiles.Remove(file.Id);
                            }
                        }
                    }
                }
                else
                {
                    string previewMimetype;
                    string previewPath;
                    string error;

                    if (Erasme.Cloud.Preview.PreviewService.BuildPreview(
                        tempDir, tempFile, mimetype,
                        128, 128, out previewMimetype, out previewPath, out error))
                    {
                        thumbnailBlob = new Blob
                        {
                            id = Guid.NewGuid().ToString(),
                            name = "thumbnail",
                            mimetype = previewMimetype
                        };
                        thumbnailTempFile = previewPath;
                    }
                }
            }
            catch (Exception e)
            {
                thumbnailTempFile = null;
                thumbnailBlob = null;
                Console.WriteLine($"ThumbnailPlugin fails {e.ToString()}");
            }
        }

        public static void GenerateDefaultContent(FileDefinition<Node> fileDefinition)
        {
            if (fileDefinition.Define != null && fileDefinition.Define.mime != null && fileDefinition.Stream == null)
            {
                var mimetype = fileDefinition.Define.mime;
                // handle default content for Word, Excel and PowerPoint files
                if (mimetype == "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    string resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("sample.docx"));
                    fileDefinition.Stream = assembly.GetManifestResourceStream(resourceName);
                }
                else if (mimetype == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    string resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("sample.xlsx"));
                    fileDefinition.Stream = assembly.GetManifestResourceStream(resourceName);
                }
                else if (mimetype == "application/vnd.openxmlformats-officedocument.presentationml.presentation")
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    string resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("sample.pptx"));
                    fileDefinition.Stream = assembly.GetManifestResourceStream(resourceName);
                }
            }
        }

        async Task<IEnumerable<Item>> GetRootsAsync(Context context)
        {
            var roots = new List<Item>();
            if (!context.user.IsUser)
                return roots;
            // temporary allow user rights raise to allow creating the roots nodes
            var currentUser = context.user;
            var adminUser = new AuthenticatedUser() { application = new Directory.Application() };
            context.user = adminUser;
            try
            {
                // ensure user has a "cartable"
                var cartables = await context.db.SelectExpandAsync<Node>("SELECT * FROM `node` WHERE `cartable_uid`=?", new[] { currentUser.user.id });
                if (cartables.Count == 0)
                    roots.Add(await Cartable.CreateAsync(context, currentUser.user.id));
                else
                    roots.Add(Item.ByNode(context, cartables[0]));
                // ensure the structures exists
                var structuresIds = currentUser.user.profiles.Select((up) => up.structure_id).Distinct();
                var exitsStructures = (await context.db.SelectExpandAsync<Node>($"SELECT * FROM `node` WHERE {DB.InFilter("etablissement_uai", structuresIds)}", new object[] { }));
                var exitsStructuresIds = exitsStructures.Select(s => s.etablissement_uai);
                foreach (var structureId in structuresIds.Except(exitsStructuresIds))
                    roots.Add(await Structure.CreateAsync(context, structureId));
                roots.AddRange(exitsStructures.Select((s) => Item.ByNode(context, s)));
                // ensure the "groupe libre" exists
                var allGplIds = currentUser.user.groups.Select((g) => g.group_id).Concat(currentUser.user.children_groups.Select((g) => g.group_id));
                ModelList<Directory.Group> allGroups;
                using (var db = await DB.CreateAsync(context.directoryDbUrl))
                    allGroups = await db.SelectAsync<Directory.Group>($"SELECT * FROM `group` WHERE {DB.InFilter("id", allGplIds)} AND `type`='GPL'");
                foreach (var group in allGroups)
                    roots.Add(await GroupeLibre.GetOrCreateAsync(context, group.id, group.name));
            }
            finally
            {
                context.user = currentUser;
            }
            return roots;
        }

        public void AddPlugin(IFilePlugin plugin)
        {
            foreach (string mimetype in plugin.MimeTypes)
            {
                List<IFilePlugin> plugins;
                if (mimePlugins.ContainsKey(mimetype))
                    plugins = mimePlugins[mimetype];
                else
                {
                    plugins = new List<IFilePlugin>();
                    mimePlugins[mimetype] = plugins;
                }
                plugins.Add(plugin);
            }
            allPlugins.Add(plugin);
        }

        /// <summary>
        /// MIGRATION TOOLS FOR V3 TO V4
        /// </summary>
        public async Task Migration3To4Async(HttpContext c)
        {
            var limit = 1000;
            using (DB db = await DB.CreateAsync(dbUrl, true))
            {
                var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                var nodes = await db.SelectAsync<Node>("SELECT SQL_CALC_FOUND_ROWS * FROM `node` WHERE content IS NOT NULL LIMIT ?", limit);

                var folderCount = 0;
                var fileCount = 0;
                var fileContentFails = 0;

                foreach (var node in nodes)
                {
                    var item = Item.ByNode(context, node);
                    if (item is Folder)
                    {
                        await (new Node { id = node.id, content = null }).UpdateAsync(db);
                        folderCount++;
                    }
                    else
                    {
                        try
                        {
                            using (var stream = await item.GetContentAsync())
                            {
                                var fileDefinition = new FileDefinition<Node>();
                                fileDefinition.Stream = stream;
                                fileDefinition.Name = item.node.name;
                                fileDefinition.Mimetype = item.node.mime;
                                await item.ChangeAsync(fileDefinition);
                                // remove the content string
                                await (new Node { id = item.node.id, content = null }).UpdateAsync(db);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"File: {item.node.name} CONTENT NOT FOUND");
                            Console.WriteLine(e);
                            fileContentFails++;
                        }
                        fileCount++;
                    }
                }

                var total = await db.FoundRowsAsync();
                c.Response.Content = nodes;
                c.Response.Content = new JsonObject
                {
                    ["total"] = total,
                    ["limit"] = limit,
                    ["done"] = nodes.Count(),
                    ["folders"] = folderCount,
                    ["files"] = new JsonObject
                    {
                        ["total"] = fileCount,
                        ["contentfails"] = fileContentFails
                    }
                };
                await db.CommitAsync();
            }
            c.Response.StatusCode = 200;
        }

        public async Task GenerateMissingThumbnails(HttpContext c)
        {
            var limit = 1000;
            var supportedMimes = OnlyOfficeMimes.Keys.ToList();
            supportedMimes.Add("image/jpeg");
            supportedMimes.Add("image/png");
            supportedMimes.Add("image/gif");
            supportedMimes.Add("image/bmp");
            supportedMimes.Add("video/mp4");
            supportedMimes.Add("video/mpeg");
            supportedMimes.Add("video/ogg");
            supportedMimes.Add("video/webm");
            supportedMimes.Add("video/x-flv");
            supportedMimes.Add("video/3gpp");
            supportedMimes.Add("video/3gpp2");
            supportedMimes.Add("video/quicktime");
            supportedMimes.Add("video/x-msvideo");
            supportedMimes.Add("video/x-ms-wmv");

            using (DB db = await DB.CreateAsync(dbUrl, true))
            {
                var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                var nodes = await db.SelectAsync<Node>($"SELECT SQL_CALC_FOUND_ROWS * FROM `node` WHERE `blob_id` IS NOT NULL AND `has_tmb` = FALSE AND {DB.InFilter("mime", supportedMimes)} LIMIT ?", limit);
                var total = await db.FoundRowsAsync();
                var done = 0;
                var fails = 0;

                foreach (var node in nodes)
                {
                    var item = Item.ByNode(context, node);
                    try
                    {
                        if (await item.GenerateThumbnailAsync())
                            done++;
                        else
                            fails++;
                    }
                    catch
                    {
                        fails++;
                    }
                }
                await db.CommitAsync();

                c.Response.StatusCode = 200;
                c.Response.Content = new JsonObject
                {
                    ["total"] = total,
                    ["done"] = done,
                    ["fails"] = fails,
                    ["nodes"] = nodes.ToJson()
                };
            }
        }

        async Task<OnlyOfficeSession> CreateOnlyOfficeSessionAsync(OnlyOffice item, bool write)
        {
            OnlyOfficeSession session = null;
            using (DB db = await DB.CreateAsync(dbUrl))
            {
                string key = $"{item.node.id}REV{item.node.rev}";
                var sessions = await db.SelectAsync<OnlyOfficeSession>("SELECT * FROM `onlyoffice_session` WHERE `node_id` = ?", item.node.id);
                session = sessions.SingleOrDefault((s) => s.write == write);
                if (sessions.Count > 0)
                    key = sessions[0].key;
                if (session == null)
                {
                    session = new OnlyOfficeSession
                    {
                        id = StringExt.RandomString(32),
                        key = key,
                        node_id = item.node.id,
                        write = write,
                        ctime = DateTime.Now
                    };
                    await session.SaveAsync(db);
                }
            }
            return session;
        }

        async Task<OnlyOfficeSession> GetOnlyOfficeSessionAsync(string id)
        {
            OnlyOfficeSession session = null;
            using (DB db = await DB.CreateAsync(dbUrl))
            {
                // delete too old sessions
                await db.DeleteAsync("DELETE FROM `onlyoffice_session` WHERE (TIMESTAMPDIFF(SECOND, ctime, NOW()) >= ?)", 3600 * 12);

                session = new OnlyOfficeSession { id = id };
                if (!await session.LoadAsync(db))
                    session = null;
            }
            return session;
        }

        async Task DeleteOnlyOfficeSessionAsync(string id)
        {
            using (DB db = await DB.CreateAsync(dbUrl))
            {
                var session = new OnlyOfficeSession { id = id };
                await session.DeleteAsync(db);
            }
        }

        async Task DeleteOnlyOfficeSessionForNodeAsync(long nodeId)
        {
            using (DB db = await DB.CreateAsync(dbUrl))
                await db.DeleteAsync("DELETE FROM `onlyoffice_session` WHERE `node_id` = ?", nodeId);
        }
    }
}
