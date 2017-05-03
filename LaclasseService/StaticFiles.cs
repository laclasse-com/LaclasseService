// StaticFiles.cs
// 
//  Provide a service to distribute static files
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2013-2014 Departement du Rhone
// Copyright (c) 2017 Metropole de Lyon
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
using Erasme.Http;

namespace Laclasse
{
	public class StaticFiles : HttpHandler
	{
		readonly string basedir;
		readonly int cacheDuration;

		public StaticFiles(string basedir, int cacheDuration)
		{
			if (Path.IsPathRooted(basedir))
				this.basedir = Path.GetFullPath(basedir);
			else
				this.basedir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, basedir));
			this.cacheDuration = cacheDuration;
		}

		public override void ProcessRequest(HttpContext context)
		{
			// handle only if not already handled
			if (context.Response.StatusCode != -1)
				return;

			if (context.Request.Method == "GET")
			{
				var parts = context.Request.Path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
				var allParts = new string[parts.Length + 1];
				allParts[0] = basedir;
				parts.CopyTo(allParts, 1);
				var fullPath = Path.GetFullPath(Path.Combine(allParts));
				// check if full path is in the base directory
				if (!fullPath.StartsWith(basedir, StringComparison.InvariantCulture))
				{
					context.Response.StatusCode = 403;
					context.Response.Content = new StringContent("Invalid file path\r\n");
					return;
				}

				if (!File.Exists(fullPath) && Dir.Exists(fullPath))
				{
					if (context.Request.Path.EndsWith("/", StringComparison.InvariantCulture))
						fullPath = Path.Combine(fullPath, "index.html");
					else
					{
						context.Response.StatusCode = 301;
						context.Response.Headers["location"] = context.Request.Path + "/";
						context.Response.Content = new StringContent();
						return;
					}
				}

				if (File.Exists(fullPath))
				{
					var shortName = Path.GetFileName(fullPath);

					var mimetype = FileContent.MimeType(shortName);
					context.Response.Headers["content-type"] = mimetype;

					if ((mimetype == "application/font-woff") || (mimetype == "font/ttf") || (mimetype == "application/vnd.ms-fontobject"))
					{
						context.Response.StatusCode = 200;
						context.Response.Headers["cache-control"] = "public, max-age=" + cacheDuration;
						context.Response.SupportRanges = true;
						context.Response.Content = new FileContent(fullPath);
					}
					else
					{
						var lastModif = File.GetLastWriteTime(fullPath);
						string etag = "\"" + lastModif.Ticks.ToString("X") + "\"";
						context.Response.Headers["etag"] = etag;

						if (context.Request.QueryString.ContainsKey("if-none-match") &&
						   (context.Request.QueryString["if-none-match"] == etag))
						{
							context.Response.StatusCode = 304;
						}
						else
						{
							context.Response.StatusCode = 200;
							context.Response.SupportRanges = true;
							context.Response.Content = new FileContent(fullPath);
						}
					}
				}
			}
		}
	}
}
