// Structures.cs
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
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "structure", PrimaryKey = "id")]
	public class Structure : Model
	{
		public string id { get { return GetField<string>("id", null); } set { SetField("id", value); } }
		public string name { get { return GetField<string>("name", null); } set { SetField("name", value); } }
		public string siren { get { return GetField<string>("siren", null); } set { SetField("siren", value); } }
		public string address { get { return GetField<string>("address", null); } set { SetField("address", value); } }
		public string zip_code { get { return GetField<string>("zip_code", null); } set { SetField("zip_code", value); } }
		public string city { get { return GetField<string>("city", null); } set { SetField("city", value); } }
		public string phone { get { return GetField<string>("phone", null); } set { SetField("phone", value); } }
		public string fax { get { return GetField<string>("fax", null); } set { SetField("fax", value); } }
		public double longitude { get { return GetField<double>("longitude", 0); } set { SetField("longitude", value); } }
		public double latitude { get { return GetField<double>("latitude", 0); } set { SetField("latitude", value); } }
		public DateTime? aaf_mtime { get { return GetField<DateTime?>("aaf_mtime", null); } set { SetField("aaf_mtime", value); } }
		public string domain { get { return GetField<string>("domain", null); } set { SetField("domain", value); } }
		public string public_ip { get { return GetField<string>("public_ip", null); } set { SetField("public_ip", value); } }
		public int type { get { return GetField("type", 0); } set { SetField("type", value); } }
		public bool aaf_sync_activated { get { return GetField("aaf_sync_activated", false); } set { SetField("aaf_sync_activated", value); } }
		public string private_ip { get { return GetField<string>("private_ip", null); } set { SetField("private_ip", value); } }
		public string educnat_marking_id { get { return GetField<string>("educnat_marking_id", null); } set { SetField("educnat_marking_id", value); } }
		public string url_blog { get { return GetField<string>("url_blog", null); } set { SetField("url_blog", value); } }
	}

	public class Structures : HttpRouting
	{
		readonly string dbUrl;
		readonly Groups groups;
		readonly Profiles profiles;
		// Users register itself
		internal Users users;

		readonly static List<string> searchAllowedFields = new List<string> {
			"id", "name", "address", "city", "zip_code", "profiles.type", "profiles.user_id"
		};

		public Structures(string dbUrl, Groups groups, Resources resources, Profiles profiles)
		{
			this.dbUrl = dbUrl;
			this.groups = groups;
			this.profiles = profiles;

			// register a type
			//Types["uai"] = (val) => (Regex.IsMatch(val, "^[0-9]{7,7}[A-Z]$")) ? val : null;
			Types["uai"] = (val) => (Regex.IsMatch(val, "^[0-9A-Z]+$")) ? val : null;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/"] = async (p, c) =>
			{

				Console.WriteLine("DANIEL CONTAINS ID: " + c.Request.QueryStringArray.ContainsKey("id"));

				if (c.Request.QueryStringArray.ContainsKey("id"))
				{
					var result = new JsonArray();
					using (DB db = await DB.CreateAsync(dbUrl))
					{
						var items = await db.SelectAsync("SELECT * FROM structure WHERE " + db.InFilter("id", c.Request.QueryStringArray["id"]));
						foreach (var item in items)
							result.Add(await StructureToJsonAsync(db, item));
						
						c.Response.StatusCode = 200;
						c.Response.Content = result;
					}
				}
				else
				{
					int offset = 0;
					int count = -1;
					var query = "";
					if (c.Request.QueryString.ContainsKey("query"))
						query = c.Request.QueryString["query"];
					if (c.Request.QueryString.ContainsKey("limit"))
					{
						count = int.Parse(c.Request.QueryString["limit"]);
						if (c.Request.QueryString.ContainsKey("page"))
							offset = Math.Max(0, (int.Parse(c.Request.QueryString["page"]) - 1) * count);
					}
					var orderBy = "name";
					if (c.Request.QueryString.ContainsKey("sort_col"))
						orderBy = c.Request.QueryString["sort_col"];
					var sortDir = SortDirection.Ascending;
					if (c.Request.QueryString.ContainsKey("sort_dir") && (c.Request.QueryString["sort_dir"] == "desc"))
						sortDir = SortDirection.Descending;

					var parsedQuery = query.QueryParser();
					foreach (var key in c.Request.QueryString.Keys)
						if (searchAllowedFields.Contains(key) && !parsedQuery.ContainsKey(key))
							parsedQuery[key] = new List<string> { c.Request.QueryString[key] };
					foreach (var key in c.Request.QueryStringArray.Keys)
						if (searchAllowedFields.Contains(key) && !parsedQuery.ContainsKey(key))
							parsedQuery[key] = c.Request.QueryStringArray[key];

					var result = await SearchStructureAsync(parsedQuery, orderBy, sortDir, offset, count);
					c.Response.StatusCode = 200;
					if (count > 0)
						c.Response.Content = new JsonObject
						{
							["total"] = result.Total,
							["page"] = (result.Offset / count) + 1,
							["data"] = result.Data
						};
					else
						c.Response.Content = result.Data;
				}
			};

			GetAsync["/{uai:uai}"] = async (p, c) =>
			{
				var json = await GetStructureAsync((string)p["uai"]);
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
				var jsonArray = json as JsonArray;
				if (jsonArray != null)
				{
					var result = new JsonArray();
					foreach (var etabJson in jsonArray)
					{
						result.Add(await CreateStructureAsync(etabJson));
					}
					c.Response.StatusCode = 200;
					c.Response.Content = result;
				}
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = await CreateStructureAsync(json);
				}
			};

			PutAsync["/{uai:uai}"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				c.Response.StatusCode = 200;
				c.Response.Content = await ModifyStructureAsync((string)p["uai"], json);
			};

			DeleteAsync["/{uai:uai}"] = async (p, c) =>
			{
				bool done = await DeleteStructureAsync((string)p["uai"]);
				if (done)
					c.Response.StatusCode = 200;
				else
					c.Response.StatusCode = 404;
			};

			DeleteAsync["/"] = async (p, c) =>
			{				
				var json = await c.Request.ReadAsJsonAsync();
				var jsonArray = json as JsonArray;
				if (jsonArray != null)
				{
					using (DB db = await DB.CreateAsync(dbUrl))
					{
						bool allDone = true;
						foreach (string uai in jsonArray)
							allDone &= await DeleteStructureAsync(uai);
						c.Response.StatusCode = allDone ? 200 : 404;
					}
				}
			};


/*			GetAsync["/{uai:uai}/subjects"] = async (p, c) =>
			{
				var json = new JsonArray();
				// TODO
				using (DB db = await DB.CreateAsync(DBUrl))
				{
					foreach (var item in await db.SelectAsync("SELECT * FROM structure_type"))
					{
						json.Add(new JsonObject
						{
							["id"] = (int)item["id"],
							["name"] = (string)item["name"],
							["contrat_type"] = (string)item["contrat_type"],
							["name"] = (string)item["name"],
							["aaf_type"] = (string)item["aaf_type"]
						});
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = json;
			};*/

			GetAsync["/{uai:uai}/resources"] = async (p, c) =>
			{
				var json = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var item in await db.SelectAsync("SELECT * FROM structure_resource WHERE structure_id=?",(string)p["uai"]))
					{
						var jsonItem = await resources.GetResourceAsync((int)item["resource_id"]);
						jsonItem["structure_id"] = (string)item["structure_id"];
						jsonItem["resource_id"] = (int)item["resource_id"];
						json.Add(jsonItem);
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = json;
			};

			PostAsync["/{uai:uai}/resources"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				json.RequireFields("resource_id");

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					await db.InsertAsync("INSERT INTO structure_resource (structure_id,resource_id) VALUES (?,?)",
					                     (string)p["uai"], (int)json["resource_id"]);
				}
				c.Response.StatusCode = 200;
			};
			/*
			GetAsync["/{uai:uai}/classes"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var jsonGroups = await groups.GetStructureGroupsAsync(db, (string)p["uai"]);
					var classes = from g in jsonGroups where g["type_regroupement_id"] == "CLS" select g;
					c.Response.StatusCode = 200;
					c.Response.Content = new JsonArray(classes);
				}
			};*/

			GetAsync["/{uai:uai}/groups"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var jsonGroups = await groups.GetStructureGroupsAsync(db, (string)p["uai"]);
					//var classes = from g in jsonGroups where g["type_regroupement_id"] == "GRP" select g;
					c.Response.StatusCode = 200;
					c.Response.Content = new JsonArray(jsonGroups);
				}
			};

/*			GetAsync["/{uai:uai}/groupes_libres"] = async (p, c) =>
			{
				c.Response.StatusCode = 200;
				c.Response.Content = await groups.GetGroupsFreeAsync();
			};*/

			/*GetAsync["/{uai:uai}/users"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int offset = 0;
					int count = -1;
					var query = "";
					if (c.Request.QueryString.ContainsKey("query"))
						query = c.Request.QueryString["query"];
					if (c.Request.QueryString.ContainsKey("limit"))
					{
						count = int.Parse(c.Request.QueryString["limit"]);
						if (c.Request.QueryString.ContainsKey("page"))
							offset = Math.Max(0, (int.Parse(c.Request.QueryString["page"]) - 1) * count);
					}
					var queryFields = query.QueryParser();
					queryFields["profiles.structure_id"] = new List<string>(new string[] { (string)p["uai"] });
					var usersResult = await users.SearchUserAsync(queryFields, offset, count);
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
				}
			};*/
		}

		async Task<JsonObject> StructureToJsonAsync(DB db, Dictionary<string, object> item)
		{
			var id = (string)item["id"];
			var jsonGroups = await groups.GetStructureGroupsAsync(db, id);
			//var classes = from g in jsonGroups where g["type_regroupement_id"] == "CLS" select g;
			//var groupes_eleves = from g in jsonGroups where g["type_regroupement_id"] == "GRP" select g;
			//var groupes_libres = from g in jsonGroups where g["type_regroupement_id"] == "GPL" select g;
			var jsonProfiles = await profiles.GetStructureProfilesAsync(db, id);

			return new JsonObject
			{
				["id"] = id,
				["name"] = (string)item["name"],
				["address"] = (string)item["address"],
				["zip_code"] = (string)item["zip_code"],
				["city"] = (string)item["city"],
				["phone"] = (string)item["phone"],
				["fax"] = (string)item["fax"],
				["aaf_mtime"] = (DateTime?)item["aaf_mtime"],
				["type"] = (int)item["type"],
				["siren"] = (string)item["siren"],
				["longitude"] = (double?)item["longitude"],
				["latitude"] = (double?)item["latitude"],
				["domain"] = (string)item["domain"],
				["resources"] = await GetStructureResourcesAsync(db, id),
				["groups"] = jsonGroups,
				//["classes"] = new JsonArray(classes),
				//["groupes_eleves"] = new JsonArray(groupes_eleves),
				//["groupes_libres"] = new JsonArray(groupes_libres),
				["profiles"] = jsonProfiles
			};
		}

		public async Task<JsonValue> GetStructureAsync(string uai)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetStructureAsync(db, uai);
			}
		}

		public async Task<JsonValue> GetStructureAsync(DB db, string uai)
		{
			JsonValue res = null;
			var item = (await db.SelectAsync("SELECT * FROM structure WHERE id=?", uai)).SingleOrDefault();
			if (item != null)
			{
				res = await StructureToJsonAsync(db, item);
			}
			return res;
		}

		public async Task<JsonValue> CreateStructureAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await CreateStructureAsync(db, json);
			}
		}

		public async Task<JsonValue> CreateStructureAsync(DB db, JsonValue json)
		{
			json.RequireFields("id", "name", "type");
			JsonValue jsonResult = null;
			var extracted = json.ExtractFields(
				"id", "name", "type", "address", "zip_code", "siren", "city", "phone", "fax",
				"longitude", "latitude", "domain", "public_ip", "private_ip");
			
			int res = await db.InsertRowAsync("structure", extracted);
			if (res == 1)
				jsonResult = await GetStructureAsync(db, (string)extracted["id"]);
			return jsonResult;
		}

		public async Task<JsonValue> ModifyStructureAsync(string uai, JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await ModifyStructureAsync(db, uai, json);
			}
		}

		public async Task<JsonValue> ModifyStructureAsync(DB db, string id, JsonValue json)
		{
			var extracted = json.ExtractFields(
				"name", "address", "zip_code", "city", "phone", "fax", "aaf_mtime",
				"type", "siren", "longitude", "latitude", "domain", "public_ip", "private_ip"
			);

			if (extracted.Count > 0)
				await db.UpdateRowAsync("structure", "id", id, extracted);
			return await GetStructureAsync(db, id);
		}

		public async Task<bool> DeleteStructureAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await DeleteStructureAsync(db, id);
			}
		}

		public async Task<bool> DeleteStructureAsync(DB db, string id)
		{
			return (await db.DeleteAsync("DELETE FROM structure WHERE id=?", id)) != 0;
		}

		public async Task<JsonArray> GetStructureResourcesAsync(DB db, string id)
		{
			var json = new JsonArray();
			foreach (var obj in await db.SelectAsync(
				"SELECT * FROM structure_resource WHERE structure_id=?", id))
				json.Add((int)obj["resource_id"]);
			return json;
		}

		public async Task<SearchResult> SearchStructureAsync(
			string query, string orderBy = "name", SortDirection sortDir = SortDirection.Ascending,
			int offset = 0, int count = -1)
		{ 
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await SearchStructureAsync(
					db, query.QueryParser(), orderBy, sortDir, offset, count);
			}
		}

		public async Task<SearchResult> SearchStructureAsync(
			Dictionary<string, List<string>> queryFields, string orderBy = "name",
			SortDirection sortDir = SortDirection.Ascending, int offset = 0, int count = -1)
		{ 
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await SearchStructureAsync(db, queryFields, orderBy, sortDir, offset, count);
			}
		}

		public async Task<SearchResult> SearchStructureAsync(
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
					var tableName = key.Substring(0, pos - 1);
					var fieldName = key.Substring(pos + 1);
					Console.WriteLine($"FOUND TABLE: {tableName}, FIELD: {fieldName}");
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
				if (tableName == "profiles")
				{
					if (filter != "")
						filter += " AND ";
					filter += "id IN (SELECT structure_id FROM user_profile WHERE ";

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
			var sql = $"SELECT SQL_CALC_FOUND_ROWS * FROM structure WHERE {filter} " +
				$"ORDER BY `{orderBy}` " + ((sortDir == SortDirection.Ascending) ? "ASC" : "DESC") + $" {limit}";
			Console.WriteLine(sql);
			var items = await db.SelectAsync(sql);
			result.Total = (int)await db.FoundRowsAsync();

			foreach (var item in items)
			{
				result.Data.Add(await StructureToJsonAsync(db, item));
			}
			return result;
		}
	}
}
