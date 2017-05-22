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
	[Model(Table = "group", PrimaryKey = "id")]
	public class Group : Model
	{
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField]
		public string name { get { return GetField<string>("name", null); } set { SetField("name", value); } }
		[ModelField]
		public string description { get { return GetField<string>("description", null); } set { SetField("description", value); } }
		[ModelField]
		public DateTime? aaf_mtime { get { return GetField<DateTime?>("aaf_mtime", null); } set { SetField("aaf_mtime", value); } }
		[ModelField]
		public string aaf_name { get { return GetField<string>("aaf_name", null); } set { SetField("aaf_name", value); } }
		[ModelField(Required = true)]
		public string type { get { return GetField<string>("type", null); } set { SetField("type", value); } }
		[ModelField]
		public string structure_id { get { return GetField<string>("structure_id", null); } set { SetField("structure_id", value); } }
		[ModelField]
		public DateTime? ctime { get { return GetField<DateTime?>("ctime", null); } set { SetField("ctime", value); } }

		public async Task<ModelList<GroupGrade>> GetGradesAsync(DB db)
		{
			return await db.SelectAsync<GroupGrade>("SELECT * FROM `group_grade` WHERE `group_id`=?", id);
		}
	}

	[Model(Table = "group_grade", PrimaryKey = "id")]
	public class GroupGrade : Model
	{
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField(Required = true)]
		public int group_id { get { return GetField("group_id", 0); } set { SetField("group_id", value); } }
		[ModelField(Required = true)]
		public string grade_id { get { return GetField<string>("grade_id", null); } set { SetField("grade_id", value); } }
	}

	[Model(Table = "group_user", PrimaryKey = "id")]
	public class GroupUser : Model
	{
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField]
		public string type { get { return GetField<string>("type", null); } set { SetField("type", value); } }
		[ModelField(Required = true)]
		public int group_id { get { return GetField("group_id", 0); } set { SetField("group_id", value); } }
		[ModelField(Required = true)]
		public string user_id { get { return GetField<string>("user_id", null); } set { SetField("user_id", value); } }
		[ModelField]
		public string subject_id { get { return GetField<string>("subject_id", null); } set { SetField("subject_id", value); } }
		[ModelField]
		public DateTime ctime { get { return GetField("ctime", DateTime.Now); } set { SetField("ctime", value); } }
		[ModelField]
		public DateTime? aaf_mtime { get { return GetField<DateTime?>("aaf_mtime", null); } set { SetField("aaf_mtime", value); } }
		[ModelField]
		public bool pending_validation { get { return GetField("pending_validation", false); } set { SetField("pending_validation", value); } }
	}

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
			var groupGrades = await GetGroupGradesAsync(db, id);

			var result = new JsonObject
			{
				["id"] = id,
				["name"] = (string)group["name"],
				["description"] = (string)group["description"],
				["aaf_mtime"] = (DateTime?)group["aaf_mtime"],
				["aaf_name"] = (string)group["aaf_name"],
				["type"] = (string)group["type"],
				["structure_id"] = (string)group["structure_id"],
				["ctime"] = (DateTime)group["ctime"],
				["grades"] = groupGrades
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


		public async Task<ModelList<GroupUser>> GetGroupUsersAsync(DB db, int id)
		{
			return await db.SelectAsync<GroupUser>("SELECT * FROM `group_user` WHERE group_id=?", id);
		}

		public async Task<JsonArray> GetUserGroupsAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await GetUserGroupsAsync(db, id);
		}

		public async Task<ModelList<GroupUser>> GetUserGroupsAsync(DB db, string id)
		{
			return await db.SelectAsync<GroupUser>("SELECT * FROM `group_user` WHERE user_id=?", id);

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
		}

		public async Task<ModelList<Group>> GetStructureGroupsAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await GetStructureGroupsAsync(db, id);
		}

		public async Task<ModelList<Group>> GetStructureGroupsAsync(DB db, string id)
		{
			return await db.SelectAsync<Group>("SELECT * FROM `group` WHERE structure_id=?", id);
		}

		public async Task<ModelList<GroupGrade>> GetGroupGradesAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await GetGroupGradesAsync(db, id);
		}

		public async Task<ModelList<GroupGrade>> GetGroupGradesAsync(DB db, int id)
		{
			return await db.SelectAsync<GroupGrade>("SELECT * FROM `group_grade` WHERE `group_id`=?", id);
		}
	}
}
