﻿// Matieres.cs
// 
//  Handle matieres API. 
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
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "matiere", PrimaryKey = "id")]
	public class Matiere : Model
	{
		public string id { get { return GetField<string>("id", null); } set { SetField("id", value); } } 
		public string name { get { return GetField<string>("name", null); } set { SetField("name", value); } }
	}

	public class Matieres : HttpRouting
	{
		readonly string dbUrl;

		public Matieres(string dbUrl)
		{
			this.dbUrl = dbUrl;

			GetAsync["/"] = async (p, c) =>
			{
				var res = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var item in await db.SelectAsync("SELECT * FROM matiere"))
					{
						res.Add(MatiereToJson(item));
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = res;
			};

			GetAsync["/{id}"] = async (p, c) =>
			{
				var jsonResult = await GetMatiereAsync((string)p["id"]);
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
				var jsonResult = await CreateMatiereAsync(await c.Request.ReadAsJsonAsync());
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

				var jsonResult = await ModifyMatiereAsync((string)p["id"], await c.Request.ReadAsJsonAsync());
				if (jsonResult != null)
				{
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
				else
					c.Response.StatusCode = 404;
			};

			DeleteAsync["/{id}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();
				c.Response.StatusCode = await DeleteMatiereAsync((string)p["id"]) ? 200 : 404;
			};
		}

		JsonObject MatiereToJson(Dictionary<string, object> item)
		{
			return new JsonObject
			{
				["id"] = (string)item["id"],
				["name"] = (string)item["name"]
			};
		}

		public async Task<JsonValue> GetMatiereAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetMatiereAsync(db, id);
			}
		}

		public async Task<JsonValue> GetMatiereAsync(DB db, string id)
		{
			var item = (await db.SelectAsync("SELECT * FROM matiere WHERE id=?", id)).SingleOrDefault();
			return (item == null) ? null : MatiereToJson(item);
		}

		public async Task<JsonValue> CreateMatiereAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await CreateMatiereAsync(db, json);
			}
		}

		public async Task<JsonValue> CreateMatiereAsync(DB db, JsonValue json)
		{
			json.RequireFields("id", "name");
			var extracted = json.ExtractFields("id", "name");

			return (await db.InsertRowAsync("matiere", extracted) == 1) ? 
				await GetMatiereAsync(db, (string)extracted["id"]) : null;
		}

		public async Task<JsonValue> ModifyMatiereAsync(string id, JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await ModifyMatiereAsync(db, id, json);
			}
		}

		public async Task<JsonValue> ModifyMatiereAsync(DB db, string id, JsonValue json)
		{
			var extracted = json.ExtractFields("name");
			if (extracted.Count > 0)
				await db.UpdateRowAsync("matiere", "id", id, extracted);
			return await GetMatiereAsync(db, id);
		}

		public async Task<bool> DeleteMatiereAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await DeleteMatiereAsync(db, id);
			}
		}

		public async Task<bool> DeleteMatiereAsync(DB db, string id)
		{
			return (await db.DeleteAsync("DELETE FROM matiere WHERE id=?", id)) != 0;
		}
	}
}
