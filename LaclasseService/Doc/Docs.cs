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
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;
using ICSharpCode.SharpZipLib.Zip;

namespace Laclasse.Doc
{
	public partial class OnlyOfficeView
    {
		public Node node = null;
		public Session session = null;
		public Directory.User user = null;
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
		public string mime { get { return GetField<string>(nameof(mime),  null); } set { SetField(nameof(mime), value); } }
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
		public string owner_lastname { get { return GetField<string>(nameof(owner_lastname), null); } set { SetField(nameof(owner_firstname), value); } }
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
		public ModelList<Node> rights { get { return GetField<ModelList<Node>>(nameof(rights), null); } set { SetField(nameof(rights), value); } }

		// TODO: implement it to works
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
		Blobs blobs;      
		Dictionary<string,List<IFilePlugin>> mimePlugins = new Dictionary<string,List<IFilePlugin>>();
        List<IFilePlugin> allPlugins = new List<IFilePlugin>();

		public Docs(string dbUrl, string path, string tempDir, Blobs blobs, int cacheDuration)
		{
			this.dbUrl = dbUrl;
			this.path = path;
			this.tempDir = tempDir;
			this.blobs = blobs;
            
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/"] = async (p, c) =>
			{
				var authUser = await c.GetAuthenticatedUserAsync();
				var filterAuth = (new Node()).FilterAuthUser(authUser);
                using (DB db = await DB.CreateAsync(dbUrl))
                {
                    var result = await Model.SearchAsync<Node>(db, c, filterAuth);
                    foreach (var item in result.Data)
                        await item.EnsureRightAsync(c, Laclasse.Right.Read, null);
                    c.Response.Content = result.ToJson(c);
                }
                c.Response.StatusCode = 200;
			};

			GetAsync["/{id:int}"] = async (p, c) =>
			{
				bool expand = true;
                if (c.Request.QueryString.ContainsKey("expand"))
                    expand = Convert.ToBoolean(c.Request.QueryString["expand"]);
				Node item = new Node { id = (int)p["id"] };
                using (DB db = await DB.CreateAsync(dbUrl))
					if (!await item.LoadAsync(db, expand))
						item = null;
                if (item != null)
                {
                    await item.EnsureRightAsync(c, Laclasse.Right.Read, null);
                    c.Response.StatusCode = 200;
                    c.Response.Content = item;
                }
			};

			GetAsync["/{id:int}/onlyoffice"] = async (p, c) =>
            {
                Node item = new Node { id = (int)p["id"] };
                using (DB db = await DB.CreateAsync(dbUrl))
                    if (!await item.LoadAsync(db, true))
                        item = null;
                if (item != null)
                {
                    await item.EnsureRightAsync(c, Laclasse.Right.Read, null);
					var session = await c.GetSessionAsync();
					var authUser = await c.GetAuthenticatedUserAsync();
                    c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "text/html; charset=utf-8";
					c.Response.Content = new OnlyOfficeView { node = item, session = session, user = authUser.user }.TransformText();
                }
            };

			PostAsync["/{id:int}/onlyoffice"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json.ContainsKey("status") && json.ContainsKey("url") && json["status"] == 2)
				{
					Console.WriteLine("SAVE NEEDED");

					var uri = new Uri(json["url"]);
					using (HttpClient client = await HttpClient.CreateAsync(uri))
                    {
                        HttpClientRequest request = new HttpClientRequest();
                        request.Method = "GET";
						request.Path = uri.PathAndQuery;
                        client.SendRequest(request);
                        HttpClientResponse response = await client.GetResponseAsync();
						if (response.StatusCode == 200)
						{                     
							var fileDefinition = new FileDefinition {
                                Name = "content",
								Mimetype = response.Headers["content-type"],
								Stream = response.InputStream
							};

                            Blob blob; string tempFile;
                            (blob, tempFile) = await blobs.PrepareBlobAsync(fileDefinition);

							string thumbnailTempFile = null;
                            Blob thumbnailBlob = null;

                            if (tempFile != null)
                                BuildThumbnail(tempFile, blob.mimetype, out thumbnailTempFile, out thumbnailBlob);

                            using (var db = await DB.CreateAsync(dbUrl, true))
                            {
                                var node = new Node { id = (int)p["id"] };
                                if (await node.LoadAsync(db))
                                {
                                    var oldBlobId = node.blob_id;
                                    blob = await blobs.CreateBlobFromTempFileAsync(db, blob, tempFile);
                                    node.blob_id = blob.id;
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
                    }
				}

				c.Response.StatusCode = 200;
				c.Response.Content = new JsonObject { ["error"] = 0 };
			};

			PostAsync["/"] = async (p, c) =>
			{
				var expand = c.Request.QueryString.ContainsKey("expand") ? bool.Parse(c.Request.QueryString["expand"]) : true;
				var fileDefinition = await Blobs.GetFileDefinitionAsync<Node>(c);
				Blob blob; string tempFile;
                (blob, tempFile) = await blobs.PrepareBlobAsync(fileDefinition);

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
                
				var authUser = c.GetAuthenticatedUser();
				if (authUser.IsUser)
				{
					node.owner = authUser.user.id;
					node.owner_firstname = authUser.user.firstname;
					node.owner_lastname = authUser.user.lastname;
				}

				string thumbnailTempFile = null;
				Blob thumbnailBlob = null;

				if (tempFile != null)
					BuildThumbnail(tempFile, node.mime, out thumbnailTempFile, out thumbnailBlob);
            
				using (var db = await DB.CreateAsync(dbUrl))
                {
					if (tempFile != null)
					{
						blob = await blobs.CreateBlobFromTempFileAsync(db, blob, tempFile);
						node.blob_id = blob.id;
					}
					if (thumbnailTempFile != null)
					{
						thumbnailBlob.parent_id = blob.id;
						thumbnailBlob = await blobs.CreateBlobFromTempFileAsync(db, thumbnailBlob, thumbnailTempFile);
						node.has_tmb = true;
					}
					await node.SaveAsync(db, expand);
					await db.CommitAsync();
                }

				c.Response.StatusCode = 200;
				c.Response.Content = node;
			};

			PutAsync["/{id:int}"] = async (p, c) =>
            {
				var expand = c.Request.QueryString.ContainsKey("expand") ? bool.Parse(c.Request.QueryString["expand"]) : true;
				var fileDefinition = await Blobs.GetFileDefinitionAsync<Node>(c);
            
				Blob blob; string tempFile;
				(blob, tempFile) = await blobs.PrepareBlobAsync(fileDefinition);

				string thumbnailTempFile = null;
                Blob thumbnailBlob = null;

                if (tempFile != null)
                    BuildThumbnail(tempFile, blob.mimetype, out thumbnailTempFile, out thumbnailBlob);
            
				using (var db = await DB.CreateAsync(dbUrl, true))
                {
					var node = new Node { id = (int)p["id"] };
					if (await node.LoadAsync(db))
					{
						var oldBlobId = node.blob_id;
						blob = await blobs.CreateBlobFromTempFileAsync(db, blob, tempFile);
						node.blob_id = blob.id;
						node.rev++;
						await node.UpdateAsync(db);
						await node.LoadAsync(db, expand);

						if (thumbnailTempFile != null)
                        {
                            thumbnailBlob.parent_id = blob.id;
                            thumbnailBlob = await blobs.CreateBlobFromTempFileAsync(db, thumbnailBlob, thumbnailTempFile);
							node.has_tmb = true;
                        }

						// delete old blob if any
						if (oldBlobId != null)
							await blobs.DeleteBlobAsync(db, oldBlobId);

						c.Response.StatusCode = 200;
						c.Response.Content = node;
					}
					await db.CommitAsync();
				}
            };

			DeleteAsync["/{id:int}"] = async (p, c) =>
			{
				var node = new Node { id = (int)p["id"] };
				using (var db = await DB.CreateAsync(dbUrl, true))
                {
					if (await node.LoadAsync(db))
					{
						await node.DeleteAsync(db);
						if (node.blob_id != null)
							await blobs.DeleteBlobAsync(db, node.blob_id);                  
					}
					await db.CommitAsync();
				}            
				c.Response.StatusCode = 200;
				c.Response.Content = "";
			};

			GetAsync["/{id:int}/content"] = async (p, c) =>
            {
				long argRev = -1;
                if(c.Request.QueryString.ContainsKey("rev"))
                    argRev = Convert.ToInt64(c.Request.QueryString["rev"]);

				var node = new Node { id = (int)p["id"] };
                using (var db = await DB.CreateAsync(dbUrl))
                {               
					if (await node.LoadAsync(db, true))
                    {
						if (node.rev != argRev)
						{
							c.Response.StatusCode = 307;
							c.Response.Headers["location"] = $"content?rev={node.rev}";
						}
                        else if (node.content != null)
						{
							var fullPath = Path.GetFullPath(ContentToPath(path, node.content));
							// check if full path is in the base directory
							if (!fullPath.StartsWith(path, StringComparison.InvariantCulture))
							{
								c.Response.StatusCode = 403;
								c.Response.Content = new StringContent("Invalid file path\r\n");
								return;
							}
							if (File.Exists(fullPath))
							{
								c.Response.SupportRanges = true;
								if(!c.Request.QueryString.ContainsKey("nocache"))
                                    c.Response.Headers["cache-control"] = "max-age=" + cacheDuration;
								c.Response.Content = new FileContent(fullPath);
							}
						}
						else if (node.blob_id != null)
						{
                            c.Response.Headers["content-type"] = node.mime;
                            c.Response.StatusCode = 200;
                            c.Response.SupportRanges = true;
							if(!c.Request.QueryString.ContainsKey("nocache"))
                                c.Response.Headers["cache-control"] = "max-age=" + cacheDuration;
							c.Response.Content = blobs.GetBlobStream(node.blob_id);


						}
                        else
						{
                            c.Response.Headers["content-type"] = node.mime;
                            c.Response.StatusCode = 200;
                            c.Response.SupportRanges = true;
							if(!c.Request.QueryString.ContainsKey("nocache"))
                                c.Response.Headers["cache-control"] = "max-age=" + cacheDuration;
                            c.Response.Content = "";
                        }
                        
                    }
                }
				if (c.Response.StatusCode != -1 && argRev == node.rev)
				{
                    if(!c.Request.QueryString.ContainsKey("nocache"))
                        c.Response.Headers["cache-control"] = "max-age=" + cacheDuration;
				}
            };

			GetAsync["/{id:int}/tmb"] = async (p, c) =>
            {            
				long argRev = -1;
                if(c.Request.QueryString.ContainsKey("rev"))
                    argRev = Convert.ToInt64(c.Request.QueryString["rev"]);

				var node = new Node { id = (int)p["id"] };
                using (var db = await DB.CreateAsync(dbUrl))
                {
					if (await node.LoadAsync(db, true))
					{
						if (node.blob_id != null)
						{
							var blob = new Blob { id = node.blob_id };
							if (await blob.LoadAsync(db, true))
							{
								var thumbnailBlob = blob.children.SingleOrDefault((child) => child.name == "thumbnail");
								if (thumbnailBlob != null)
								{
									if (argRev != node.rev)
									{
										c.Response.StatusCode = 307;
										c.Response.Headers["location"] = $"tmb?rev={node.rev}";
									}
                                    else
									{                              
										c.Response.StatusCode = 200;
										c.Response.SupportRanges = true;
										c.Response.Headers["content-type"] = thumbnailBlob.mimetype;
										if(!c.Request.QueryString.ContainsKey("nocache"))
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

		public override async Task ProcessRequestAsync(HttpContext context)
		{
			await context.EnsureIsAuthenticatedAsync();
			 
			var match = Regex.Match(context.Request.Path, @"^/zip/(\d+)/content/(.*)$");
			if (match.Success)
			{
				string fileId = match.Groups[1].Value;
				string remainPath = match.Groups[2].Value;

				string filePath = null;
				using (var db = await DB.CreateAsync(dbUrl))
				{
					var node = await db.SelectRowAsync<Node>(long.Parse(fileId), false);
					if (node != null)
					{
						filePath = Path.GetFullPath(ContentToPath(path, node.content));
						// check if full path is in the base directory
						if (!filePath.StartsWith(path, StringComparison.InvariantCulture))
							filePath = null;
					}
				}

				if (filePath != null && !File.Exists(filePath))
					filePath = null;

				if (filePath != null)
				{
					using (var zipFile = new ZipFile(filePath))
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

			// if the request is not handled
			if (!context.Response.Sent && context.Response.StatusCode == -1)
				await base.ProcessRequestAsync(context);
		}

		static string ContentToPath(string basePath, string content)
		{
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


		void BuildThumbnail(string tempFile, string mimetype, out string thumbnailTempFile, out Blob thumbnailBlob)
		{
			thumbnailTempFile = null;
			thumbnailBlob = null;
			// build the thumbnail
			try
			{
				string previewMimetype;
				string previewPath;
				string error;

				if (Erasme.Cloud.Preview.PreviewService.BuildPreview(
					tempDir, tempFile, mimetype,
					128, 128, out previewMimetype, out previewPath, out error))
				{             
					thumbnailBlob = new Blob {
						id = Guid.NewGuid().ToString(),
                        name = "thumbnail",
						mimetype = previewMimetype
					};
					thumbnailTempFile = previewPath;
				}
			}
			catch (Exception e)
			{
				thumbnailTempFile = null;
				thumbnailBlob = null;            
				Console.WriteLine($"ThumbnailPlugin fails {e.ToString()}");
			}
		}
        

		public void AddPlugin(IFilePlugin plugin)
        {
            foreach(string mimetype in plugin.MimeTypes) {
                List<IFilePlugin> plugins;
                if(mimePlugins.ContainsKey(mimetype))
                    plugins = mimePlugins[mimetype];
                else {
                    plugins = new List<IFilePlugin>();
                    mimePlugins[mimetype] = plugins;
                }
                plugins.Add(plugin);
            }
            allPlugins.Add(plugin);
        }
	}
}
