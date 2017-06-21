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
	[Model(Table = "user", PrimaryKey = nameof(id))]
	public class User : Model
	{
		[ModelField(Required = true, RegexMatch = "^[A-Z0-9]+$")]
		public string id { get { return GetField<string>(nameof(id), null); } set { SetField(nameof(id), value); } }
		[ModelField]
		public long? aaf_jointure_id { get { return GetField<long?>(nameof(aaf_jointure_id), null); } set { SetField(nameof(aaf_jointure_id), value); } }
		[ModelField]
		public string login { get { return GetField<string>(nameof(login), null); } set { SetField(nameof(login), value); } }
		[ModelField]
		public string password {
			get { return GetField<string>(nameof(password), null); }
			set {
				if (!value.StartsWith("bcrypt:", StringComparison.InvariantCulture) &&
				    !value.StartsWith("clear:", StringComparison.InvariantCulture))
				    value = "bcrypt:" + BCrypt.Net.BCrypt.HashPassword(value, 5);
				SetField(nameof(password), value);
			}
		}
		[ModelField(Required = true)]
		public string lastname { get { return GetField<string>(nameof(lastname), null); } set { SetField(nameof(lastname), value); } }
		[ModelField(Required = true)]
		public string firstname { get { return GetField<string>(nameof(firstname), null); } set { SetField(nameof(firstname), value); } }
		[ModelField]
		public string gender { get { return GetField<string>(nameof(gender), null); } set { SetField(nameof(gender), value); } }
		[ModelField]
		public DateTime? birthdate { get { return GetField<DateTime?>(nameof(birthdate), null); } set { SetField(nameof(birthdate), value); } }
		[ModelField]
		public string address { get { return GetField<string>(nameof(address), null); } set { SetField(nameof(address), value); } }
		[ModelField]
		public string zip_code { get { return GetField<string>(nameof(zip_code), null); } set { SetField(nameof(zip_code), value); } }
		[ModelField]
		public string city { get { return GetField<string>(nameof(city), null); } set { SetField(nameof(city), value); } }
		[ModelField]
		public string country { get { return GetField<string>(nameof(country), null); } set { SetField(nameof(country), value); } }
		[ModelField]
		public DateTime ctime { get { return GetField(nameof(ctime), DateTime.Now); } set { SetField(nameof(ctime), value); } }
		[ModelField]
		public DateTime? atime { get { return GetField<DateTime?>(nameof(atime), null); } set { SetField(nameof(atime), value); } }
		[ModelField]
		public string avatar { get { return GetField<string>(nameof(avatar), null); } set { SetField(nameof(avatar), value); } }
		[ModelField(ForeignModel = typeof(EmailBackend))]
		public int? email_backend_id { get { return GetField<int?>(nameof(email_backend_id), null); } set { SetField(nameof(email_backend_id), value); } }
		[ModelField]
		public bool super_admin { get { return GetField(nameof(super_admin), false); } set { SetField(nameof(super_admin), value); } }
		[ModelField]
		public int? aaf_struct_rattach_id { get { return GetField<int?>(nameof(aaf_struct_rattach_id), null); } set { SetField(nameof(aaf_struct_rattach_id), value); } }
		[ModelField(ForeignModel = typeof(Grade))]
		public string student_grade_id { get { return GetField<string>(nameof(student_grade_id), null); } set { SetField(nameof(student_grade_id), value); } }

		public async override Task<bool> InsertAsync(DB db)
		{
			if (!IsSet(nameof(id)))
				id = await Users.GetUserNextUIDAsync(db);

			if (!IsSet(nameof(login)))
				login = await Users.FindAvailableLoginAsync(db, firstname, lastname);

			if (!IsSet(nameof(password)))
				password = "clear:" + StringExt.RandomString(12, "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789");

			return await base.InsertAsync(db);
		}

		[ModelExpandField(Name = nameof(profiles), ForeignModel = typeof(UserProfile))]
		public ModelList<UserProfile> profiles { get { return GetField<ModelList<UserProfile>>(nameof(profiles), null); } set { SetField(nameof(profiles), value); } }

		public async Task<ModelList<UserProfile>> GetProfilesAsync(DB db)
		{
			await LoadExpandFieldAsync(db, nameof(profiles));
			return profiles;
		}

		[ModelExpandField(Name = nameof(children), ForeignModel = typeof(UserChild), ForeignField = nameof(UserChild.parent_id))]
		public ModelList<UserChild> children { get { return GetField<ModelList<UserChild>>(nameof(children), null); } set { SetField(nameof(children), value); } }

		public async Task<ModelList<UserChild>> GetChildsAsync(DB db)
		{
			await LoadExpandFieldAsync(db, nameof(children));
			return children;
		}

		[ModelExpandField(Name = nameof(parents), ForeignModel = typeof(UserChild), ForeignField = nameof(UserChild.child_id))]
		public ModelList<UserChild> parents { get { return GetField<ModelList<UserChild>>(nameof(parents), null); } set { SetField(nameof(parents), value); } }

		public async Task<ModelList<UserChild>> GetParentsAsync(DB db)
		{
			await LoadExpandFieldAsync(db, nameof(parents));
			return parents;
		}

		[ModelExpandField(Name = nameof(phones), ForeignModel = typeof(Phone))]
		public ModelList<Phone> phones { get { return GetField<ModelList<Phone>>(nameof(phones), null); } set { SetField(nameof(phones), value); } }

		public async Task<ModelList<Phone>> GetPhonesAsync(DB db)
		{
			await LoadExpandFieldAsync(db, nameof(phones));
			return phones;
		}

		[ModelExpandField(Name = nameof(emails), ForeignModel = typeof(Email))]
		public ModelList<Email> emails { get { return GetField<ModelList<Email>>(nameof(emails), null); } set { SetField(nameof(emails), value); } }

		public async Task<ModelList<Email>> GetEmailsAsync(DB db)
		{
			await LoadExpandFieldAsync(db, nameof(emails));
			return emails;
		}

		[ModelExpandField(Name = nameof(groups), ForeignModel = typeof(GroupUser))]
		public ModelList<GroupUser> groups { get { return GetField<ModelList<GroupUser>>(nameof(groups), null); } set { SetField(nameof(groups), value); } }

		public async Task<ModelList<GroupUser>> GetGroupsAsync(DB db)
		{
			await LoadExpandFieldAsync(db, nameof(groups));
			return groups;
		}

		[ModelExpandField(Name = nameof(news), ForeignModel = typeof(News), Visible = false)]
		public ModelList<News> news { get { return GetField<ModelList<News>>(nameof(news), null); } set { SetField(nameof(news), value); } }

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

		public override JsonObject ToJson()
		{
			var json = base.ToJson();
			if (IsSet(nameof(avatar)))
			{
				string jsonAvatar;
				if ((avatar == null) || (avatar == "empty"))
				{
					if (gender == "M")
						jsonAvatar = "avatar/avatar_masculin.svg";
					else if (gender == "F")
						jsonAvatar = "avatar/avatar_feminin.svg";
					else
						jsonAvatar = "avatar/avatar_neutre.svg";
				}
				else
					jsonAvatar = "api/avatar/user/" +
						id.Substring(0, 1) + "/" + id.Substring(1, 1) + "/" +
						id.Substring(2, 1) + "/" + avatar;
				json["avatar"] = jsonAvatar;
			}

			if (IsSet(nameof(password)))
			{
				var encodedPassword = password;
				if ((encodedPassword != null) && (encodedPassword.StartsWith("clear:", StringComparison.InvariantCulture)))
					json["password"] = encodedPassword.Substring(6);
				else
					json.Remove("password");
			}
			return json;
		}

		public override void FromJson(JsonObject json, string[] filterFields = null, HttpContext context = null)
		{
			base.FromJson(json, filterFields, context);
			// if the password is set, need to transform it
			if (json.ContainsKey(nameof(password)))
				password = json[nameof(password)];
		}

		public override async Task EnsureRightAsync(HttpContext context, Right right)
		{
			await context.EnsureHasRightsOnUserAsync(this, true, right == Right.Update, right == Right.Create || right == Right.Delete);
		}
	}

	public class Users : ModelService<User>
	{
		readonly string dbUrl;
		readonly string masterPassword;

		readonly static List<string> searchAllowedFields = new List<string> {
			"id", "login", "firstname", "lastname", "gender", "address", "city", "zip_code", "super_admin",
			"aaf_struct_rattach_id", "profiles.type", "profiles.structure_id", "emails.adresse", "emails.type",
			"groups.group_id"
		};

		public Users(string dbUrl, string storageDir, string masterPassword) : base(dbUrl)
		{
			this.dbUrl = dbUrl;
			this.masterPassword = masterPassword;

			var avatarDir = Path.Combine(storageDir, "avatar");

			if (!Dir.Exists(avatarDir))
				Dir.CreateDirectory(avatarDir);

			// register a type
			Types["uid"] = (val) => (Regex.IsMatch(val, "^[A-Z0-9]+$")) ? val : null;

			GetAsync["/current"] = async (p, c) =>
			{
				var user = await c.GetAuthenticatedUserAsync();
				if ((user == null) || !user.IsUser)
					c.Response.StatusCode = 401;
				else
				{
					c.Response.StatusCode = 302;
					c.Response.Headers["location"] = user.user.id;
				}
			};

			PostAsync["/{uid:uid}/upload/avatar"] = async (p, c) =>
			{
				var uid = (string)p["uid"];

				User oldUser;
				using (DB db = await DB.CreateAsync(dbUrl))
					 oldUser = await db.SelectRowAsync<User>(uid, false);

				//var oldUser = await GetUserAsync(uid);
				if (oldUser == null)
					return;

				await c.EnsureHasRightsOnUserAsync(oldUser, true, true, false);

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
							var userDiff = new User { id = uid, avatar = name };
							using (DB db = await DB.CreateAsync(dbUrl))
							{
								await userDiff.UpdateAsync(db);
								await userDiff.LoadAsync(db, true);
							}
							c.Response.Content = userDiff;

							if ((oldUser.avatar != null) && (oldUser.avatar != "empty"))
							{
								var oldFile = Path.Combine(avatarDir, uid[0].ToString(), uid[1].ToString(), uid[2].ToString(), Path.GetFileName(oldUser.avatar));
								if(File.Exists(oldFile))
									File.Delete(oldFile);
							}
						}
					}
				}
			};
		}

		public async Task<User> GetUserAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await GetUserAsync(db, id);
		}

		public async Task<User> GetUserAsync(DB db, string id, bool expand = true)
		{
			return await db.SelectRowAsync<User>(id, expand);
		}

		public async Task<SearchResult<User>> SearchUserAsync(
			string query, int offset = 0, int count = -1, string orderBy = null,
			SortDirection sortDirection = SortDirection.Ascending)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await SearchUserAsync(db, query, offset, count, orderBy, sortDirection);
		}

		public Task<SearchResult<User>> SearchUserAsync(
			DB db, string query, int offset = 0, int count = -1, string orderBy = null,
			SortDirection sortDirection = SortDirection.Ascending)
		{
			return SearchUserAsync(db, query.QueryParser(), offset, count, orderBy, sortDirection);
		}

		public async Task<SearchResult<User>> SearchUserAsync(
			Dictionary<string, List<string>> queryFields, int offset = 0, int count = -1,
			string orderBy = null, SortDirection sortDirection = SortDirection.Ascending)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await SearchUserAsync(db, queryFields, offset, count, orderBy, sortDirection);
		}

		public async Task<SearchResult<User>> SearchUserAsync(
			DB db, Dictionary<string, List<string>> queryFields, int offset = 0, int count = -1,
			string orderBy = null, SortDirection sortDirection = SortDirection.Ascending)
		{
			return await Model.SearchAsync<User>(db, searchAllowedFields, queryFields, orderBy, sortDirection, true, offset, count);
		}

		public async Task<User> GetUserByLoginAsync(string login)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return (await db.SelectExpandAsync<User>("SELECT * FROM `user` WHERE `login`=?", new object[] { login })).SingleOrDefault();
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
			await db.UpdateAsync("UPDATE `ent` SET `last_id_ent_counter`=last_insert_id(last_id_ent_counter+1) WHERE `id`='Laclasse'");

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
			var login = Regex.Replace(firstname.RemoveDiacritics().ToLower(), "[^a-z0-9]", "");
			if (login.Length > 0)
				login = login.Substring(0, 1);
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
				var item = (await db.SelectAsync("SELECT `login` FROM `user` WHERE `login`=?", finalLogin)).SingleOrDefault();
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
			var item = (await db.SelectAsync("SELECT * FROM `user` WHERE `login`=?", login)).SingleOrDefault();
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
						await db.UpdateAsync("UPDATE `user` SET `atime` = NOW() WHERE `id`=?", user);
					}
				}
			}
			return user;
		}
	}
}
