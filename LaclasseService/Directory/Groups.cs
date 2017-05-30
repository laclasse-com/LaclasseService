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

	public class Groups : HttpRouting
	{
		readonly string dbUrl;
		readonly Grades grades;

		readonly static List<string> searchAllowedFields = new List<string> {
			"id", "name", "description", "aaf_mtime", "aaf_name", "type", "ctime", "structure_id",
			"users.user_id", "users.pending_validation", "users.type"
		};

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

			GetAsync["/"] = async (p, c) =>
			{
				int offset = 0;
				int count = -1;
				string orderBy = "name";
				SortDirection orderDir = SortDirection.Ascending;
				var query = "";
				if (c.Request.QueryString.ContainsKey("query"))
					query = c.Request.QueryString["query"];
				if (c.Request.QueryString.ContainsKey("limit"))
				{
					count = int.Parse(c.Request.QueryString["limit"]);
					if (c.Request.QueryString.ContainsKey("page"))
						offset = Math.Max(0, (int.Parse(c.Request.QueryString["page"]) - 1) * count);
				}
				if (c.Request.QueryString.ContainsKey("sort_col"))
					orderBy = c.Request.QueryString["sort_col"];
				if (c.Request.QueryString.ContainsKey("sort_dir") && (c.Request.QueryString["sort_dir"] == "desc"))
					orderDir = SortDirection.Descending;

				var parsedQuery = query.QueryParser();
				foreach (var key in c.Request.QueryString.Keys)
					if (searchAllowedFields.Contains(key) && !parsedQuery.ContainsKey(key))
						parsedQuery[key] = new List<string> { c.Request.QueryString[key] };
				foreach (var key in c.Request.QueryStringArray.Keys)
					if (searchAllowedFields.Contains(key) && !parsedQuery.ContainsKey(key))
						parsedQuery[key] = c.Request.QueryStringArray[key];
				var usersResult = await SearchGroupAsync(parsedQuery, orderBy, orderDir, offset, count);
				c.Response.StatusCode = 200;
				if (count > 0)
					c.Response.Content = new JsonObject
					{
						["total"] = usersResult.Total,
						["page"] = (usersResult.Offset / count) + 1,
						["data"] = usersResult.Data
					};
				else
					c.Response.Content = usersResult.Data;
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
				"name", "description", "type", "grade_id", "structure_id", "aaf_name");
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

		public async Task<SearchResult> SearchGroupAsync(
			string query, string orderBy = "name", SortDirection sortDir = SortDirection.Ascending,
			int offset = 0, int count = -1)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await SearchGroupAsync(
					db, query.QueryParser(), orderBy, sortDir, offset, count);
		}

		public async Task<SearchResult> SearchGroupAsync(
			Dictionary<string, List<string>> queryFields, string orderBy = "name",
			SortDirection sortDir = SortDirection.Ascending, int offset = 0, int count = -1)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await SearchGroupAsync(db, queryFields, orderBy, sortDir, offset, count);
		}

		public async Task<SearchResult> SearchGroupAsync(
			DB db, Dictionary<string, List<string>> queryFields, string orderBy = "name",
			SortDirection sortDir = SortDirection.Ascending, int offset = 0, int count = -1)
		{
			var result = new SearchResult();
			string filter = "";
			var tables = new Dictionary<string, Dictionary<string, List<string>>>();
			foreach (string key in queryFields.Keys)
			{
				if (!searchAllowedFields.Contains(key))
					continue;

				if (key.IndexOf('.') > 0)
				{
					var pos = key.IndexOf('.');
					var tableName = key.Substring(0, pos);
					var fieldName = key.Substring(pos + 1);
					Dictionary<string, List<string>> table;
					if (!tables.ContainsKey(tableName))
					{
						table = new Dictionary<string, List<string>>();
						tables[tableName] = table;
					}
					else
						table = tables[tableName];
					table[fieldName] = queryFields[key];
				}
				else
				{
					var words = queryFields[key];
					if (words.Count == 1)
					{
						if (filter != "")
							filter += " AND ";
						filter += "`" + key + "`='" + db.EscapeString(words[0]) + "'";
					}
					else if (words.Count > 1)
					{
						if (filter != "")
							filter += " AND ";
						filter += db.InFilter(key, words);
					}
				}
			}

			if (queryFields.ContainsKey("global"))
			{
				var words = queryFields["global"];
				foreach (string word in words)
				{
					if (filter != "")
						filter += " AND ";
					filter += "(";
					var first = true;
					foreach (var field in searchAllowedFields)
					{
						if (field.IndexOf('.') > 0)
							continue;
						if (first)
							first = false;
						else
							filter += " OR ";
						filter += "`" + field + "` LIKE '%" + db.EscapeString(word) + "%'";
					}
					filter += ")";
				}
			}

			foreach (string tableName in tables.Keys)
			{
				Console.WriteLine($"FOUND TABLE {tableName}");
				if (tableName == "users")
				{
					if (filter != "")
						filter += " AND ";
					filter += "id IN (SELECT group_id FROM `group_user` WHERE ";

					var first = true;
					var profilesTable = tables[tableName];
					foreach (var profilesKey in profilesTable.Keys)
					{
						var words = profilesTable[profilesKey];
						foreach (string word in words)
						{
							if (first)
								first = false;
							else
								filter += " AND ";
							filter += "`" + profilesKey + "`='" + db.EscapeString(word) + "'";
						}
					}
					filter += ")";
				}
			}

			if (filter == "")
				filter = "TRUE";
			string limit = "";
			if (count > 0)
				limit = $"LIMIT {count} OFFSET {offset}";

			result.Data = new JsonArray();
			var sql = $"SELECT SQL_CALC_FOUND_ROWS * FROM `group` WHERE {filter} " +
				$"ORDER BY `{orderBy}` " + ((sortDir == SortDirection.Ascending) ? "ASC" : "DESC") + $" {limit}";
			Console.WriteLine(sql);
			var items = await db.SelectAsync(sql);
			result.Total = (int)await db.FoundRowsAsync();

			foreach (var item in items)
			{
				result.Data.Add(await GroupToJsonAsync(db, item));
			}
			return result;
		}
	}
}
