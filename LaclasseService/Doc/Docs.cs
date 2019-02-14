﻿// Log.cs
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
        public Session session = null;
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
        public long mtime { get { return GetField(nameof(mtime), 0L); } set { SetField(nameof(mtime), value); } }
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
        Setup globalSetup;
        DocSetup setup;
        Blobs blobs;
        Dictionary<string, List<IFilePlugin>> mimePlugins = new Dictionary<string, List<IFilePlugin>>();
        List<IFilePlugin> allPlugins = new List<IFilePlugin>();

        class TempFile
        {
            public string Id;
            public Stream Stream;
            public string Mime;
        }

        object tempFilesLock = new object();
        Dictionary<string, TempFile> tempFiles = new Dictionary<string, TempFile>();

        public static Dictionary<string, OnlyOfficeFileType> OnlyOfficeMimes = new Dictionary<string, OnlyOfficeFileType>
        {
            // text
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = OnlyOfficeFileType.docx,
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
                // not authentication needed for tempFile
                if (!c.Request.Path.StartsWith("/tempFile/", StringComparison.InvariantCulture))
                    await c.EnsureIsAuthenticatedAsync();
            };

            GetAsync["/migration3to4"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                await Migration3To4Async(c);
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
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
                    foreach (var id in json as JsonArray)
                    {
                        var item = await context.GetByIdAsync(id);
                        if (item != null)
                            await item.GenerateThumbnailAsync();
                    }
                    await db.CommitAsync();
                }
                c.Response.StatusCode = 200;
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
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
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
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
                    var result = await Model.SearchAsync<Node>(db, c);
                    c.Response.Content = result;
                }
                c.Response.StatusCode = 200;
            };

            GetAsync["/"] = async (p, c) =>
            {
                // NAIVE ALGO. IMPROVE THIS
                using (DB db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
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
                                Console.WriteLine($"Add root: {rootId} => {root}, read? {rights.Read}");
                                if (rights.Read)
                                    (roots as List<Item>).Add(root);
                            }
                        }
                    }
                    else
                        roots = await GetRootsAsync(context);
                    var result = new List<Item>();
                    var filter = c.ToFilter();
                    filter.Remove("roots");
                    filter.Remove("expand");
                    Console.WriteLine("Filter:");
                    Console.WriteLine(filter.Dump());
                    await SearchItemsAsync(context, roots, filter, result);
                    var json = new JsonArray();
                    foreach (var item in result)
                        json.Add(await item.ToJsonAsync(false));
                    c.Response.StatusCode = 200;
                    c.Response.Content = json;
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
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
                    var item = await context.GetByIdAsync(id);
                    await db.CommitAsync();
                    if (item != null)
                    {
                        c.Response.StatusCode = 200;
                        c.Response.Content = await item.ToJsonAsync(expand);
                    }
                }
            };

            DeleteAsync["/{id}"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                using (var db = await DB.CreateAsync(dbUrl, true))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
                    var item = await context.GetByIdAsync(id);
                    if (item != null)
                        await item.DeleteAsync();
                    await db.CommitAsync();
                }
                c.Response.StatusCode = 200;
                c.Response.Content = "";
            };

            PostAsync["/"] = async (p, c) =>
            {
                var fileDefinition = await Blobs.GetFileDefinitionAsync<Node>(c);
                try
                {
                    using (var db = await DB.CreateAsync(dbUrl, true))
                    {
                        var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
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
            };

            PutAsync["/{id}"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                var fileDefinition = await Blobs.GetFileDefinitionAsync<Node>(c);

                try
                {
                    using (DB db = await DB.CreateAsync(dbUrl, true))
                    {
                        var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
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
                    if (e.Number == 1602)
                    {
                        c.Response.StatusCode = 400;
                        c.Response.Content = new JsonObject { ["error"] = "Duplicate name", ["code"] = 1 };
                    }
                    else
                        throw;
                }
            };

            GetAsync["/{id}/onlyoffice"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                Item item;
                ItemRight rights = null;
                using (DB db = await DB.CreateAsync(dbUrl))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
                    item = await context.GetByIdAsync(id);
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
                    NodeToFileType(item.node, out documentType, out fileType);

                    if (!rights.Read)
                        throw new WebException(403, "Rights needed");

                    var session = await c.GetSessionAsync();
                    var authUser = await c.GetAuthenticatedUserAsync();
                    c.Response.StatusCode = 200;
                    c.Response.Headers["content-type"] = "text/html; charset=utf-8";
                    c.Response.Content = new OnlyOfficeView
                    {
                        mode = mode,
                        documentType = documentType,
                        fileType = fileType,
                        node = item.node,
                        session = session,
                        user = authUser.user,
                        edit = rights.Write,
                        downloadUrl = $"{Regex.Replace(c.SelfURL(), "onlyoffice$", "content")}?rev={item.node.rev}&session={session.id}",
                        callbackUrl = $"{c.SelfURL()}?session={session.id}"
                    }.TransformText();
                }
            };

            PostAsync["/{id}/onlyoffice"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                var json = await c.Request.ReadAsJsonAsync();
                if (json.ContainsKey("status") && json.ContainsKey("url") && json["status"] == 2)
                {
                    Console.WriteLine("SAVE NEEDED");
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
                            MimeToFileType(response.Headers["content-type"], out fromDocumentType, out fromFileType);
                            OnlyOfficeFileType toFileType;
                            OnlyOfficeDocumentType toDocumentType;
                            MimeToFileType(node.mime, out toDocumentType, out toFileType);
                            var convertUrl = await OnlyOfficeConvertAsync(c, json["url"], fromFileType, toFileType);
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
                        var fileDefinition = new FileDefinition
                        {
                            Name = "content",
                            Mimetype = response.Headers["content-type"],
                            Stream = response.InputStream
                        };

                        Blob blob; string tempFile;
                        (blob, tempFile) = await blobs.PrepareBlobAsync(fileDefinition);

                        string thumbnailTempFile = null;
                        Blob thumbnailBlob = null;

                        if (tempFile != null)
                            BuildThumbnail(tempDir, tempFile, blob.mimetype, out thumbnailTempFile, out thumbnailBlob);

                        using (var db = await DB.CreateAsync(dbUrl, true))
                        {
                            node = new Node { id = id };
                            if (await node.LoadAsync(db))
                            {
                                var oldBlobId = node.blob_id;
                                blob = await blobs.CreateBlobFromTempFileAsync(db, blob, tempFile);
                                node.blob_id = blob.id;
                                node.has_tmb = thumbnailTempFile != null;
                                node.rev++;
                                await node.UpdateAsync(db);

                                if (thumbnailTempFile != null)
                                {
                                    thumbnailBlob.parent_id = blob.id;
                                    thumbnailBlob = await blobs.CreateBlobFromTempFileAsync(db, thumbnailBlob, thumbnailTempFile);
                                    node.has_tmb = true;
                                }

                                // delete old blob if any
                                if (oldBlobId != null)
                                    await blobs.DeleteBlobAsync(db, oldBlobId);
                            }
                            await db.CommitAsync();
                        }

                    }
                    finally
                    {
                        client.Dispose();
                    }
                }
                c.Response.StatusCode = 200;
                c.Response.Content = new JsonObject { ["error"] = 0 };
            };

            PostAsync["/{id}/copy"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                var expand = c.Request.QueryString.ContainsKey("expand") ? bool.Parse(c.Request.QueryString["expand"]) : true;
                var fileDefinition = await Blobs.GetFileDefinitionAsync<Node>(c);

                var dstNode = new Node
                {
                    rev = 0,
                    mtime = (long)(DateTime.Now - DateTime.Parse("1970-01-01T00:00:00Z")).TotalSeconds
                };

                using (var db = await DB.CreateAsync(dbUrl, true))
                {
                    var node = new Node { id = id };
                    if (await node.LoadAsync(db, true))
                    {
                        dstNode.name = node.name;
                        dstNode.mime = node.mime;

                        Blob blob = null; string tempFile = null;
                        string thumbnailTempFile = null;
                        Blob thumbnailBlob = null;

                        if (node.blob_id != null)
                        {
                            fileDefinition.Stream = blobs.GetBlobStream(node.blob_id);
                            (blob, tempFile) = await blobs.PrepareBlobAsync(fileDefinition, node.blob);

                            if (tempFile != null)
                                BuildThumbnail(tempDir, tempFile, blob.mimetype, out thumbnailTempFile, out thumbnailBlob);
                        }

                        if (blob != null)
                        {
                            blob = await blobs.CreateBlobFromTempFileAsync(db, blob, tempFile);
                            dstNode.blob_id = blob.id;
                            dstNode.size = blob.size;
                        }
                        if (fileDefinition.Define != null)
                        {
                            var define = fileDefinition.Define;
                            if (define.Fields.ContainsKey(nameof(Node.name)))
                                dstNode.name = define.name;
                            if (define.Fields.ContainsKey(nameof(Node.parent_id)))
                                dstNode.parent_id = define.parent_id;
                        }

                        if (thumbnailTempFile != null)
                        {
                            thumbnailBlob.parent_id = blob.id;
                            thumbnailBlob = await blobs.CreateBlobFromTempFileAsync(db, thumbnailBlob, thumbnailTempFile);
                            dstNode.has_tmb = true;
                        }

                        await dstNode.SaveAsync(db);
                        await dstNode.LoadAsync(db, expand);

                        c.Response.StatusCode = 200;
                        c.Response.Content = dstNode;
                    }
                    await db.CommitAsync();
                }
            };

            PutAsync["/{id}/copy/{src}"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                var src = long.Parse((string)p["src"]);
                var expand = c.Request.QueryString.ContainsKey("expand") ? bool.Parse(c.Request.QueryString["expand"]) : true;
                var fileDefinition = new FileDefinition<Node>();

                using (var db = await DB.CreateAsync(dbUrl, true))
                {
                    var srcNode = new Node { id = src };
                    var dstNode = new Node { id = id };

                    if (await srcNode.LoadAsync(db, true) && await dstNode.LoadAsync(db, true))
                    {
                        Blob blob = null; string tempFile = null;
                        string thumbnailTempFile = null;
                        Blob thumbnailBlob = null;

                        if (srcNode.blob_id != null)
                        {
                            fileDefinition.Stream = blobs.GetBlobStream(srcNode.blob_id);
                            (blob, tempFile) = await blobs.PrepareBlobAsync(fileDefinition, srcNode.blob);

                            if (tempFile != null)
                                BuildThumbnail(tempDir, tempFile, blob.mimetype, out thumbnailTempFile, out thumbnailBlob);
                        }

                        string oldBlobId = null;
                        if (blob != null)
                        {
                            oldBlobId = dstNode.blob_id;
                            blob = await blobs.CreateBlobFromTempFileAsync(db, blob, tempFile);
                            dstNode.blob_id = blob.id;
                            dstNode.size = blob.size;
                            dstNode.rev++;
                        }
                        if (fileDefinition.Define != null)
                        {
                            var define = fileDefinition.Define;
                            if (define.Fields.ContainsKey(nameof(Node.name)))
                                dstNode.name = define.name;
                            if (define.Fields.ContainsKey(nameof(Node.parent_id)))
                                dstNode.parent_id = define.parent_id;
                        }

                        if (thumbnailTempFile != null)
                        {
                            thumbnailBlob.parent_id = blob.id;
                            thumbnailBlob = await blobs.CreateBlobFromTempFileAsync(db, thumbnailBlob, thumbnailTempFile);
                            dstNode.has_tmb = true;
                        }

                        await dstNode.UpdateAsync(db);
                        await dstNode.LoadAsync(db, expand);

                        // delete old blob if any
                        if (oldBlobId != null)
                            await blobs.DeleteBlobAsync(db, oldBlobId);

                        c.Response.StatusCode = 200;
                        c.Response.Content = dstNode;
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

                using (var db = await DB.CreateAsync(dbUrl))
                {
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
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
                                c.Response.Headers["location"] = $"content?rev={item.node.rev}{attachment}";
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
                    var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
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
        }

        //# Thumbnail avec OnlyOffice
        //        curl -X POST http://daniel.erasme.lan/onlyoffice/ConvertService.ashx
        // -H "Content-Type: application/json" -d
        // '{"async": false, "outputtype":"jpg","thumbnail":{"aspect": 0, "width": 64, "height": 64 },"filetype":"docx","url": "http://admin:masterPassword@daniel.erasme.lan/api/docs/3180/content"}'
        async Task<string> OnlyOfficeConvertAsync(HttpContext c, string fileUrl, OnlyOfficeFileType fileType, OnlyOfficeFileType destFileType)
        {
            string resFileUrl = null;
            var uri = new Uri(new Uri(c.SelfURL()), "/onlyoffice/ConvertService.ashx");
            using (HttpClient client = await HttpClient.CreateAsync(uri))
            {
                HttpClientRequest request = new HttpClientRequest();
                request.Method = "POST";
                request.Path = uri.PathAndQuery;
                request.Headers["accept"] = "application/json";
                request.Content = new JsonObject
                {
                    ["key"] = Guid.NewGuid().ToString(),
                    ["async"] = false,
                    ["codePage"] = 65001,
                    ["filetype"] = fileType.ToString(),
                    ["outputtype"] = destFileType.ToString(),
                    ["url"] = fileUrl
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

        async Task<string> OnlyOfficeGenerateThumbAsync(string fileUrl, OnlyOfficeFileType fileType)
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


        async Task SearchItemsAsync(Context context, IEnumerable<Item> roots, Dictionary<string, List<string>> filter, List<Item> result)
        {
            foreach (var item in roots)
            {
                var rights = await item.RightsAsync();
                if (rights.Read)
                    await RecursiveSearchItemsAsync(context, item, filter, result);
            }
        }

        async Task RecursiveSearchItemsAsync(Context context, Item item, Dictionary<string, List<string>> filter, List<Item> result)
        {
            if (item is Folder)
            {
                var folder = item as Folder;
                var children = await folder.GetFilteredChildrenAsync();
                foreach (var child in children)
                    await RecursiveSearchItemsAsync(context, child, filter, result);
            }
            if (item.node.IsFilterMatch(filter))
                result.Add(item);
        }

        public override async Task ProcessRequestAsync(HttpContext context)
        {
            var match = Regex.Match(context.Request.Path, @"^/zip/(\d+)/content/(.*)$");
            if (match.Success)
            {
                await context.EnsureIsAuthenticatedAsync();

                string fileId = match.Groups[1].Value;
                string remainPath = match.Groups[2].Value;

                //                string filePath = null;
                using (var db = await DB.CreateAsync(dbUrl))
                {
                    var ctx = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await context.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
                    var item = await ctx.GetByIdAsync(long.Parse(fileId));

                    if (item != null)
                    {
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

        static void NodeToFileType(Node node, out OnlyOfficeDocumentType documentType, out OnlyOfficeFileType fileType)
        {
            MimeToFileType(node.mime, out documentType, out fileType);
        }

        static void MimeToFileType(string mime, out OnlyOfficeDocumentType documentType, out OnlyOfficeFileType fileType)
        {
            documentType = OnlyOfficeDocumentType.text;
            fileType = OnlyOfficeFileType.docx;

            // text
            if (mime == "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.docx;
            }
            else if (mime == "application/msword")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.doc;
            }
            else if (mime == "application/epub+zip")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.epub;
            }
            else if (mime == "application/vnd.oasis.opendocument.text")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.odt;
            }
            else if (mime == "application/rtf")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.rtf;
            }
            else if (mime == "text/plain")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.txt;
            }
            else if (mime == "application/vnd.ms-xpsdocument")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.xps;
            }

            // spreadsheet
            else if (mime == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            {
                documentType = OnlyOfficeDocumentType.spreadsheet;
                fileType = OnlyOfficeFileType.xlsx;
            }
            else if (mime == "application/vnd.ms-excel")
            {
                documentType = OnlyOfficeDocumentType.spreadsheet;
                fileType = OnlyOfficeFileType.xls;
            }
            else if (mime == "application/vnd.oasis.opendocument.spreadsheet")
            {
                documentType = OnlyOfficeDocumentType.spreadsheet;
                fileType = OnlyOfficeFileType.ods;
            }
            else if (mime == "text/csv")
            {
                documentType = OnlyOfficeDocumentType.spreadsheet;
                fileType = OnlyOfficeFileType.csv;
            }

            // presentation
            else if (mime == "application/vnd.openxmlformats-officedocument.presentationml.presentation")
            {
                documentType = OnlyOfficeDocumentType.presentation;
                fileType = OnlyOfficeFileType.pptx;
            }
            else if (mime == "application/vnd.ms-powerpoint")
            {
                documentType = OnlyOfficeDocumentType.presentation;
                fileType = OnlyOfficeFileType.ppt;
            }
            else if (mime == "application/vnd.oasis.opendocument.presentation")
            {
                documentType = OnlyOfficeDocumentType.presentation;
                fileType = OnlyOfficeFileType.odp;
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
                var cartables = await context.db.SelectAsync<Node>("SELECT * FROM `node` WHERE `cartable_uid`=?", currentUser.user.id);
                if (cartables.Count == 0)
                    roots.Add(await Cartable.CreateAsync(context, currentUser.user.id));
                else
                    roots.Add(Item.ByNode(context, cartables[0]));
                // ensure the structures exists
                var structuresIds = currentUser.user.profiles.Select((up) => up.structure_id).Distinct();
                var exitsStructures = (await context.db.SelectAsync<Node>($"SELECT * FROM `node` WHERE {DB.InFilter("etablissement_uai", structuresIds)}"));
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
                var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
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
                var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = this, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl };
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
    }
}
