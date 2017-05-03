// Etablissements.cs
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class Etablissements : HttpRouting
	{
		readonly string dbUrl;
		readonly Groups groups;
		// Users register itself
		internal Users users;

		public Etablissements(string dbUrl, Groups groups, Resources resources)
		{
			this.dbUrl = dbUrl;
			this.groups = groups;

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
				var orderBy = "nom";
				if (c.Request.QueryString.ContainsKey("sort_col"))
					orderBy = c.Request.QueryString["sort_col"];
				var sortDir = SortDirection.Ascending;
				if (c.Request.QueryString.ContainsKey("sort_dir") && (c.Request.QueryString["sort_dir"] == "desc"))
					sortDir = SortDirection.Descending;

				var result = await SearchEtablissementAsync(query, orderBy, sortDir, offset, count);
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

			GetAsync["/types/types_etablissements"] = async (p, c) =>
			{
				var json = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var item in await db.SelectAsync("SELECT * FROM type_etablissement"))
					{
						json.Add(new JsonObject
						{
							["id"] = (int)item["id"],
							["nom"] = (string)item["nom"],
							["type_contrat"] = (string)item["type_contrat"],
							["libelle"] = (string)item["libelle"],
							["type_struct_aaf"] = (string)item["type_struct_aaf"]
						});
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = json;
			};

			GetAsync["/{uai:uai}"] = async (p, c) =>
			{
				var json = await GetEtablissementAsync((string)p["uai"]);
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
				c.Response.Content = await CreateEtablissementAsync(json);
			};

			PutAsync["/{uai:uai}"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				c.Response.StatusCode = 200;
				c.Response.Content = await ModifyEtablissementAsync((string)p["uai"], json);
			};

			DeleteAsync["/{uai:uai}"] = async (p, c) =>
			{
				bool done = await DeleteEtablissementAsync((string)p["uai"]);
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
					foreach (var item in await db.SelectAsync("SELECT * FROM type_etablissement"))
					{
						json.Add(new JsonObject
						{
							["id"] = (int)item["id"],
							["nom"] = (string)item["nom"],
							["type_contrat"] = (string)item["type_contrat"],
							["libelle"] = (string)item["libelle"],
							["type_struct_aaf"] = (string)item["type_struct_aaf"]
						});
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = json;
			};*/

			GetAsync["/{uai:uai}/ressources"] = async (p, c) =>
			{
				var json = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var item in await db.SelectAsync("SELECT * FROM etablissement_has_ressources_num"))
					{
						var jsonItem = await resources.GetResourceAsync((int)item["ressources_num_id"]);
						jsonItem["etablissement_id"] = (int)item["etablissement_id"];
						jsonItem["ressources_num_id"] = (int)item["ressources_num_id"];
						jsonItem["date_deb_abon"] = (DateTime?)item["date_deb_abon"];
						jsonItem["date_fin_abon"] = (DateTime?)item["date_fin_abon"];
						json.Add(jsonItem);
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = json;
			};

			PostAsync["/{uai:uai}/ressources"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				json.RequireFields("ressource_num_id");

				var jsonEtab = await GetEtablissementAsync((string)p["uai"]);
				if (jsonEtab == null)
					throw new WebException(404, "Etablissement " + (string)p["uai"] + " not found");

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					await db.InsertAsync("INSERT INTO etablissement_has_ressources_num (etablissement_id,ressources_num_id) VALUES (?,?)",
					               (int)jsonEtab["id"], (int)json["ressource_num_id"]);
				}
				c.Response.StatusCode = 200;
			};

			GetAsync["/{uai:uai}/classes"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var id = await GetEtablissementIdAsync(db, (string)p["uai"]);
					if (id == -1)
						throw new WebException(404, "Etablissement " + (string)p["uai"] + "not found");

					var jsonGroups = await groups.GetEtablissementGroupsAsync(db, id);
					var classes = from g in jsonGroups where g["type_regroupement_id"] == "CLS" select g;
					c.Response.StatusCode = 200;
					c.Response.Content = new JsonArray(classes);
				}
			};

			GetAsync["/{uai:uai}/groupes"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var id = await GetEtablissementIdAsync(db, (string)p["uai"]);
					if (id == -1)
						throw new WebException(404, "Etablissement " + (string)p["uai"] + "not found");

					var jsonGroups = await groups.GetEtablissementGroupsAsync(db, id);
					var classes = from g in jsonGroups where g["type_regroupement_id"] == "GRP" select g;
					c.Response.StatusCode = 200;
					c.Response.Content = new JsonArray(classes);
				}
			};

			GetAsync["/{uai:uai}/groupes_libres"] = async (p, c) =>
			{
				c.Response.StatusCode = 200;
				c.Response.Content = await groups.GetGroupsFreeAsync();
			};

			GetAsync["/{uai:uai}/users"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var id = await GetEtablissementIdAsync(db, (string)p["uai"]);
					if (id == -1)
						throw new WebException(404, "Etablissement " + (string)p["uai"] + " not found");

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
					queryFields["profils.etablissement_id"] = new List<string>(new string[] { id.ToString() });
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

		async Task<JsonObject> EtablissementToJsonAsync(DB db, Dictionary<string, object> item)
		{
			var id = (int)item["id"];
			var jsonGroups = await groups.GetEtablissementGroupsAsync(db, id);
			var classes = from g in jsonGroups where g["type_regroupement_id"] == "CLS" select g;
			var groupes_eleves = from g in jsonGroups where g["type_regroupement_id"] == "GRP" select g;
			var groupes_libres = from g in jsonGroups where g["type_regroupement_id"] == "GPL" select g;

			return new JsonObject
			{
				["id"] = id,
				["code_uai"] = (string)item["code_uai"],
				["nom"] = (string)item["nom"],
				["adresse"] = (string)item["adresse"],
				["code_postal"] = (string)item["code_postal"],
				["ville"] = (string)item["ville"],
				["telephone"] = (string)item["telephone"],
				["fax"] = (string)item["fax"],
				["date_last_maj_aaf"] = (DateTime?)item["date_last_maj_aaf"],
				["type_etablissement_id"] = (int)item["type_etablissement_id"],
				["resources"] = await GetEtablissementResourcesAsync(db, id),
				["groups"] = jsonGroups,
				["classes"] = new JsonArray(classes),
				["groupes_eleves"] = new JsonArray(groupes_eleves),
				["groupes_libres"] = new JsonArray(groupes_libres)
			};
		}

		public async Task<JsonValue> GetEtablissementAsync(string uai)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetEtablissementAsync(db, uai);
			}
		}

		public async Task<JsonValue> GetEtablissementAsync(DB db, string uai)
		{
			JsonValue res = null;
			var item = (await db.SelectAsync("SELECT * FROM etablissement WHERE code_uai=?", uai)).SingleOrDefault();
			if (item != null)
			{
				res = await EtablissementToJsonAsync(db, item);
			}
			return res;
		}

		public async Task<int> GetEtablissementIdAsync(string uai)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetEtablissementIdAsync(db, uai);
			}
		}

		public async Task<int> GetEtablissementIdAsync(DB db, string uai)
		{
			var item = (await db.SelectAsync("SELECT id FROM etablissement WHERE code_uai=?", uai)).SingleOrDefault();
			return (item != null) ? (int)item["id"] : -1;
		}

		public async Task<JsonValue> CreateEtablissementAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await CreateEtablissementAsync(db, json);
			}
		}

		public async Task<JsonValue> CreateEtablissementAsync(DB db, JsonValue json)
		{
			JsonValue jsonResult = null;
			var extracted = json.ExtractFields(
				"code_uai", "nom", "type_etablissement_id", "adresse",
				"code_postal", "adresse");
			// check required fields
			if (!extracted.ContainsKey("code_uai") || !extracted.ContainsKey("nom") ||
			        !extracted.ContainsKey("type_etablissement_id"))
				return null;
			
			int res = await db.InsertRowAsync("etablissement", extracted);
			if (res == 1)
				jsonResult = await GetEtablissementAsync(db, (string)extracted["code_uai"]);
			return jsonResult;
		}

		public async Task<JsonValue> ModifyEtablissementAsync(string uai, JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await ModifyEtablissementAsync(db, uai, json);
			}
		}

		public async Task<JsonValue> ModifyEtablissementAsync(DB db, string uai, JsonValue json)
		{
			var extracted = json.ExtractFields(
				"nom", "adresse", "code_postal", "ville", "telephone", "fax", "date_last_maj_aaf",
				"type_etablissement_id"
			);
			if (extracted.Count > 0)
				await db.UpdateRowAsync("etablissement", "code_uai", uai, extracted);
			return await GetEtablissementAsync(db, uai);
		}

		public async Task<bool> DeleteEtablissementAsync(string uai)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await DeleteEtablissementAsync(db, uai);
			}
		}

		public async Task<bool> DeleteEtablissementAsync(DB db, string uai)
		{
			return (await db.DeleteAsync("DELETE FROM etablissement WHERE code_uai=?", uai)) != 0;
		}

		public async Task<JsonArray> GetEtablissementResourcesAsync(DB db, int id)
		{
			var json = new JsonArray();
			foreach (var obj in await db.SelectAsync(
				"SELECT * FROM etablissement_has_ressources_num WHERE etablissement_id=?", id))
				json.Add((int)obj["ressources_num_id"]);
			return json;
		}

		public async Task<SearchResult> SearchEtablissementAsync(
			string query, string orderBy = "nom", SortDirection sortDir = SortDirection.Ascending,
			int offset = 0, int count = -1)
		{ 
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await SearchEtablissementAsync(
					db, query.QueryParser(), orderBy, sortDir, offset, count);
			}
		}

		public async Task<SearchResult> SearchEtablissementAsync(
			DB db, Dictionary<string, List<string>> queryFields, string orderBy = "nom", 
			SortDirection sortDir = SortDirection.Ascending, int offset = 0, int count = -1)
		{
			var result = new SearchResult();
			var allowedFields = new List<string> {
				"id", "nom", "code_uai", "adresse", "ville", "code_postal"
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
				$"SELECT SQL_CALC_FOUND_ROWS * FROM etablissement WHERE {filter} "+
				$"ORDER BY `{orderBy}` "+((sortDir == SortDirection.Ascending)?"ASC":"DESC")+$" {limit}");
			result.Total = (int)await db.FoundRowsAsync();

			foreach (var item in items)
			{
				result.Data.Add(await EtablissementToJsonAsync(db, item));
			}
			return result;
		}
	}
}
