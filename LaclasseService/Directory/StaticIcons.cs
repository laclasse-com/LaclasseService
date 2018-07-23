// StaticIcons.cs
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
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;

namespace Laclasse.Directory
{
	public class StaticIcons: HttpRouting
    {
		readonly DirectoryInfo baseDir;
		readonly int cacheDuration;

		public StaticIcons(string basedir, int cacheDuration)
        {
			if (Path.IsPathRooted(basedir))
				this.baseDir = new DirectoryInfo(Path.GetFullPath(basedir));
            else
				this.baseDir = new DirectoryInfo(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, basedir)));
			if (!this.baseDir.FullName.EndsWith("/", StringComparison.InvariantCulture))
				this.baseDir = new DirectoryInfo(this.baseDir.FullName + "/");
            this.cacheDuration = cacheDuration;
            
			Get["/"] = (p, c) =>
			{
				var expand = false;
				if (c.Request.QueryString.ContainsKey("expand"))
					expand = bool.Parse(c.Request.QueryString["expand"]);
				var res = new JsonArray();
				GetIcons(baseDir, res, expand);
				c.Response.StatusCode = 200;
				c.Response.Headers["cache-control"] = "public, max-age=" + cacheDuration;
				c.Response.Content = res;
			};
        }

		void GetIcons(DirectoryInfo findDir, JsonArray res, bool wantData)
		{
			var baseDirLength = baseDir.FullName.Length;
			foreach (var file in findDir.EnumerateFiles())
			{
				if (file.Extension == ".svg")
				{               
					var obj = new JsonObject()
					{
						["id"] = file.FullName.Substring(baseDirLength, file.FullName.Length - (baseDirLength + 4))
					};
					if (wantData)
						obj["data"] = File.ReadAllText(file.FullName);
					res.Add(obj);
				}
			}
			foreach (var childDir in findDir.EnumerateDirectories())
				GetIcons(childDir, res, wantData);
		}

		public override async Task ProcessRequestAsync(HttpContext context)
		{
			await base.ProcessRequestAsync(context);

			// handle only if not already handled
			if (context.Response.StatusCode != -1)
				return;

			if (context.Request.Method == "GET")
			{
				var parts = context.Request.Path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
				var allParts = new string[parts.Length + 1];
				allParts[0] = baseDir.FullName;
				parts.CopyTo(allParts, 1);
				var fullPath = Path.GetFullPath(Path.Combine(allParts)) + ".svg";
				// check if full path is in the base directory
				if (!fullPath.StartsWith(baseDir.FullName, StringComparison.InvariantCulture))
				{
					context.Response.StatusCode = 403;
					context.Response.Content = new StringContent("Invalid file path\r\n");
					return;
				}

				if (File.Exists(fullPath))
				{
					var shortName = Path.GetFileName(fullPath);
					context.Response.Headers["content-type"] = "image/svg+xml";

					context.Response.StatusCode = 200;
					context.Response.Headers["cache-control"] = "public, max-age=" + cacheDuration;
					context.Response.Content = new FileContent(fullPath);
				}
			}
		}
    }   
}
