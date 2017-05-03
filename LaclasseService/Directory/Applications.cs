// Applications.cs
// 
//  Handle applications API. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
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

using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class Applications: HttpRouting
	{
		readonly string dbUrl;

		public Applications(string dbUrl)
		{
			this.dbUrl = dbUrl;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/"] = async (p, c) =>
			{
				var res = new JsonArray();
				var filter = "TRUE";
				JsonArray jsonArray = null;
				if (c.Request.Headers.ContainsKey("content-type") &&
					(c.Request.Headers["content-type"] == "application/json"))
				{
					var json = await c.Request.ReadAsJsonAsync();
					jsonArray = json as JsonArray;
				}
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					if (jsonArray != null)
						filter = db.InFilter("id", jsonArray.Select((arg) => (string)(arg.Value)));
					foreach (var app in await db.SelectAsync($"SELECT * FROM application WHERE {filter}"))
					{
						res.Add(ApplicationToJson(app));
					}
				}

				c.Response.StatusCode = 200;
				c.Response.Content = res;
			};

			GetAsync["/{id}"] = async (p, c) =>
			{
				var jsonResult = await GetApplicationAsync((string)p["id"]);
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
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonArray)
				{
					var jsonResult = new JsonArray();
					using (DB db = await DB.CreateAsync(dbUrl))
					{
						foreach (var jsonItem in (JsonArray)json)
							jsonResult.Add(await CreateApplicationAsync(jsonItem));
					}
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = await CreateApplicationAsync(json);
				}
			};

			PutAsync["/{id}"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields("libelle", "description", "url", "password");
				if (extracted.Count == 0)
					return;
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.UpdateRowAsync("application", "id", p["id"], extracted);
					if (count > 0)
					{
						c.Response.StatusCode = 200;
						c.Response.Content = await GetApplicationAsync(db, json["id"]);
					}
					else
						c.Response.StatusCode = 404;
				}
			};

			DeleteAsync["/{id}"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.DeleteAsync("DELETE FROM application WHERE id=?", (string)p["id"]);
					if (count == 0)
						c.Response.StatusCode = 404;
					else
						c.Response.StatusCode = 200;
				}
			};

			DeleteAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				var ids = ((JsonArray)json).Select((arg) => (string)(arg.Value));

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.DeleteAsync("DELETE FROM application WHERE "+db.InFilter("id", ids));
					if (count == 0)
						c.Response.StatusCode = 404;
					else
						c.Response.StatusCode = 200;
				}
			};
		}

		JsonObject ApplicationToJson(Dictionary<string, object> item)
		{
			return new JsonObject
			{
				["id"] = (string)item["id"],
				["libelle"] = (string)item["libelle"],
				["description"] = (string)item["description"],
				["url"] = (string)item["url"],
				["password"] = (string)item["password"]
			};
		}

		public async Task<JsonValue> GetApplicationAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetApplicationAsync(db, id);
			}
		}

		public async Task<JsonValue> GetApplicationAsync(DB db, string id)
		{
			var item = (await db.SelectAsync("SELECT * FROM application WHERE id=?", id)).First();
			return (item == null) ? null : ApplicationToJson(item);
		}

		public async Task<JsonValue> CreateApplicationAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await CreateApplicationAsync(db, json);
			}
		}

		public async Task<JsonValue> CreateApplicationAsync(DB db, JsonValue json)
		{
			var extracted = json.ExtractFields("id", "libelle", "description", "url", "password");
			// check required fields
			if (!extracted.ContainsKey("id") || !extracted.ContainsKey("url"))
				throw new WebException(400, "Bad protocol. 'id' and 'url' are needed");

			JsonValue jsonResult = null;
			if (await db.InsertRowAsync("application", extracted) == 1)
				jsonResult = await GetApplicationAsync(db, json["id"]);
			return jsonResult;
		}

		public async Task<string> CheckPasswordAsync(string login, string password)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await CheckPasswordAsync(db, login, password);
			}
		}

		public async Task<string> CheckPasswordAsync(DB db, string login, string password)
		{
			string user = null;
			var item = (await db.SelectAsync("SELECT * FROM application WHERE id=?", login)).SingleOrDefault();
			if (item != null)
			{
				if ((item["password"] != null) && (password == (string)item["password"]))
					user = (string)item["id"];
			}
			return user;
		}
	}
}
