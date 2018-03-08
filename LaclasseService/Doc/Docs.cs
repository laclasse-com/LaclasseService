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
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;
using ICSharpCode.SharpZipLib.Zip;

namespace Laclasse.Docs
{
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
		public string mime { get { return GetField<string>(nameof(mime), "unknown"); } set { SetField(nameof(mime), value); } }
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

		[ModelExpandField(Name = nameof(children), ForeignModel = typeof(Node))]
		public ModelList<Node> children { get { return GetField<ModelList<Node>>(nameof(children), null); } set { SetField(nameof(children), value); } }
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

	public class Docs : Directory.ModelService<Node>
	{
		string dbUrl;
		string path;

		public Docs(string dbUrl, string path): base(dbUrl)
		{
			this.dbUrl = dbUrl;
			this.path = path;

			GetAsync["/file/{id:int}/content"] = async (p, c) =>
			{
				using (var db = await DB.CreateAsync(dbUrl))
				{
					var node = await db.SelectRowAsync<Node>(p["id"], false);
					if (node != null)
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
							c.Response.Content = new FileContent(fullPath);
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
	}
}
