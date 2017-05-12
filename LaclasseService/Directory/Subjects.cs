// Subjects.cs
// 
//  Handle school subjects API. 
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
	[Model(Table = "subject", PrimaryKey = "id")]
	public class Subject : Model
	{
		public string id { get { return GetField<string>("id", null); } set { SetField("id", value); } } 
		public string name { get { return GetField<string>("name", null); } set { SetField("name", value); } }
	}

	public class Subjects : HttpRouting
	{
		readonly string dbUrl;

		public Subjects(string dbUrl)
		{
			this.dbUrl = dbUrl;

			GetAsync["/"] = async (p, c) =>
			{
				var res = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var item in await db.SelectAsync("SELECT * FROM subject"))
					{
						res.Add(SubjectToJson(item));
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = res;
			};

			GetAsync["/{id}"] = async (p, c) =>
			{
				var jsonResult = await GetSubjectAsync((string)p["id"]);
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
				var jsonResult = await CreateSubjectAsync(await c.Request.ReadAsJsonAsync());
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

				var jsonResult = await ModifySubjectAsync((string)p["id"], await c.Request.ReadAsJsonAsync());
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
				c.Response.StatusCode = await DeleteSubjectAsync((string)p["id"]) ? 200 : 404;
			};
		}

		JsonObject SubjectToJson(Dictionary<string, object> item)
		{
			return new JsonObject
			{
				["id"] = (string)item["id"],
				["name"] = (string)item["name"]
			};
		}

		public async Task<JsonValue> GetSubjectAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetSubjectAsync(db, id);
			}
		}

		public async Task<JsonValue> GetSubjectAsync(DB db, string id)
		{
			var item = (await db.SelectAsync("SELECT * FROM subject WHERE id=?", id)).SingleOrDefault();
			return (item == null) ? null : SubjectToJson(item);
		}

		public async Task<JsonValue> CreateSubjectAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await CreateSubjectAsync(db, json);
			}
		}

		public async Task<JsonValue> CreateSubjectAsync(DB db, JsonValue json)
		{
			json.RequireFields("id", "name");
			var extracted = json.ExtractFields("id", "name");

			return (await db.InsertRowAsync("subject", extracted) == 1) ? 
				await GetSubjectAsync(db, (string)extracted["id"]) : null;
		}

		public async Task<JsonValue> ModifySubjectAsync(string id, JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await ModifySubjectAsync(db, id, json);
			}
		}

		public async Task<JsonValue> ModifySubjectAsync(DB db, string id, JsonValue json)
		{
			var extracted = json.ExtractFields("name");
			if (extracted.Count > 0)
				await db.UpdateRowAsync("subject", "id", id, extracted);
			return await GetSubjectAsync(db, id);
		}

		public async Task<bool> DeleteSubjectAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await DeleteSubjectAsync(db, id);
			}
		}

		public async Task<bool> DeleteSubjectAsync(DB db, string id)
		{
			return (await db.DeleteAsync("DELETE FROM subject WHERE id=?", id)) != 0;
		}
	}
}
