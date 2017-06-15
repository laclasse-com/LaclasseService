// Aaf.cs
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017 Metropole de Lyon
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Aaf
{
	public class Aaf : HttpRouting
	{
		readonly string syncFilesFolder;

		public Aaf(string dbUrl, string syncFilesFolder)
		{
			this.syncFilesFolder = syncFilesFolder;
			//string syncLogsFolder = "/home/daniel/Programmation/laclassev4/aaf/logs";

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/"] = async (p, c) =>
			{
				var dir = new DirectoryInfo(syncFilesFolder);
				var result = new JsonArray();
				foreach (var file in dir.EnumerateFiles("*.zip"))
				{
					var jsonFile = new JsonObject { ["file"] = file.Name };
					var matches = Regex.Match(file.Name, "ENT2D\\.(20\\d\\d)(\\d\\d)(\\d\\d).*\\.zip");
					if (matches.Success)
						jsonFile["date"] = new DateTime(
							Convert.ToInt32(matches.Groups[1].Value),
							Convert.ToInt32(matches.Groups[2].Value),
							Convert.ToInt32(matches.Groups[3].Value));
					result.Add(jsonFile);
				}
				c.Response.StatusCode = 200;
				c.Response.Content = result;

				await Task.Delay(0);
			};

			/*Get["/{id}"] = (p, c) =>
			{
				var aafFile = GetFile((string)p["id"]);
				if (aafFile != null)
				{
					c.Response.StatusCode = 200;
					c.Response.Content = "TODO";
				}
			};*/

			GetAsync["/{id}"] = async (p, c) =>
			{
				var aafFile = GetFile((string)p["id"]);
				if (aafFile != null)
				{
					//var json = await c.Request.ReadAsJsonAsync();
					var apply = true;
					if (c.Request.QueryString.ContainsKey("apply"))
						apply = Convert.ToBoolean(c.Request.QueryString["apply"]);

					bool subject = true;
					if (c.Request.QueryString.ContainsKey("subject"))
						subject = Convert.ToBoolean(c.Request.QueryString["subject"]);
					bool grade = true;
					if (c.Request.QueryString.ContainsKey("grade"))
						grade = Convert.ToBoolean(c.Request.QueryString["grade"]);
					bool structure = true;
					if (c.Request.QueryString.ContainsKey("structure"))
						structure = Convert.ToBoolean(c.Request.QueryString["structure"]);
					bool persEducNat = true;
					if (c.Request.QueryString.ContainsKey("persEducNat"))
						persEducNat = Convert.ToBoolean(c.Request.QueryString["persEducNat"]);
					bool eleve = true;
					if (c.Request.QueryString.ContainsKey("eleve"))
						eleve = Convert.ToBoolean(c.Request.QueryString["eleve"]);
					bool persRelEleve = true;
					if (c.Request.QueryString.ContainsKey("persRelEleve"))
						persRelEleve = Convert.ToBoolean(c.Request.QueryString["persRelEleve"]);
					
					var sync = new Synchronizer(dbUrl);

					var diff = await sync.Synchronize(
						aafFile.FullName, subject, grade, structure, persEducNat, eleve, persRelEleve, apply);

					c.Response.StatusCode = 200;
					c.Response.Content = diff;
				}
				//json.RequireFields("structures", "persEducNat", ""
			};
		}

		FileInfo GetFile(string id)
		{
			var dir = new DirectoryInfo(syncFilesFolder);
			foreach (var file in dir.EnumerateFiles("*.zip"))
				if (file.Name == id)
					return file;
			return null;
		}
	}
}
