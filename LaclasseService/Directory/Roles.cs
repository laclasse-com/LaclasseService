// Roles.cs
// 
//  Handles roles API. 
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

using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class Roles : HttpRouting
	{
		readonly string dbUrl;

		public Roles(string dbUrl)
		{
			this.dbUrl = dbUrl;

			GetAsync["/"] = async (p, c) =>
			{
				var res = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var item in await db.SelectAsync("SELECT * FROM role"))
					{
						res.Add(RoleToJson(item));
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = res;
			};

			GetAsync["/{id}"] = async (p, c) =>
			{
				var jsonResult = await GetRoleAsync((string)p["id"]);
				if (jsonResult == null)
					c.Response.StatusCode = 404;
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
			};

			PostAsync["/"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();
				var json = await c.Request.ReadAsJsonAsync();
				json.RequireFields("id");
				var extracted = json.ExtractFields("id", "libelle", "description", "priority");

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int res = await db.InsertRowAsync("role", extracted);
					if (res == 1)
					{
						var jsonResult = await GetRoleAsync(db, json["id"]);
						if (jsonResult != null)
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

			PutAsync["/{id}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields("libelle", "description", "priority");
				if (extracted.Count == 0)
					return;
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.UpdateRowAsync("role", "id", p["id"], extracted);
					if (count > 0)
					{
						c.Response.StatusCode = 200;
						c.Response.Content = await GetRoleAsync(db, (string)p["id"]);
					}
					else
						c.Response.StatusCode = 404;
				}
			};

			DeleteAsync["/{id}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.DeleteAsync("DELETE FROM role WHERE id=?", (string)p["id"]);
					if (count == 0)
						c.Response.StatusCode = 404;
					else
						c.Response.StatusCode = 200;
				}
			};
		}

		JsonObject RoleToJson(Dictionary<string, object> item)
		{
			return new JsonObject
			{
				["id"] = (string)item["id"],
				["libelle"] = (string)item["libelle"],
				["description"] = (string)item["description"],
				["priority"] = (int)item["priority"]
			};
		}

		public async Task<JsonValue> GetRoleAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetRoleAsync(db, id);
			}
		}

		public async Task<JsonValue> GetRoleAsync(DB db, string id)
		{
			var item = (await db.SelectAsync("SELECT * FROM role WHERE id=?", id)).SingleOrDefault();
			return (item == null) ? null : RoleToJson(item);
		}
	}
}
