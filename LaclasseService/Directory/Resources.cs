// Resources.cs
// 
//  Handle resources API. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
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
using System.Collections.Generic;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class Resources: HttpRouting
	{
		string DBUrl;

		public Resources(string dbUrl)
		{
			DBUrl = dbUrl;

			GetAsync["/"] = async (p, c) =>
			{
				var res = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var resource in await db.SelectAsync("SELECT * FROM ressources_num"))
					{
						res.Add(ResourceToJson(resource));
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = res;
			};

			GetAsync["/{id:int}"] = async (p, c) =>
			{
				var json = await GetResourceAsync((int)p["id"]);
				if (json == null)
					c.Response.StatusCode = 404;
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = json;
				}
			};

			PostAsync["/"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields(
					"lib", "url_access_get", "site_web", "code", "url_logout", "type_ressource",
					"nom_court");
				// check required fields
				if (!extracted.ContainsKey("lib") || !extracted.ContainsKey("code"))
					throw new WebException(400, "Missing arguments");

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int res = await db.InsertRowAsync("ressources_num", extracted);
					if (res == 1)
					{
						var jsonResult = await GetResourceAsync(db, (int)await db.LastInsertIdAsync());
						if(jsonResult != null) 
						{
							c.Response.StatusCode = 200;
							c.Response.Content = jsonResult;
						}
						else
							c.Response.StatusCode = 500;
					}
					else
						c.Response.StatusCode = 500;
				}
			};

			PutAsync["/{id:int}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields(
					"lib", "url_access_get", "site_web", "code", "url_logout", "type_ressource",
					"nom_court");
				JsonValue jsonResult = null;
				if (extracted.Count > 0)
				{
					using (DB db = await DB.CreateAsync(dbUrl))
					{
						if ((await db.UpdateRowAsync("ressources_num", "id", p["id"], extracted)) > 0)
							jsonResult = await GetResourceAsync(db, (int)p["id"]);
					}
				}
				if (jsonResult == null)
					c.Response.StatusCode = 404;
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
			};

			DeleteAsync["/{id:int}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.DeleteAsync("DELETE FROM ressources_num WHERE id=?", (int)p["id"]);
					if (count == 0)
						c.Response.StatusCode = 404;
					else
						c.Response.StatusCode = 200;
				}
			};
		}

		public JsonObject ResourceToJson(Dictionary<string, object> resource)
		{
			return new JsonObject
			{
				["id"] = (int)resource["id"],
				["lib"] = (string)resource["lib"],
				["url_access_get"] = (string)resource["url_access_get"],
				["site_web"] = (string)resource["site_web"],
				["date_modified"] = (DateTime?)resource["date_modified"],
				["code"] = (string)resource["code"],
				["url_logout"] = (string)resource["url_logout"],
				["type_ressource"] = (string)resource["type_ressource"],
				["nom_court"] = (string)resource["nom_court"]
			};
		}

		public async Task<JsonValue> GetResourceAsync(int id)
		{
			using (DB db = await DB.CreateAsync(DBUrl))
			{
				return await GetResourceAsync(db, id);
			}
		}

		public async Task<JsonValue> GetResourceAsync(DB db, int id)
		{
			var resource = (await db.SelectAsync("SELECT * FROM ressources_num WHERE id=?", id)).First();
			return (resource == null) ? null : ResourceToJson(resource);
		}


	}
}
