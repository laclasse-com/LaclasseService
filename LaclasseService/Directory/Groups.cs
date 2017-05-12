// Groupe.cs
// 
//  Handle groupes API. 
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
	public class Groups : HttpRouting
	{
		readonly string dbUrl;
		readonly Grades grades;

		public Groups(string dbUrl, Grades grades)
		{
			this.dbUrl = dbUrl;
			this.grades = grades;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/{id:int}"] = async (p, c) =>
			{
				var json = await GetGroupAsync((int)p["id"]);
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
				var json = await c.Request.ReadAsJsonAsync();
				c.Response.StatusCode = 200;
				c.Response.Content = await CreateGroupAsync(json);
			};

			PutAsync["/{id:int}"] = async (p, c) =>
			{
				Console.WriteLine($"PUT GROUPS ID: {(int)p["id"]}");

				var json = await c.Request.ReadAsJsonAsync();
				c.Response.StatusCode = 200;
				c.Response.Content = await ModifyGroupAsync((int)p["id"], json);
			};

			DeleteAsync["/{id:int}"] = async (p, c) =>
			{
				if(await DeleteGroupAsync((int)p["id"]))
					c.Response.StatusCode = 200;
				else
					c.Response.StatusCode = 404;
			};
		}

		async Task<JsonObject> GroupToJsonAsync(DB db, Dictionary<string, object> group, bool expand = true)
		{
			var id = (int)group["id"];
			var grade_id = (string)group["grade_id"];
			var grade_name = await grades.GetGradeLibelleAsync(db, grade_id);

			var result = new JsonObject
			{
				["id"] = id,
				["name"] = (string)group["name"],
				["description"] = (string)group["description"],
				["aaf_mtime"] = (DateTime?)group["aaf_mtime"],
				["aaf_name"] = (string)group["aaf_name"],
				["type"] = (string)group["type"],
				["grade_id"] = grade_id,
				["grade_name"] = grade_name,
				["structure_id"] = (string)group["structure_id"],
				["ctime"] = (DateTime)group["ctime"]
			};

			if (expand)
				result["users"] = await GetGroupUsersAsync(db, id);
			return result;
		}

		public async Task<JsonValue> GetGroupAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetGroupAsync(db, id);
			}
		}

		public async Task<JsonValue> GetGroupAsync(DB db, int id)
		{
			var group = (await db.SelectAsync("SELECT * FROM `group` WHERE id=?", id)).First();
			return (group == null) ? null : await GroupToJsonAsync(db, group);
		}

		public async Task<JsonValue> CreateGroupAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await CreateGroupAsync(db, json);
			}
		}

		public async Task<JsonValue> CreateGroupAsync(DB db, JsonValue json)
		{
			JsonValue jsonResult = null;
			var extracted = json.ExtractFields(
				"name", "description", "type", "grade_id", "structure_id");
			// check required fields
			extracted.RequireFields("name", "type");

			int res = await db.InsertRowAsync("group", extracted);
			if (res == 1)
				jsonResult = await GetGroupAsync(db, (int)await db.LastInsertIdAsync());
			return jsonResult;
		}

		public async Task<JsonValue> ModifyGroupAsync(int id, JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await ModifyGroupAsync(db, id, json);
			}
		}

		public async Task<JsonValue> ModifyGroupAsync(DB db, int id, JsonValue json)
		{
			var extracted = json.ExtractFields("name", "structure_id", "type");
			if (extracted.Count > 0)
				await db.UpdateRowAsync("group", "id", id, extracted);
			return await GetGroupAsync(db, id);
		}

		public async Task<bool> DeleteGroupAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await DeleteGroupAsync(db, id);
			}
		}

		public async Task<bool> DeleteGroupAsync(DB db, int id)
		{
			return (await db.DeleteAsync("DELETE FROM `group` WHERE id=?", id)) != 0;
		}


		public async Task<JsonArray> GetGroupUsersAsync(DB db, int id)
		{
			var res = new JsonArray();
			foreach (var item in await db.SelectAsync("SELECT * FROM `group_user` WHERE group_id=?", id))
			{
				res.Add(new JsonObject
				{
					["id"] = (int)item["id"],
					["type"] = (string)item["type"],
					["user_id"] = (string)item["user_id"],
					["subject_id"] = (string)item["subject_id"],
					["ctime"] = (DateTime)item["ctime"],
					["aaf_mtime"] = (DateTime?)item["aaf_mtime"],
					["pending_validation"] = (bool)item["pending_validation"]
				});
			}
			return res;
		}

		public async Task<JsonArray> GetUserGroupsAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetUserGroupsAsync(db, id);
			}
		}

		public async Task<JsonArray> GetUserGroupsAsync(DB db, string id)
		{
			var res = new JsonArray();
			foreach (var item in await db.SelectAsync("SELECT * FROM `group_user` WHERE user_id=?", id))
			{
				res.Add(new JsonObject
				{
					["id"] = (int)item["id"],
					["type"] = (string)item["type"],
					["group_id"] = (int)item["group_id"],
					["subject_id"] = (string)item["subject_id"],
					["ctime"] = (DateTime)item["ctime"],
					["aaf_mtime"] = (DateTime?)item["aaf_mtime"],
					["pending_validation"] = (bool)item["pending_validation"]
				});
			}

/*			foreach (var inGroup in await db.SelectAsync("SELECT * FROM eleve_dans_regroupement WHERE user_id=?", id))
			{
				res.Add(new JsonObject
				{
					["type"] = "ELV",
					["regroupement_id"] = (int)inGroup["regroupement_id"]
				});
			}

			foreach (var inGroup in await db.SelectAsync("SELECT * FROM membre_regroupement_libre WHERE user_id=?", id))
			{
				res.Add(new JsonObject
				{
					["type"] = "MBR",
					["regroupement_id"] = (int)inGroup["regroupement_libre_id"],
					["regroupement_libre_id"] = (int)inGroup["regroupement_libre_id"],
					["joined_at"] = (DateTime?)inGroup["joined_at"]
				});
			}*/
			return res;
		}

		public async Task<JsonArray> GetStructureGroupsAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetStructureGroupsAsync(db, id);
			}
		}

		public async Task<JsonArray> GetStructureGroupsAsync(DB db, string id)
		{
			var res = new JsonArray();
			foreach (var group in await db.SelectAsync("SELECT * FROM `group` WHERE structure_id=?", id))
			{
				res.Add(await GroupToJsonAsync(db, group, false));
			}
			return res;
		}
	}
}
