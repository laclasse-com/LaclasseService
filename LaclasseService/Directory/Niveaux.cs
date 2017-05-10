// Niveaux.cs
// 
//  Handle matieres API. 
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
using System.Collections.Generic;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "niveau", PrimaryKey = "id")]
	public class Niveau: Model
	{
		public string id { get { return GetField<string>("id", null); } set { SetField("id", value); } }
		public string name { get { return GetField<string>("name", null); } set { SetField("name", value); } }
		public string rattach { get { return GetField<string>("rattach", null); } set { SetField("rattach", value); } }
		public string stat { get { return GetField<string>("stat", null); } set { SetField("stat", value); } }
	}

	public class Niveaux : HttpRouting
	{
		readonly string dbUrl;

		public Niveaux(string dbUrl)
		{
			this.dbUrl = dbUrl;

			GetAsync["/"] = async (p, c) =>
			{
				var res = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var item in await db.SelectAsync("SELECT * FROM niveau"))
					{
						res.Add(NiveauToJson(item));
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = res;
			};

			GetAsync["/{id}"] = async (p, c) =>
			{
				var jsonResult = await GetNiveauAsync((string)p["id"]);
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

				var jsonResult = await CreateNiveauAsync(await c.Request.ReadAsJsonAsync());
				if (jsonResult == null)
					c.Response.StatusCode = 500;
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
			};

			PutAsync["/{id}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields("name", "rattach", "stat");
				if (extracted.Count == 0)
					return;
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.UpdateRowAsync("niveau", "id", p["id"], extracted);
					if (count > 0)
					{
						c.Response.StatusCode = 200;
						c.Response.Content = await GetNiveauAsync(db, (string)p["id"]);
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
					int count = await db.DeleteAsync("DELETE FROM niveau WHERE id=?", (string)p["id"]);
					if (count == 0)
						c.Response.StatusCode = 404;
					else
						c.Response.StatusCode = 200;
				}
			};
		}

		JsonObject NiveauToJson(Dictionary<string, object> item)
		{
			return new JsonObject
			{
				["id"] = (string)item["id"],
				["name"] = (string)item["name"],
				["rattach"] = (string)item["rattach"],
				["stat"] = (string)item["stat"]
			};
		}

		public async Task<JsonValue> GetNiveauAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetNiveauAsync(db, id);
			}
		}

		public async Task<JsonValue> GetNiveauAsync(DB db, string id)
		{
			var item = (await db.SelectAsync("SELECT * FROM niveau WHERE id=?", id)).SingleOrDefault();
			return (item == null) ? null : NiveauToJson(item);
		}

		public async Task<string> GetNiveauLibelleAsync(DB db, string id)
		{
			return (string)await db.ExecuteScalarAsync("SELECT name FROM niveau WHERE id=?", id);
		}

		public async Task<JsonValue> CreateNiveauAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await CreateNiveauAsync(db, json);
			}
		}

		public async Task<JsonValue> CreateNiveauAsync(DB db, JsonValue json)
		{
			json.RequireFields("id", "name");
			var extracted = json.ExtractFields("id", "name", "rattach", "stat");

			return (await db.InsertRowAsync("niveau", extracted) == 1) ?
				await GetNiveauAsync(db, (string)extracted["id"]) : null;
		}

		public async Task<JsonValue> ModifyNiveauAsync(string id, JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await ModifyNiveauAsync(db, id, json);
			}
		}

		public async Task<JsonValue> ModifyNiveauAsync(DB db, string id, JsonValue json)
		{
			var extracted = json.ExtractFields("name", "rattach", "stat");
			if (extracted.Count > 0)
				await db.UpdateRowAsync("niveau", "id", id, extracted);
			return await GetNiveauAsync(db, id);
		}

		public async Task<bool> DeleteNiveauAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await DeleteNiveauAsync(db, id);
			}
		}

		public async Task<bool> DeleteNiveauAsync(DB db, string id)
		{
			return (await db.DeleteAsync("DELETE FROM niveau WHERE id=?", id)) != 0;
		}
	}
}
