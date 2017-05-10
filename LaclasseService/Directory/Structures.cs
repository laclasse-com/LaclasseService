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
		public int structure_type_id { get { return GetField("structure_type_id", 0); } set { SetField("structure_type_id", value); } }
		public string logo { get { return GetField<string>("logo", null); } set { SetField("logo", value); } }
		public bool aaf_sync_activated { get { return GetField("aaf_sync_activated", false); } set { SetField("aaf_sync_activated", value); } }
		public string private_ip { get { return GetField<string>("private_ip", null); } set { SetField("private_ip", value); } }
		public string educnat_marking_id { get { return GetField<string>("educnat_marking_id", null); } set { SetField("educnat_marking_id", value); } }
		public string url_blog { get { return GetField<string>("url_blog", null); } set { SetField("url_blog", value); } }
	}

	public class Structures : HttpRouting
	{
		readonly string dbUrl;
		readonly Groups groups;
		readonly Profils profils;
		// Users register itself
		internal Users users;

		public Structures(string dbUrl, Groups groups, Resources resources, Profils profils)
		{
			this.dbUrl = dbUrl;
			this.groups = groups;
			this.profils = profils;

			// register a type
			Types["uai"] = (val) => (Regex.IsMatch(val, "^[0-9]{7,7}[A-Z]$")) ? val : null;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/"] = async (p, c) =>
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

				var result = await SearchStructureAsync(query, orderBy, sortDir, offset, count);
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
				c.Response.StatusCode = 200;
				c.Response.Content = await CreateStructureAsync(json);
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

/*			GetAsync["/{uai:uai}/matieres"] = async (p, c) =>
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

			GetAsync["/{uai:uai}/users"] = async (p, c) =>
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
					queryFields["profils.structure_id"] = new List<string>(new string[] { (string)p["uai"] });
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
			};
		}

		async Task<JsonObject> StructureToJsonAsync(DB db, Dictionary<string, object> item)
		{
			var id = (string)item["id"];
			var jsonGroups = await groups.GetStructureGroupsAsync(db, id);
			//var classes = from g in jsonGroups where g["type_regroupement_id"] == "CLS" select g;
			//var groupes_eleves = from g in jsonGroups where g["type_regroupement_id"] == "GRP" select g;
			//var groupes_libres = from g in jsonGroups where g["type_regroupement_id"] == "GPL" select g;
			var jsonProfils = await profils.GetStructureProfilsAsync(db, id);

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
				["structure_type_id"] = (int)item["structure_type_id"],
				["resources"] = await GetStructureResourcesAsync(db, id),
				["groups"] = jsonGroups,
				//["classes"] = new JsonArray(classes),
				//["groupes_eleves"] = new JsonArray(groupes_eleves),
				//["groupes_libres"] = new JsonArray(groupes_libres),
				["profils"] = jsonProfils
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
			JsonValue jsonResult = null;
			var extracted = json.ExtractFields(
				"id", "name", "structure_type_id", "address", "zip_code");
			// check required fields
			if (!extracted.ContainsKey("id") || !extracted.ContainsKey("name") ||
			        !extracted.ContainsKey("structure_type_id"))
				return null;
			
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
				"structure_type_id"
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
			DB db, Dictionary<string, List<string>> queryFields, string orderBy = "name", 
			SortDirection sortDir = SortDirection.Ascending, int offset = 0, int count = -1)
		{
			var result = new SearchResult();
			var allowedFields = new List<string> {
				"id", "name", "address", "city", "zip_code"
			};
			string filter = "";
			var tables = new Dictionary<string, Dictionary<string, List<string>>>();
			foreach (string key in queryFields.Keys)
			{
				if (!allowedFields.Contains(key))
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
					foreach (string word in words)
					{
						if (filter != "")
							filter += " AND ";
						filter += "`" + key + "`='" + db.EscapeString(word) + "'";
					}
				}
			}
			foreach (string tableName in tables.Keys)
			{
				// TODO
			}

			Console.WriteLine(tables.Dump());
			if (filter == "")
				filter = "TRUE";
			string limit = "";
			if (count > 0)
				limit = $"LIMIT {count} OFFSET {offset}";

			result.Data = new JsonArray();
			var items = await db.SelectAsync(
				$"SELECT SQL_CALC_FOUND_ROWS * FROM structure WHERE {filter} "+
				$"ORDER BY `{orderBy}` "+((sortDir == SortDirection.Ascending)?"ASC":"DESC")+$" {limit}");
			result.Total = (int)await db.FoundRowsAsync();

			foreach (var item in items)
			{
				result.Data.Add(await StructureToJsonAsync(db, item));
			}
			return result;
		}
	}
}
