// Resources.cs
// 
//  Handle resources API. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017-2018 Metropole de Lyon
// Copyright (c) 2017 Daniel LACROIX
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
using Dir = System.IO.Directory;
using System.Diagnostics;
using System.Threading.Tasks;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public enum ResourceUrlMode
	{
		GLOBAL,
		USERDEFINED
	}

	public enum ResourceEmbedMode
	{
		IFRAME,
		EXTERNAL,
		PORTAL,
		REPLACE
	}

    public enum ResourceCost
    {
        FREE,
        SUBSCRIPTION
    }
    
    public enum ResourceType
	{
        MANUEL,
        DICO,
        NEWS,
        APPLICATION,
        AUTRE
	}

	public enum ResourceGrade
    {
        PRIMAIRE,
        SECONDAIRE,
        ALL
    }

	[Model(Table = "resource", PrimaryKey = nameof(id))]
	public class Resource : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField]
		public string name { get { return GetField<string>(nameof(name), null); } set { SetField(nameof(name), value); } }
		[ModelField]
		public string url { get { return GetField<string>(nameof(url), null); } set { SetField(nameof(url), value); } }
		[ModelField]
		public string site_web { get { return GetField<string>(nameof(site_web), null); } set { SetField(nameof(site_web), value); } }
		[ModelField]
		public DateTime ctime { get { return GetField<DateTime>(nameof(ctime), DateTime.Now); } set { SetField(nameof(ctime), value); } }
		[ModelField]
		public DateTime? mtime { get { return GetField<DateTime?>(nameof(mtime), null); } set { SetField(nameof(mtime), value); } }
		[ModelField]
		public string icon { get { return GetField<string>(nameof(icon), null); } set { SetField(nameof(icon), value); } }
		[ModelField]
		public string color { get { return GetField<string>(nameof(color), null); } set { SetField(nameof(color), value); } }
		[ModelField]
		public string description { get { return GetField<string>(nameof(description), null); } set { SetField(nameof(description), value); } }
		[ModelField]
		public string editor { get { return GetField<string>(nameof(editor), null); } set { SetField(nameof(editor), value); } }
		[ModelField]
		public ResourceUrlMode url_mode { get { return GetField(nameof(url_mode), ResourceUrlMode.GLOBAL); } set { SetField(nameof(url_mode), value); } }
		[ModelField]
		public ResourceEmbedMode embed { get { return GetField(nameof(embed), ResourceEmbedMode.EXTERNAL); } set { SetField(nameof(embed), value); } }
        [ModelField]
        public ResourceCost cost { get { return GetField(nameof (cost), ResourceCost.FREE); } set { SetField (nameof (cost), value); } }
		[ModelField]
		public ResourceType type { get { return GetField(nameof(type), ResourceType.AUTRE); } set { SetField(nameof(type), value); } }
		[ModelField]
		public ResourceGrade grade { get { return GetField(nameof(grade), ResourceGrade.ALL); } set { SetField(nameof(grade), value); } }
		[ModelField]
        public int index { get { return GetField(nameof(index), 0); } set { SetField(nameof(index), value); } }
		[ModelField]
		public bool default_visible { get { return GetField(nameof(default_visible), false); } set { SetField(nameof(default_visible), value); } }      

		[ModelExpandField(Name = nameof(structures), ForeignModel = typeof(StructureResource), Visible = false)]
		public ModelList<StructureResource> structures {
			get { return GetField<ModelList<StructureResource>>(nameof(structures), null); }
			set { SetField(nameof(structures), value); } }

        [ModelExpandField (Name = nameof (sso_clients), ForeignModel = typeof (SsoClient))]
        public ModelList<SsoClient> sso_clients { get { return GetField<ModelList<SsoClient>> (nameof (sso_clients), null); } set { SetField (nameof (sso_clients), value); } }

		public override async Task EnsureRightAsync(HttpContext context, Right right, Model diff)
		{
			if (right != Right.Read)
				await context.EnsureIsSuperAdminAsync();
			else
				await context.EnsureIsAuthenticatedAsync();
		}
	}

	public class Resources: ModelService<Resource>
	{
		public Resources(string dbUrl, string storageDir) : base(dbUrl)
		{
			var resourceDir = Path.Combine(storageDir, "resource");

			if (!Dir.Exists(resourceDir))
				Dir.CreateDirectory(resourceDir);

			GetAsync["/{id:int}/image"] = async (p, c) =>
			{
				var id = (int)p["id"];

                var oldResource = new Resource { id = id };
                using (var db = await DB.CreateAsync(dbUrl))
                {
                    if (!await oldResource.LoadAsync(db, true))
                        oldResource = null;
                }

                if (oldResource == null)
                    return;

				var fullPath = Path.Combine(resourceDir, $"{id}.jpg");

				if (File.Exists(fullPath))
				{
					var shortName = Path.GetFileName(fullPath);
                    c.Response.Headers["content-type"] = "image/jpeg";
                    
                    var lastModif = File.GetLastWriteTime(fullPath);
					string etag = "\"" + lastModif.Ticks.ToString("X") + "\"";
					c.Response.Headers["etag"] = etag;

					if (c.Request.QueryString.ContainsKey("if-none-match") &&
					    (c.Request.QueryString["if-none-match"] == etag))
					{
						c.Response.StatusCode = 304;
					}
					else
					{
						c.Response.StatusCode = 200;
						c.Response.SupportRanges = true;
						c.Response.Content = new FileContent(fullPath);
					}               
				}
			};

			DeleteAsync["/{id:int}/image"] = async (p, c) =>
            {
                var id = (int)p["id"];

                var oldResource = new Resource { id = id };
                using (var db = await DB.CreateAsync(dbUrl))
                {
                    if (!await oldResource.LoadAsync(db, true))
                        oldResource = null;
                }

                if (oldResource == null)
                    return;

                var fullPath = Path.Combine(resourceDir, $"{id}.jpg");

                if (File.Exists(fullPath))
                {
					File.Delete(fullPath);
					c.Response.StatusCode = 200;
					c.Response.Content = "";
                }
            };

			PostAsync["/{id:int}/image"] = async (p, c) =>
            {
                var id = (int)p["id"];
            
                var oldResource = new Resource { id = id };
                using (var db = await DB.CreateAsync(dbUrl))
                {
					if (!await oldResource.LoadAsync(db, true))
						oldResource = null;
                }

				if (oldResource == null)
                    return;

                var reader = c.Request.ReadAsMultipart();
                MultipartPart part;
                while ((part = await reader.ReadPartAsync()) != null)
                {
                    if (part.Headers.ContainsKey("content-disposition") && part.Headers.ContainsKey("content-type"))
                    {
                        if ((part.Headers["content-type"] != "image/jpeg") &&
                            (part.Headers["content-type"] != "image/png") &&
                            (part.Headers["content-type"] != "image/svg+xml"))
                            continue;

                        var disposition = ContentDisposition.Decode(part.Headers["content-disposition"]);
                        if (disposition.ContainsKey("name") && (disposition["name"] == "image"))
                        {
                            var dir = DirExt.CreateRecursive(resourceDir);
							var fullPath = Path.Combine(dir.FullName, $"{id}.jpg");

                            // crop / resize / convert the image using ImageMagick
							var startInfo = new ProcessStartInfo("/usr/bin/convert", $"- -auto-orient -strip -distort SRT 0 +repage -quality 80 -resize 1024x1024 jpeg:{fullPath}");
                            startInfo.RedirectStandardOutput = false;
                            startInfo.RedirectStandardInput = true;
                            startInfo.UseShellExecute = false;
                            var process = new Process();
                            process.StartInfo = startInfo;
                            process.Start();

                            // read the file stream and send it to ImageMagick
                            await part.Stream.CopyToAsync(process.StandardInput.BaseStream);
                            process.StandardInput.Close();

                            process.WaitForExit();
                            process.Dispose();

                            c.Response.StatusCode = 200;
                        }
                    }
                }
            };         
		}      
	}
}
