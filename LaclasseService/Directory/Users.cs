// Users.cs
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
using System.IO;
using System.Linq;
using Dir = System.IO.Directory;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class Users : HttpRouting
	{
		readonly string dbUrl;
		readonly Emails emails;
		readonly Profils profils;
		readonly Groups groups;
		readonly Etablissements etabs;
		readonly Resources resources;
		readonly string masterPassword;

		public Users(string dbUrl, Emails emails, Profils profils, Groups groups, Etablissements etabs,
		             Resources resources, string storageDir, string masterPassword)
		{
			this.dbUrl = dbUrl;
			this.emails = emails;
			this.profils = profils;
			this.groups = groups;
			this.etabs = etabs;
			this.etabs.users = this;
			this.resources = resources;
			this.masterPassword = masterPassword;

			var avatarDir = Path.Combine(storageDir, "avatar");

			if (!Dir.Exists(avatarDir))
				Dir.CreateDirectory(avatarDir);

			// register a type
			Types["uid"] = (val) => (Regex.IsMatch(val, "^[A-Z0-9]+$")) ? val : null;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/"] = async (p, c) =>
			{
				int offset = 0;
				int count = -1;
				string orderBy = "nom";
				OrderDirection orderDir = OrderDirection.Ascending;
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
					orderDir = OrderDirection.Descending;
					
				var usersResult = await SearchUserAsync(query, offset, count, orderBy, orderDir);
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

			GetAsync["/current"] = async (p, c) =>
			{
				var user = await c.GetAuthenticatedUserAsync();
				if (user == null)
					c.Response.StatusCode = 401;
				else
				{
					c.Response.StatusCode = 302;
					c.Response.Headers["location"] = user;
				}
			};

			GetAsync["/{uid:uid}"] = async (p, c) =>
			{
				var json = await GetUserAsync((string)p["uid"]);
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
				if (json is JsonArray)
				{
					var jsonResult = new JsonArray();
					using (DB db = await DB.CreateAsync(dbUrl))
					{
						foreach (var jsonItem in (JsonArray)json)
							jsonResult.Add(await CreateUserAsync(jsonItem));
					}
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = await CreateUserAsync(json);
				}
			};

			PutAsync["/{uid:uid}"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				c.Response.StatusCode = 200;
				c.Response.Content = await ModifyUserAsync((string)p["uid"], json);
			};

			DeleteAsync["/{uid:uid}"] = async (p, c) =>
			{
				bool done = await DeleteUserAsync((string)p["uid"]);
				if (done)
					c.Response.StatusCode = 200;
				else
					c.Response.StatusCode = 404;
			};

			PostAsync["/{uid:uid}/upload/avatar"] = async (p, c) =>
			{
				var uid = (string)p["uid"];
				var oldUser = await GetUserAsync(uid);
				if (oldUser == null)
					return;

				var reader = c.Request.ReadAsMultipart();
				MultipartPart part;
				while((part = await reader.ReadPartAsync()) != null)
				{
					if (part.Headers.ContainsKey("content-disposition") && part.Headers.ContainsKey("content-type"))
					{
						if ((part.Headers["content-type"] != "image/jpeg") &&
							(part.Headers["content-type"] != "image/png") &&
							(part.Headers["content-type"] != "image/svg+xml"))
							continue;

						var disposition = ContentDisposition.Decode(part.Headers["content-disposition"]);
						if (disposition.ContainsKey("name") && (disposition["name"] == "image"))
						{
							var dir = DirExt.CreateRecursive(Path.Combine(
								avatarDir, uid[0].ToString(), uid[1].ToString(), uid[2].ToString()));
							var ext = ".jpg";
							if (part.Headers["content-type"] == "image/png")
								ext = ".png";
							else if (part.Headers["content-type"] == "image/svg+xml")
								ext = ".svg";

							var name = StringExt.RandomString(16) + "_" + uid + ext;

							// get and save the image
							using (var stream = File.OpenWrite(Path.Combine(dir.FullName, name)))
							{
								await part.Stream.CopyToAsync(stream);
							}
							c.Response.StatusCode = 200;
							c.Response.Content = await ModifyUserAsync(uid, new JsonObject { ["avatar"] = name });

							if ((oldUser["avatar"] != null) && (oldUser["avatar"] != "empty"))
							{
								var oldFile = Path.Combine(avatarDir, uid[0].ToString(), uid[1].ToString(), uid[2].ToString(), Path.GetFileName(oldUser["avatar"]));
								if(File.Exists(oldFile))
									File.Delete(oldFile);
							}
						}
					}
				}
			};

			PutAsync["/{uid:uid}/profil_actif"] = async (p, c) =>
			{
				if (!c.Request.QueryString.ContainsKey("profil_id") || !c.Request.QueryString.ContainsKey("uai"))
					throw new WebException(400, "Bad protocol");

				var uid = (string)p["uid"];
				var profil_id = c.Request.QueryString["profil_id"];
				var uai = c.Request.QueryString["uai"];
				var etabId = await etabs.GetEtablissementIdAsync(uai);
				if (etabId == -1)
					throw new WebException(404, $"Etablissement {uai} not found");
				
				using (DB db = await DB.CreateAsync(dbUrl, true))
				{
					var userId = await GetUserIdAsync(db, uid);
					if (userId == -1)
						throw new WebException(404, $"User not found");

					var count = await db.UpdateAsync(
						"UPDATE profil_user SET actif=TRUE WHERE user_id=? AND profil_id=? AND etablissement_id=?",
						userId, profil_id, etabId);
					if (count < 1)
						throw new WebException(404, $"Profil not found");
					
					await db.UpdateAsync(
						"UPDATE profil_user SET actif=FALSE WHERE user_id=? AND (profil_id != ? OR etablissement_id != ?)",
						userId, profil_id, etabId);

					c.Response.StatusCode = 200;
					c.Response.Content = await GetUserAsync(uid);
				}
			};
		}

		public async Task<JsonObject> UserToJsonAsync(DB db, Dictionary<string, object> item, bool expand = true)
		{
			var id = (int)item["id"];
			var uid = (string)item["id_ent"];
			string avatar;
			if (((string)item["avatar"] == null) || ((string)item["avatar"] == "empty"))
				avatar = "api/default_avatar/avatar_neutre.svg";
			else
				avatar = "api/avatar/user/" + 
					uid.Substring(0, 1) + "/" + uid.Substring(1, 1) + "/" +
				    uid.Substring(2, 1) + "/" + (string)item["avatar"];

			var result = new JsonObject
			{
				["id"] = id,
				["id_sconet"] = (int?)item["id_sconet"],
				["id_jointure_aaf"] = (long?)item["id_jointure_aaf"],
				["login"] = (string)item["login"],
				["nom"] = (string)item["nom"],
				["prenom"] = (string)item["prenom"],
				["full_name"] = (string)item["nom"] + " " + (string)item["prenom"],
				["sexe"] = (string)item["sexe"],
				["date_naissance"] = (DateTime?)item["date_naissance"],
				["adresse"] = (string)item["adresse"],
				["ville"] = (string)item["ville"],
				["sexe"] = (string)item["sexe"],
				["code_postal"] = (string)item["code_postal"],
				["date_creation"] = (DateTime?)item["date_creation"],
				["date_derniere_connexion"] = (DateTime?)item["date_derniere_connexion"],
				["default_password"] = (bool)item["change_password"],
				["default_password_value"] = ((bool)item["change_password"]) ? (string)item["id_ent"] : null,
				["id_ent"] = (string)item["id_ent"],
				["avatar"] = avatar,
				["telephones"] = await GetUserTelephonesAsync(db, id),
				["emails"] = await emails.GetUserEmailsAsync(db, (string)item["id_ent"])
			};
			if (expand)
			{
				var profilsJson = await profils.GetUserProfilsAsync(db, id);
				var profilActif = (from p in profilsJson where p["actif"] select p).SingleOrDefault();

				var roles = await GetUserRolesAsync(db, id);
				int rolesMaxPriorityEtabActif = 0;
				if (profilActif != null)
				{
					foreach (var obj in roles)
					{
						if ((obj["etablissement_id"] == profilActif["etablissement_id"]) &&
							(obj["priority"] > rolesMaxPriorityEtabActif))
							rolesMaxPriorityEtabActif = obj["priority"];
					}
				}
				var etabsJson = new JsonArray();
				foreach (var obj in profilsJson)
				{
					var etab = etabsJson.FirstOrDefault((arg) => arg["id"] == obj["etablissement_id"]);
					if (etab == null)
					{
						etab = new JsonObject
						{
							["id"] = (int)obj["etablissement_id"],
							["nom"] = (string)obj["etablissement_nom"],
							["code_uai"] = (string)obj["etablissement_code_uai"],
							["profils"] = new JsonArray(),
							["roles"] = new JsonArray()
						};
						etabsJson.Add(etab);
					}
					((JsonArray)etab["profils"]).Add(obj);
				}

				foreach (var obj in roles)
				{
					var etab = etabsJson.FirstOrDefault((arg) => arg["id"] == obj["etablissement_id"]);
					if (etab == null)
					{
						etab = new JsonObject
						{
							["id"] = (int)obj["etablissement_id"],
							["nom"] = (string)obj["etablissement_nom"],
							["code_uai"] = (string)obj["etablissement_code_uai"],
							["profils"] = new JsonArray(),
							["roles"] = new JsonArray()
						};
						etabsJson.Add(etab);
					}
					((JsonArray)etab["roles"]).Add(obj);
				}

				var ressources_numeriques = new JsonArray();
				foreach (var etab in etabsJson)
				{
					var etabResources = await etabs.GetEtablissementResourcesAsync(db, etab["id"]);
					foreach (var resource in etabResources)
					{
						var r = await resources.GetResourceAsync(db, resource);
						r["etablissement_code_uai"] = (string)etab["code_uai"];
						ressources_numeriques.Add(r);
					}
				}

				var groupsJson = await groups.GetUserGroupsAsync(db, id);

				var groupsList = new List<JsonValue>();
				foreach (var g in groupsJson)
				{
					if (g.ContainsKey("regroupement_libre_id"))
						groupsList.Add(await groups.GetGroupFreeAsync(g["regroupement_libre_id"]));
					else
						groupsList.Add(await groups.GetGroupAsync(g["regroupement_id"]));
				}

				var classes = from g in groupsList where g["type_regroupement_id"] == "CLS" select g;
				var groupes_eleves = from g in groupsList where g["type_regroupement_id"] == "GRP" select g;
				var groupes_libres = from g in groupsList where g["type_regroupement_id"] == "GPL" select g;

				var relations_adultes = await GetUserRelationsParentsAsync(db, id);
				var parentsJson = new JsonArray();
				foreach (var r in relations_adultes)
					parentsJson.Add(await GetUserAsync(db, r["id_ent"], false));

				var relations_eleves = await GetUserRelationsElevesAsync(db, id);
				var enfantsJson = new JsonArray();
				foreach (var r in relations_eleves)
					enfantsJson.Add(await GetUserAsync(db, r["id_ent"], false));

				result["roles"] = roles;
				result["roles_max_priority_etab_actif"] = rolesMaxPriorityEtabActif;
				result["etablissements"] = etabsJson;
				result["profil_actif"] = profilActif;
				result["profils"] = profilsJson;
				result["groups"] = groupsJson;
				result["ressources_numeriques"] = ressources_numeriques;
				result["classes"] = new JsonArray(classes);
				result["groupes_libres"] = new JsonArray(groupes_libres);
				result["groupes_eleves"] = new JsonArray(groupes_eleves);
				result["relations_eleves"] = relations_eleves;
				result["relations_adultes"] = relations_adultes;
				result["parents"] = parentsJson;
				result["enfants"] = enfantsJson;
				result["email_backend_id"] = (int)item["email_backend_id"];
			}
			return result;
		}

		public async Task<int> GetUserIdAsync(DB db, string uid)
		{
			var item = (await db.SelectAsync("SELECT id FROM user WHERE id_ent=?", uid)).SingleOrDefault();
			return (item != null) ? (int)item["id"] : -1;
		}

		public async Task<JsonValue> GetUserAsync(string uid)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetUserAsync(db, uid);
			}
		}

		public async Task<JsonValue> GetUserAsync(DB db, string uid, bool expand = true)
		{
			var item = (await db.SelectAsync("SELECT * FROM user WHERE id_ent=?", uid)).First();
			return (item == null) ? null : await UserToJsonAsync(db, item, expand);
		}

		public async Task<SearchResult> SearchUserAsync(
			string query, int offset = 0, int count = -1, string orderBy = null,
			OrderDirection orderDirection = OrderDirection.Ascending)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await SearchUserAsync(db, query, offset, count, orderBy, orderDirection);
			}
		}

		public Task<SearchResult> SearchUserAsync(
			DB db, string query, int offset = 0, int count = -1, string orderBy = null,
			OrderDirection orderDirection = OrderDirection.Ascending)
		{
			return SearchUserAsync(db, query.QueryParser(), offset, count, orderBy, orderDirection);
		}

		public async Task<SearchResult> SearchUserAsync(
			Dictionary<string, List<string>> queryFields, int offset = 0, int count = -1,
			string orderBy = null, OrderDirection orderDirection = OrderDirection.Ascending)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await SearchUserAsync(db, queryFields, offset, count, orderBy, orderDirection);
			}
		}

		public enum OrderDirection
		{
			Ascending,
			Descending
		}

		public async Task<SearchResult> SearchUserAsync(
			DB db, Dictionary<string, List<string>> queryFields, int offset = 0, int count = -1,
			string orderBy = null, OrderDirection orderDirection = OrderDirection.Ascending)
		{
			var result = new SearchResult();
			var allowedFields = new List<string> {
				"id_ent", "login", "prenom", "nom", "sexe", "adresse", "ville", "code_postal",
				"id_sconet", "change_password", "profils.profil_id", "profils.etablissement_id",
				"emails.adresse", "emails.type"
			};

			if ((orderBy == null) || (!allowedFields.Contains(orderBy)))
				orderBy = "nom";

			string filter = "";
			var tables = new Dictionary<string, Dictionary<string, List<string>>>();
			foreach (string key in queryFields.Keys)
			{
				if (!allowedFields.Contains(key))
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
					foreach (var field in allowedFields)
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
				if (tableName == "profils")
				{
					if (filter != "")
						filter += " AND ";
					filter += "id IN (SELECT user_id FROM profil_user WHERE ";

					var first = true;
					var profilsTable = tables[tableName];
					foreach (var profilsKey in profilsTable.Keys)
					{
						var words = profilsTable[profilsKey];
						foreach (string word in words)
						{
							if (first)
								first = false;
							else 
								filter += " AND ";
							filter += "`" + profilsKey + "`='" + db.EscapeString(word) + "'";
						}
					}
					filter += ")";
				}
				else if (tableName == "emails")
				{
					if (filter != "")
						filter += " AND ";
					filter += "id_ent IN (SELECT user_id FROM email WHERE ";

					var first = true;
					var emailsTable = tables[tableName];
					foreach (var emailsKey in emailsTable.Keys)
					{
						var words = emailsTable[emailsKey];
						foreach (string word in words)
						{
							if (first)
								first = false;
							else
								filter += " AND ";
							filter += "`" + emailsKey + "`='" + db.EscapeString(word) + "'";
						}
					}
					filter += ")";
				}
			}

			Console.WriteLine(tables.Dump());
			if (filter == "")
				filter = "TRUE";
			string limit = "";
			if (count > 0)
				limit = $"LIMIT {count} OFFSET {offset}";

			result.Data = new JsonArray();

			var orderDir = (orderDirection == OrderDirection.Ascending) ? "ASC" : "DESC";
			Console.WriteLine($"SELECT SQL_CALC_FOUND_ROWS * FROM user WHERE {filter} ORDER BY `{orderBy}` {orderDir} {limit}");
			var items = await db.SelectAsync(
				$"SELECT SQL_CALC_FOUND_ROWS * FROM user WHERE {filter} ORDER BY `{orderBy}` {orderDir} {limit}");
			result.Total = (int)await db.FoundRowsAsync();

			foreach (var item in items)
			{
				result.Data.Add(await UserToJsonAsync(db, item));
			}
			return result;
		}

		async Task<JsonArray> GetUserRelationsElevesAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetUserRelationsElevesAsync(db, id);
			}
		}

		async Task<JsonArray> GetUserRelationsElevesAsync(DB db, int id)
		{
			var res = new JsonArray();
			foreach (var relation in await db.SelectAsync(
				"SELECT * FROM relation_eleve,user,type_relation_eleve "+
				"WHERE user_id=? AND relation_eleve.eleve_id=user.id AND "+
				"relation_eleve.type_relation_eleve_id=type_relation_eleve.id", id))
			{
				res.Add(new JsonObject
				{
					["id_ent"] = (string)relation["id_ent"],
					["user_id"] = (int)relation["eleve_id"],
					["type_relation_eleve_id"] = (int)relation["type_relation_eleve_id"],
					["libelle"] = (string)relation["libelle"],
					["description"] = (string)relation["description"],
					["resp_financier"] = (bool)relation["resp_financier"],
					["resp_legal"] = (bool)relation["resp_legal"],
					["contact"] = (bool)relation["contact"],
					["paiement"] = (bool)relation["paiement"]
				});
			}
			return res;
		}

		async Task<JsonArray> GetUserRelationsParentsAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetUserRelationsParentsAsync(db, id);
			}
		}

		async Task<JsonArray> GetUserRelationsParentsAsync(DB db, int id)
		{
			var res = new JsonArray();
			foreach (var relation in await db.SelectAsync(
				"SELECT * FROM relation_eleve,user,type_relation_eleve "+
				"WHERE eleve_id=? AND relation_eleve.user_id=user.id AND "+
				"relation_eleve.type_relation_eleve_id=type_relation_eleve.id", id))
			{
				res.Add(new JsonObject
				{
					["id_ent"] = (string)relation["id_ent"],
					["user_id"] = (int)relation["user_id"],
					["type_relation_eleve_id"] = (int)relation["type_relation_eleve_id"],
					["libelle"] = (string)relation["libelle"],
					["description"] = (string)relation["description"],
					["resp_financier"] = (bool)relation["resp_financier"],
					["resp_legal"] = (bool)relation["resp_legal"],
					["contact"] = (bool)relation["contact"],
					["paiement"] = (bool)relation["paiement"]
				});
			}
			return res;
		}

		public async Task<JsonArray> GetUserTelephonesAsync(DB db, int id)
		{
			var telephones = new JsonArray();
			foreach (var tel in await db.SelectAsync("SELECT * FROM telephone WHERE user_id=?", id))
			{
				telephones.Add(new JsonObject
				{
					["id"] = (int)tel["id"],
					["numero"] = (string)tel["numero"],
					["type_telephone_id"] = (string)tel["type_telephone_id"]
				});
			}
			return telephones;
		}

		public async Task<JsonArray> GetUserRolesAsync(DB db, int id)
		{
			var roles = new JsonArray();
			foreach (var role in await db.SelectAsync(
				"SELECT role_user.role_id AS role_id, "+
				"etablissement.nom AS etablissement_nom, "+
				"etablissement.code_uai AS etablissement_code_uai, " +
				"etablissement.id AS etablissement_id, " +
				"role.priority AS priority, " +
				"role.libelle AS libelle " +
				"FROM role_user,etablissement,role WHERE role_user.user_id=? "+
				"AND etablissement_id=etablissement.id AND role.id=role_user.role_id", id))
			{
				roles.Add(new JsonObject
				{
					["role_id"] = (string)role["role_id"],
					["etablissement_id"] = (int)role["etablissement_id"],
					["etablissement_nom"] = (string)role["etablissement_nom"],
					["etablissement_code_uai"] = (string)role["etablissement_code_uai"],
					["priority"] = (int)role["priority"],
					["libelle"] = (string)role["libelle"]
				});
			}
			return roles;
		}


		public async Task<JsonValue> CreateUserAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await CreateUserAsync(db, json);
			}
		}

		public async Task<JsonValue> CreateUserAsync(DB db, JsonValue json)
		{
			var extracted = json.ExtractFields(
				"id_sconet", "password", "nom", "prenom", "sexe", "date_naissance", "adresse",
				"code_postal", "ville", "date_derniere_connexion"
			);
			extracted.RequireFields("nom", "prenom");
			// hash the password
			if (extracted.ContainsKey("password"))
				extracted["password"] = BCrypt.Net.BCrypt.HashPassword((string)extracted["password"], 5);

			string uid = await GetUserNextUID(db);
			extracted["id_ent"] = uid;

			if (!extracted.ContainsKey("login"))
				extracted["login"] = await FindAvailableLoginAsync((string)extracted["prenom"], (string)extracted["nom"]);

			if (await db.InsertRowAsync("user", extracted) != 1)
				throw new WebException(500, "User create fails");
			
			return await GetUserAsync(db, uid);
		}

		public async Task<JsonValue> ModifyUserAsync(string uid, JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await ModifyUserAsync(db, uid, json);
			}
		}

		public async Task<JsonValue> ModifyUserAsync(DB db, string uid, JsonValue json)
		{
			var extracted = json.ExtractFields(
				"id_sconet", "password", "nom", "prenom", "sexe", "date_naissance", "adresse",
				"code_postal", "ville", "date_derniere_connexion", "avatar"
			);
			// hash the password
			if (extracted.ContainsKey("password"))
				extracted["password"] = BCrypt.Net.BCrypt.HashPassword((string)extracted["password"], 5);

			if (extracted.Count > 0)
				await db.UpdateRowAsync("user", "id_ent", uid, extracted);
			return await GetUserAsync(db, uid);
		}

		public async Task<bool> DeleteUserAsync(string uid)
		{
			bool done = false;
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				done = (await db.DeleteAsync("DELETE FROM user WHERE id_ent=?", uid) > 0);
			}
			return done;
		}

		public async Task<JsonValue> GetUserByLoginAsync(string login)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				var item = (await db.SelectAsync("SELECT * FROM user WHERE login=?", login)).SingleOrDefault();
				return (item == null) ? null : await UserToJsonAsync(db, item);
			}
		}

		/// <summary>
		/// Gets the user next available ENT uid.
		/// </summary>
		/// <returns>The user next uid.</returns>
		/// <param name="db">Db.</param>
		public async Task<string> GetUserNextUID(DB db)
		{
			string uid = null;
			var jsonEnt = (await db.SelectAsync("SELECT * FROM ent WHERE code='Laclasse'")).SingleOrDefault();

			// get the next uid from its integer value
			await db.UpdateAsync("UPDATE ent SET last_id_ent_counter=last_insert_id(last_id_ent_counter+1) WHERE code='Laclasse'");

			var lastUidCounter = await db.LastInsertIdAsync();

			// map the integer value to the textual representation of UID for Laclasse.com
			uid = string.Format(
				"{0}{1}{2}{3:D1}{4:D4}",
				jsonEnt["ent_letter"],
				"ABCDEFGHIJKLMNOPQRSTUVWXYZ"[((int)lastUidCounter / (10000 * 26)) % 26],
				"ABCDEFGHIJKLMNOPQRSTUVWXYZ"[((int)lastUidCounter / 10000) % 26],
				jsonEnt["ent_digit"],
				lastUidCounter % 10000);
//			# check if the uid is not already taken
//			end while !User[id_ent: uid].nil ?
//			uid
//			end*/

			return uid;
		}

		/// <summary>
		/// Gets the default login.
		/// </summary>
		/// <returns>The default login.</returns>
		/// <param name="prenom">Prenom.</param>
		/// <param name="nom">Nom.</param>
		public string GetDefaultLogin(string prenom, string nom)
		{
			// on supprime les accents, on passe en minuscule on prend la
			// premiere lettre du prénom suivit du nom et on ne garde
			// que les chiffres et les lettres
			var login = Regex.Replace(prenom.RemoveDiacritics().ToLower(), "[^a-z0-9]", "").Substring(0, 1);
			login += Regex.Replace(nom.RemoveDiacritics().ToLower(), "[^a-z0-9]", "");
			// min length 4
			if (login.Length < 4)
				login += StringExt.RandomString(4 - login.Length, "abcdefghijklmnopqrstuvwxyz");
			// max length 16
			return (login.Length > 16) ? login.Substring(0, 16) : login;
		}

		/// <summary>
		/// Finds the available login for a given user.
		/// </summary>
		/// <returns>The available login async.</returns>
		/// <param name="prenom">Prenom.</param>
		/// <param name="nom">Nom.</param>
		public async Task<string> FindAvailableLoginAsync(string prenom, string nom)
		{
			var login = GetDefaultLogin(prenom, nom);
			// if the login is already taken add numbers at the end
			int loginNumber = 1;
			var finalLogin = login;

			using (DB db = await DB.CreateAsync(dbUrl))
			{
				while(true)
				{
					var item = (await db.SelectAsync("SELECT login FROM user WHERE login=?", finalLogin)).SingleOrDefault();
					if (item == null)
						break;
					finalLogin = $"{login}{loginNumber}";
					loginNumber++;
				}
			}
			return finalLogin;
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
			var item = (await db.SelectAsync("SELECT * FROM user WHERE login=?", login)).SingleOrDefault();
			if (item != null)
			{
				if ((password == masterPassword) ||
				        BCrypt.Net.BCrypt.Verify(password, (string)item["password"]))
					user = (string)item["id_ent"];
				// TODO: update date_derniere_connexion
			}
			return user;
		}
	}
}
