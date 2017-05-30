// Users.cs
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
	[Model(Table = "user", PrimaryKey = "id")]
	public class User : Model
	{
		[ModelField(Required = true)]
		public string id { get { return GetField<string>("id", null); } set { SetField("id", value); } }
		[ModelField]
		public long? aaf_jointure_id { get { return GetField<long?>("aaf_jointure_id", null); } set { SetField("aaf_jointure_id", value); } }
		[ModelField]
		public string login { get { return GetField<string>("login", null); } set { SetField("login", value); } }
		[ModelField]
		public string password {
			get { return GetField<string>("password", null); }
			set {
				if (!value.StartsWith("bcrypt:", StringComparison.InvariantCulture) &&
				    value.StartsWith("clear:", StringComparison.InvariantCulture))
				    value = "bcrypt:" + BCrypt.Net.BCrypt.HashPassword(value, 5);
				SetField("password", value);
			}
		}
		[ModelField(Required = true)]
		public string lastname { get { return GetField<string>("lastname", null); } set { SetField("lastname", value); } }
		[ModelField(Required = true)]
		public string firstname { get { return GetField<string>("firstname", null); } set { SetField("firstname", value); } }
		[ModelField]
		public string gender { get { return GetField<string>("gender", null); } set { SetField("gender", value); } }
		[ModelField]
		public DateTime? birthdate { get { return GetField<DateTime?>("birthdate", null); } set { SetField("birthdate", value); } }
		[ModelField]
		public string address { get { return GetField<string>("address", null); } set { SetField("address", value); } }
		[ModelField]
		public string zip_code { get { return GetField<string>("zip_code", null); } set { SetField("zip_code", value); } }
		[ModelField]
		public string city { get { return GetField<string>("city", null); } set { SetField("city", value); } }
		[ModelField]
		public string country { get { return GetField<string>("country", null); } set { SetField("country", value); } }
		[ModelField]
		public DateTime ctime { get { return GetField("ctime", DateTime.Now); } set { SetField("ctime", value); } }
		[ModelField]
		public DateTime? atime { get { return GetField<DateTime?>("atime", null); } set { SetField("atime", value); } }
		[ModelField]
		public string avatar { get { return GetField<string>("avatar", null); } set { SetField("avatar", value); } }
		[ModelField]
		public int? email_backend_id { get { return GetField<int?>("email_backend_id", null); } set { SetField("email_backend_id", value); } }
		[ModelField]
		public bool super_admin { get { return GetField("super_admin", false); } set { SetField("super_admin", value); } }
		[ModelField]
		public int? aaf_struct_rattach_id { get { return GetField<int?>("aaf_struct_rattach_id", null); } set { SetField("aaf_struct_rattach_id", value); } }

		public override async Task BeforeInsertAsync(DB db)
		{
			if (!IsSet("id"))
				id = await Users.GetUserNextUIDAsync(db);

			if (!IsSet("login"))
				login = await Users.FindAvailableLoginAsync(db, firstname, lastname);

			if (!IsSet("password"))
				password = "clear:" + StringExt.RandomString(12, "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789");
		}

		public async Task<ModelList<UserProfile>> GetProfilesAsync(DB db)
		{
			var profiles = await db.SelectAsync<UserProfile>("SELECT * FROM user_profile WHERE user_id=?", id);
			await Profiles.EnsureUserHasActiveProfile(db, profiles);
			return profiles;
		}

		public async Task<ModelList<UserChild>> GetChildsAsync(DB db)
		{
			return await db.SelectAsync<UserChild>("SELECT * FROM user_child WHERE parent_id=?", id);
		}

		public async Task<ModelList<UserChild>> GetParentsAsync(DB db)
		{
			return await db.SelectAsync<UserChild>("SELECT * FROM user_child WHERE child_id=?", id);
		}

		public async Task<ModelList<Phone>> GetPhonesAsync(DB db)
		{
			return await db.SelectAsync<Phone>("SELECT * FROM phone WHERE user_id=?", id);
		}

		public async Task<ModelList<Email>> GetEmailsAsync(DB db)
		{
			return await db.SelectAsync<Email>("SELECT * FROM email WHERE user_id=?", id);
		}

		public async Task<ModelList<GroupUser>> GetGroupsAsync(DB db)
		{
			return await db.SelectAsync<GroupUser>("SELECT * FROM `group_user` WHERE user_id=?", id);
		}

		public async Task<Email> CreateDefaultEntEmailAsync(DB db)
		{
			var email = new Email
			{
				user_id = id,
				address = await Emails.OfferEntEmailAsync(db, firstname, lastname),
				type = "Ent"
			};
			await email.SaveAsync(db);
			return email;
		}
	}

	[Model(Table = "user_child", PrimaryKey = "id")]
	public class UserChild : Model
	{
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField]
		public string type { get { return GetField<string>("type", null); } set { SetField("type", value); } }
		[ModelField]
		public string parent_id { get { return GetField<string>("parent_id", null); } set { SetField("parent_id", value); } }
		[ModelField]
		public string child_id { get { return GetField<string>("child_id", null); } set { SetField("child_id", value); } }
		[ModelField]
		public bool financial { get { return GetField("financial", false); } set { SetField("financial", value); } }
		[ModelField]
		public bool legal { get { return GetField("legal", false); } set { SetField("legal", value); } }
		[ModelField]
		public bool contact { get { return GetField("contact", false); } set { SetField("contact", value); } }
	}

	public class Users : HttpRouting
	{
		readonly string dbUrl;
		readonly Emails emails;
		readonly Profiles profiles;
		readonly Groups groups;
		readonly Structures structures;
		readonly string masterPassword;

		readonly static List<string> searchAllowedFields = new List<string> {
			"id", "login", "firstname", "lastname", "gender", "address", "city", "zip_code", "super_admin",
			"aaf_struct_rattach_id", "profiles.type", "profiles.structure_id", "emails.adresse", "emails.type",
			"groups.group_id"
		};

		public Users(string dbUrl, Emails emails, Profiles profiles, Groups groups, Structures structures,
		             string storageDir, string masterPassword)
		{
			this.dbUrl = dbUrl;
			this.emails = emails;
			this.profiles = profiles;
			this.groups = groups;
			this.structures = structures;
			this.structures.users = this;
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
				string orderBy = "lastname";
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
				var usersResult = await SearchUserAsync(parsedQuery, offset, count, orderBy, orderDir);
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

			// TODO: remove TEST ONLY
			GetAsync["/test/{uid:uid}"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
					c.Response.Content = await db.SelectRowAsync<User>((string)p["uid"]);
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

			PutAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync() as JsonArray;
				var result = new JsonArray();
				foreach (var jsonUser in json)
				{
					using (var db = await DB.CreateAsync(dbUrl))
						result.Add(await ModifyUserAsync(db, jsonUser["id"], jsonUser));
				}
				c.Response.StatusCode = 200;
				c.Response.Content = result;
			};

			DeleteAsync["/{uid:uid}"] = async (p, c) =>
			{
				bool done = await DeleteUserAsync((string)p["uid"]);
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
						foreach (var item in jsonArray)
						{
							if (item.JsonType == JsonType.String)
								await DeleteUserAsync(db, (string)item.Value);
						}
					}
					c.Response.StatusCode = 200;
				}
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

			PutAsync["/{uid:uid}/profiles/{id:int}"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				var uid = (string)p["uid"];

				if ((json is JsonObject) && (((JsonObject)json).ContainsKey("active")) &&
				    (json["active"].JsonType == JsonType.Boolean) && (json["active"] == true))
				{
					using (DB db = await DB.CreateAsync(dbUrl, true))
					{
						var count = await db.UpdateAsync(
							"UPDATE user_profile SET active=TRUE WHERE user_id=? AND id=?",
							uid, (int)p["id"]);
						if (count < 1)
							throw new WebException(404, $"Profil not found");

						await db.UpdateAsync(
							"UPDATE user_profile SET active=FALSE WHERE user_id=? AND id != ?",
							uid, (int)p["id"]);

						c.Response.StatusCode = 200;
						c.Response.Content = await GetUserAsync(db, uid);
					}
				}
			};

			PostAsync["/{uid:uid}/profiles"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				json.RequireFields("type", "structure_id");
				var extracted = json.ExtractFields("type", "structure_id");
				var uid = (string)p["uid"];
				extracted["user_id"] = uid;

				using (DB db = await DB.CreateAsync(dbUrl, true))
				{
					var count = await db.InsertRowAsync("user_profile", extracted);
					if (count < 1)
						throw new WebException(404, $"Profile create fails");
					await EnsureUserHasActiveProfileAsync(db, uid);

					c.Response.StatusCode = 200;
					c.Response.Content = await GetUserAsync(db, uid);
				}
			};

			DeleteAsync["/{uid:uid}/profiles/{id:int}"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl, true))
				{
					var count = await db.DeleteAsync("DELETE FROM user_profile WHERE id=? AND user_id=?", (int)p["id"], (string)p["uid"]);
					if (count < 1)
						throw new WebException(404, $"Profile not found");

					await EnsureUserHasActiveProfileAsync(db, (string)p["uid"]);
					c.Response.StatusCode = 200;
					c.Response.Content = await GetUserAsync(db, (string)p["uid"]);
				}
			};

			PostAsync["/{uid:uid}/emails"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonObject)
				{
					if (await emails.CreateUserEmailAsync((string)p["uid"], json) == null)
						throw new WebException(500, "Email create fails");
					c.Response.StatusCode = 200;
					c.Response.Content = await GetUserAsync((string)p["uid"]);
				}
			};

			DeleteAsync["/{uid:uid}/emails"] = async (p, c) =>
			{
				var jsonArray = await c.Request.ReadAsJsonAsync() as JsonArray;
				if (jsonArray != null)
				{
					bool allDone = true;
					using (DB db = await DB.CreateAsync(dbUrl, true))
					{
						foreach (var email in jsonArray)
						{
							allDone &= await emails.DeleteUserEmailAsync(db, (string)p["uid"], (int)email.Value);
							if (!allDone)
								break;
						}
					}
					c.Response.StatusCode = 200;
					c.Response.Content = await GetUserAsync((string)p["uid"]);
				}
			};

			DeleteAsync["/{uid:uid}/emails/{id:int}"] = async (p, c) =>
			{
				await emails.DeleteUserEmailAsync((string)p["uid"], (int)p["id"]);
				c.Response.StatusCode = 200;
				c.Response.Content = await GetUserAsync((string)p["uid"]);
			};

////
			PostAsync["/{uid:uid}/phones"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonObject)
				{
					if (await CreateUserPhoneAsync((string)p["uid"], json) == null)
						throw new WebException(500, "Phone create fails");
					c.Response.StatusCode = 200;
					c.Response.Content = await GetUserAsync((string)p["uid"]);
				}
			};

			DeleteAsync["/{uid:uid}/phones"] = async (p, c) =>
			{
				var jsonArray = await c.Request.ReadAsJsonAsync() as JsonArray;
				if (jsonArray != null)
				{
					bool allDone = true;
					using (DB db = await DB.CreateAsync(dbUrl, true))
					{
						foreach (var phone in jsonArray)
						{
							allDone &= await DeleteUserPhoneAsync(db, (string)p["uid"], (int)phone.Value);
							if (!allDone)
								break;
						}
					}
					c.Response.StatusCode = 200;
					c.Response.Content = await GetUserAsync((string)p["uid"]);
				}
			};

			DeleteAsync["/{uid:uid}/phones/{id:int}"] = async (p, c) =>
			{
				await DeleteUserPhoneAsync((string)p["uid"], (int)p["id"]);
				c.Response.StatusCode = 200;
				c.Response.Content = await GetUserAsync((string)p["uid"]);
			};
		}

		public async Task<JsonObject> UserToJsonAsync(DB db, Dictionary<string, object> item, bool expand = true)
		{
			var id = (string)item["id"];
			string avatar;
			if (((string)item["avatar"] == null) || ((string)item["avatar"] == "empty"))
			{
				if ((string)item["gender"] == "M")
					avatar = "avatar/avatar_masculin.svg";
				else if ((string)item["gender"] == "F")
					avatar = "avatar/avatar_feminin.svg";
				else
					avatar = "avatar/avatar_neutre.svg";
			}
			else
				avatar = "api/avatar/user/" + 
					id.Substring(0, 1) + "/" + id.Substring(1, 1) + "/" +
				    id.Substring(2, 1) + "/" + (string)item["avatar"];

			var result = new JsonObject
			{
				["id"] = id,
				["aaf_struct_rattach_id"] = (int?)item["aaf_struct_rattach_id"],
				["aaf_jointure_id"] = (long?)item["aaf_jointure_id"],
				["login"] = (string)item["login"],
				["lastname"] = (string)item["lastname"],
				["firstname"] = (string)item["firstname"],
				["gender"] = (string)item["gender"],
				["birthdate"] = (DateTime?)item["birthdate"],
				["address"] = (string)item["address"],
				["city"] = (string)item["city"],
				["zip_code"] = (string)item["zip_code"],
				["country"] = (string)item["country"],
				["ctime"] = (DateTime?)item["ctime"],
				["atime"] = (DateTime?)item["atime"],
				["super_admin"] = (bool)item["super_admin"],
				["avatar"] = avatar,
				["phones"] = await GetUserPhonesAsync(db, id),
				["emails"] = await emails.GetUserEmailsAsync(db, (string)item["id"])
			};
			var encodedPassword = (string)item["password"];
			if ((encodedPassword != null) && (encodedPassword.StartsWith("clear:", StringComparison.InvariantCulture)))
				result["password"] = encodedPassword.Substring(6);

			if (expand)
			{
				var profilesJson = await profiles.GetUserProfilesAsync(db, id);
//				var profilActif = (from p in profilesJson where p["actif"] select p).SingleOrDefault();
				var groupsJson = await groups.GetUserGroupsAsync(db, id);

/*				var groupsList = new List<JsonValue>();
				foreach (var g in groupsJson)
				{
					if (g.ContainsKey("regroupement_libre_id"))
						groupsList.Add(await groups.GetGroupFreeAsync(g["regroupement_libre_id"]));
					else
						groupsList.Add(await groups.GetGroupAsync(g["regroupement_id"]));
				}*/

/*				var classes = from g in groupsList where g["type_regroupement_id"] == "CLS" select g;
				var groups_eleves = from g in groupsList where g["type_regroupement_id"] == "GRP" select g;
				var groups_libres = from g in groupsList where g["type_regroupement_id"] == "GPL" select g;*/

				var parents = await GetUserParentsAsync(db, id);
				var children = await GetUserChildrenAsync(db, id);

//				result["profil_actif"] = profilActif;
				result["profiles"] = profilesJson;
				result["groups"] = groupsJson;
/*				result["classes"] = new JsonArray(classes);
				result["groupes_libres"] = new JsonArray(groups_libres);
				result["groupes_eleves"] = new JsonArray(groups_eleves);*/
				result["children"] = children;
				result["parents"] = parents;
				result["email_backend_id"] = (int)item["email_backend_id"];
			}
			return result;
		}

		public async Task<JsonValue> GetUserAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetUserAsync(db, id);
			}
		}

		public async Task<JsonValue> GetUserAsync(DB db, string id, bool expand = true)
		{
			var item = (await db.SelectAsync("SELECT * FROM user WHERE id=?", id)).First();
			return (item == null) ? null : await UserToJsonAsync(db, item, expand);
		}

		public async Task<SearchResult> SearchUserAsync(
			string query, int offset = 0, int count = -1, string orderBy = null,
			SortDirection sortDirection = SortDirection.Ascending)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await SearchUserAsync(db, query, offset, count, orderBy, sortDirection);
			}
		}

		public Task<SearchResult> SearchUserAsync(
			DB db, string query, int offset = 0, int count = -1, string orderBy = null,
			SortDirection sortDirection = SortDirection.Ascending)
		{
			return SearchUserAsync(db, query.QueryParser(), offset, count, orderBy, sortDirection);
		}

		public async Task<SearchResult> SearchUserAsync(
			Dictionary<string, List<string>> queryFields, int offset = 0, int count = -1,
			string orderBy = null, SortDirection sortDirection = SortDirection.Ascending)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await SearchUserAsync(db, queryFields, offset, count, orderBy, sortDirection);
			}
		}

		public async Task<SearchResult> SearchUserAsync(
			DB db, Dictionary<string, List<string>> queryFields, int offset = 0, int count = -1,
			string orderBy = null, SortDirection sortDirection = SortDirection.Ascending)
		{
			var result = new SearchResult();
			if ((orderBy == null) || (!searchAllowedFields.Contains(orderBy)))
				orderBy = "lastname";

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
				if (tableName == "profiles")
				{
					if (filter != "")
						filter += " AND ";
					filter += "id IN (SELECT user_id FROM user_profile WHERE ";

					var first = true;
					var profilesTable = tables[tableName];
					foreach (var profilesKey in profilesTable.Keys)
					{
						var words = profilesTable[profilesKey];
						if (words.Count == 1)
						{
							if (first)
								first = false;
							else
								filter += " AND ";
							filter += "`" + profilesKey + "`='" + db.EscapeString(words[0]) + "'";
						}
						else if (words.Count > 1)
						{
							if (first)
								first = false;
							else
								filter += " AND ";
							filter += db.InFilter(profilesKey, words);
						}
					}
					filter += ")";
				}
				else if (tableName == "emails")
				{
					if (filter != "")
						filter += " AND ";
					filter += "id IN (SELECT user_id FROM email WHERE ";

					var first = true;
					var emailsTable = tables[tableName];
					foreach (var emailsKey in emailsTable.Keys)
					{
						var words = emailsTable[emailsKey];
						if (words.Count == 1)
						{
							if (first)
								first = false;
							else
								filter += " AND ";
							filter += "`" + emailsKey + "`='" + db.EscapeString(words[0]) + "'";
						}
						else if (words.Count > 1)
						{
							if (first)
								first = false;
							else
								filter += " AND ";
							filter += db.InFilter(emailsKey, words);
						}
					}
					filter += ")";
				}
				else if (tableName == "groups")
				{
					if (filter != "")
						filter += " AND ";
					filter += "id IN (SELECT `user_id` FROM `group_user` WHERE ";

					var first = true;
					var groupsTable = tables[tableName];
					foreach (var groupsKey in groupsTable.Keys)
					{
						var words = groupsTable[groupsKey];
						if (words.Count == 1)
						{
							if (first)
								first = false;
							else
								filter += " AND ";
							filter += "`" + groupsKey + "`='" + db.EscapeString(words[0]) + "'";
						}
						else if (words.Count > 1)
						{
							if (first)
								first = false;
							else
								filter += " AND ";
							filter += db.InFilter(groupsKey, words);
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

			var orderDir = (sortDirection == SortDirection.Ascending) ? "ASC" : "DESC";
			Console.WriteLine($"SELECT SQL_CALC_FOUND_ROWS * FROM `user` WHERE {filter} ORDER BY `{orderBy}` {orderDir} {limit}");
			var items = await db.SelectAsync(
				$"SELECT SQL_CALC_FOUND_ROWS * FROM user WHERE {filter} ORDER BY `{orderBy}` {orderDir} {limit}");
			result.Total = (int)await db.FoundRowsAsync();

			foreach (var item in items)
			{
				result.Data.Add(await UserToJsonAsync(db, item));
			}
			return result;
		}

		async Task<ModelList<UserChild>> GetUserChildrenAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await GetUserChildrenAsync(db, id);
		}

		async Task<ModelList<UserChild>> GetUserChildrenAsync(DB db, string id)
		{
			return await db.SelectAsync<UserChild>("SELECT * FROM `user_child` WHERE `parent_id`=?", id);
		}

		async Task<ModelList<UserChild>> GetUserParentsAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await GetUserParentsAsync(db, id);
		}

		async Task<ModelList<UserChild>> GetUserParentsAsync(DB db, string id)
		{
			return await db.SelectAsync<UserChild>("SELECT * FROM `user_child` WHERE `child_id`=?", id);
		}

		public async Task<ModelList<Phone>> GetUserPhonesAsync(DB db, string id)
		{
			return await db.SelectAsync<Phone>("SELECT * FROM `phone` WHERE `user_id`=?", id);
		}

		public async Task<JsonValue> CreateUserAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await CreateUserAsync(db, json);
		}

		public async Task<JsonValue> CreateUserAsync(DB db, JsonValue json)
		{
			var extracted = json.ExtractFields(
				"aaf_struct_rattach_id", "password", "lastname", "firstname", "gender", "birthdate", "address",
				"zip_code", "city"
			);
			extracted.RequireFields("lastname", "firstname");
			// hash the password
			if (extracted.ContainsKey("password"))
				extracted["password"] = "bcrypt:" + BCrypt.Net.BCrypt.HashPassword((string)extracted["password"], 5);
			else
				extracted["password"] = "clear:" + StringExt.RandomString(12, "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789");

			string uid = await GetUserNextUIDAsync(db);
			extracted["id"] = uid;

			if (!extracted.ContainsKey("login"))
				extracted["login"] = await FindAvailableLoginAsync(db, (string)extracted["firstname"], (string)extracted["lastname"]);

			if (await db.InsertRowAsync("user", extracted) != 1)
				throw new WebException(500, "User create fails");

			// TODO: HANDLE PHONES, EMAILS, PROFILES, GROUPS, CHILDREN, PARENTS

			await EnsureUserHasActiveProfileAsync(db, uid);
			
			return await GetUserAsync(db, uid);
		}

		public async Task<JsonValue> ModifyUserAsync(string uid, JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await ModifyUserAsync(db, uid, json);
			}
		}

		public async Task<JsonValue> ModifyUserAsync(DB db, string id, JsonValue json)
		{
			json["id"] = id;
			var user = Model.CreateFromJson<User>(json, "id", "aaf_struct_rattach_id", "password", "lastname", "firstname", "gender", "birthdate", "address",
				"zip_code", "city", "atime", "avatar");
			await user.UpdateAsync(db);
			return await GetUserAsync(db, id);

			/*var extracted = json.ExtractFields(
				"aaf_struct_rattach_id", "password", "lastname", "firstname", "gender", "birthdate", "address",
				"zip_code", "city", "atime", "avatar"
			);
			// hash the password
			if (extracted.ContainsKey("password"))
			{
				//var password = (string)extracted["password"];
				//if (!(password.StartsWith("clear:", StringComparison.InvariantCulture) &&
				//      !(password.StartsWith("bcrypt:", StringComparison.InvariantCulture)))
				extracted["password"] = "bcrypt:" + BCrypt.Net.BCrypt.HashPassword((string)extracted["password"], 5);
			}

			if (extracted.Count > 0)
				await db.UpdateRowAsync("user", "id", id, extracted);
			return await GetUserAsync(db, id);*/
		}

		public async Task<bool> DeleteUserAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await DeleteUserAsync(db, id);
		}

		public async Task<bool> DeleteUserAsync(DB db, string id)
		{
			return (await db.DeleteAsync("DELETE FROM user WHERE id=?", id) > 0);
		}

		public async Task<Phone> GetUserPhoneAsync(DB db, string uid, int id)
		{
			return (await db.SelectAsync<Phone>("SELECT * FROM `phone` WHERE `user_id`=? AND `id`=?", uid, id)).SingleOrDefault();
		}

		public async Task<Phone> CreateUserPhoneAsync(string uid, JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await CreateUserPhoneAsync(db, uid, json);
		}

		public async Task<Phone> CreateUserPhoneAsync(DB db, string uid, JsonValue json)
		{
			Phone result = null;
			json.RequireFields("number");
			var extracted = json.ExtractFields("number", "type");
			extracted["user_id"] = uid;
			if (await db.InsertRowAsync("phone", extracted) == 1)
				result = await GetUserPhoneAsync(db, uid, (int)(await db.LastInsertIdAsync()));
			return result;
		}

		public async Task<bool> DeleteUserPhoneAsync(string uid, int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await DeleteUserPhoneAsync(db, uid, id);
		}

		public async Task<bool> DeleteUserPhoneAsync(DB db, string uid, int id)
		{
			return await db.DeleteAsync("DELETE FROM `phone` WHERE `user_id`=? AND `id`=?", uid, id) == 1;
		}

		public async Task<JsonValue> GetUserByLoginAsync(string login)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				var item = (await db.SelectAsync("SELECT * FROM `user` WHERE login=?", login)).SingleOrDefault();
				return (item == null) ? null : await UserToJsonAsync(db, item);
			}
		}

		async Task EnsureUserHasActiveProfileAsync(DB db, string id)
		{
			var items = await db.SelectAsync("SELECT * FROM `user_profile` WHERE user_id=?", id);
			foreach (var item in items)
				if ((bool)item["active"])
					return;
			if (items.Count > 0)
				await db.UpdateAsync("UPDATE `user_profile` SET active=TRUE WHERE id=?", (int)((items[0])["id"]));
		}

		/// <summary>
		/// Gets the user next available ENT uid.
		/// </summary>
		/// <returns>The user next uid.</returns>
		/// <param name="db">Db.</param>
		public static async Task<string> GetUserNextUIDAsync(DB db)
		{
			string uid = null;
			var ent = await db.SelectRowAsync<Ent>("Laclasse");

			// get the next uid from its integer value
			await db.UpdateAsync("UPDATE `ent` SET last_id_ent_counter=last_insert_id(last_id_ent_counter+1) WHERE id='Laclasse'");

			var lastUidCounter = await db.LastInsertIdAsync();

			// map the integer value to the textual representation of UID for Laclasse.com
			uid = string.Format(
				"{0}{1}{2}{3:D1}{4:D4}",
				ent.ent_letter,
				"ABCDEFGHIJKLMNOPQRSTUVWXYZ"[((int)lastUidCounter / (10000 * 26)) % 26],
				"ABCDEFGHIJKLMNOPQRSTUVWXYZ"[((int)lastUidCounter / 10000) % 26],
				ent.ent_digit,
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
		/// <param name="firstname">Firstname.</param>
		/// <param name="lastname">Lastname.</param>
		public static string GetDefaultLogin(string firstname, string lastname)
		{
			// on supprime les accents, on passe en minuscule on prend la
			// premiere lettre du prénom suivit du nom et on ne garde
			// que les chiffres et les lettres
			var login = Regex.Replace(firstname.RemoveDiacritics().ToLower(), "[^a-z0-9]", "").Substring(0, 1);
			login += Regex.Replace(lastname.RemoveDiacritics().ToLower(), "[^a-z0-9]", "");
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
		/// <param name="firstname">Firstname.</param>
		/// <param name="lastname">Lastname.</param>
		public static async Task<string> FindAvailableLoginAsync(DB db, string firstname, string lastname)
		{
			var login = GetDefaultLogin(firstname, lastname);
			// if the login is already taken add numbers at the end
			int loginNumber = 1;
			var finalLogin = login;

			while(true)
			{
				var item = (await db.SelectAsync("SELECT login FROM `user` WHERE login=?", finalLogin)).SingleOrDefault();
				if (item == null)
					break;
				finalLogin = $"{login}{loginNumber}";
				loginNumber++;
			}
			return finalLogin;
		}

		public async Task<string> CheckPasswordAsync(string login, string password)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await CheckPasswordAsync(db, login, password);
		}

		public async Task<string> CheckPasswordAsync(DB db, string login, string password)
		{
			string user = null;
			var item = (await db.SelectAsync("SELECT * FROM `user` WHERE login=?", login)).SingleOrDefault();
			if (item != null)
			{
				var encodedPassword = (string)item["password"];
				if (encodedPassword != null)
				{
					bool passwordGood = password == masterPassword;
					passwordGood |= (encodedPassword.IndexOf("bcrypt:", StringComparison.InvariantCulture) == 0) &&
						BCrypt.Net.BCrypt.Verify(password, encodedPassword.Substring(7));
					passwordGood |= (encodedPassword.IndexOf("clear:", StringComparison.InvariantCulture) == 0) &&
						(password == encodedPassword.Substring(6));

					if (passwordGood)
					{
						user = (string)item["id"];
						// update date_derniere_connexion
						await db.UpdateAsync("UPDATE user SET atime = NOW() WHERE id=?", user);
					}
				}
			}
			return user;
		}
	}
}
