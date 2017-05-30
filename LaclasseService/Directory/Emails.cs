// Emails.cs
// 
//  Handle emails API. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017 Daniel LACROIX
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
	[Model(Table = "email", PrimaryKey = "id")]
	public class Email : Model
	{ 
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField(Required = true)]
		public string user_id { get { return GetField<string>("user_id", null); } set { SetField("user_id", value); } }
		[ModelField(Required = true)]
		public string address { get { return GetField<string>("address", null); } set { SetField("address", value); } }
		[ModelField]
		public bool primary { get { return GetField("primary", false); } set { SetField("primary", value); } }
		[ModelField]
		public string type { get { return GetField<string>("type", null); } set { SetField("type", value); } }
	}

	public class Emails : HttpRouting
	{
		readonly string dbUrl;

		public Emails(string dbUrl)
		{
			this.dbUrl = dbUrl;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/offer_ent"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				json.RequireFields("firstname", "lastname");
				c.Response.StatusCode = 200;
				using (DB db = await DB.CreateAsync(dbUrl))
					c.Response.Content = new JsonPrimitive(await OfferEntEmailAsync(db, json["firstname"], json["lastname"]));
			};

			GetAsync["/mail_available"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				json.RequireFields("mail");

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var emails = await db.SelectAsync("SELECT * FROM email WHERE address=?", (string)json["mail"]);
					c.Response.StatusCode = 200;
					c.Response.Content = new JsonObject
					{
						["available"] = (emails.Count() == 0)
					};
				}
			};

			GetAsync["/"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
					c.Response.Content = await Model.SearchAsync<Email>(
						db,new List<string> { "id", "address", "user_id", "primary", "type" }, c);
				c.Response.StatusCode = 200;
			};

			GetAsync["/{id:int}"] = async (p, c) =>
			{
				Email email = null;
				using (DB db = await DB.CreateAsync(dbUrl))
					email = await db.SelectRowAsync<Email>((int)p["id"]);
				if (email != null)
				{
					c.Response.StatusCode = 200;
					c.Response.Content = email;
				}
			};

			PostAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonArray)
				{
					var result = new JsonArray();
					using (DB db = await DB.CreateAsync(dbUrl, true))
					{
						foreach (var jsonEmail in (JsonArray)json)
						{
							var email = Model.CreateFromJson<Email>(jsonEmail);
							result.Add(await email.SaveAsync(db));
						}
					}
					c.Response.StatusCode = 200;
					c.Response.Content = result;
				}
				else if (json is JsonObject)
				{
					var email = Model.CreateFromJson<Email>(json);
					using (DB db = await DB.CreateAsync(dbUrl))
						await email.SaveAsync(db);
					c.Response.StatusCode = 200;
					c.Response.Content = email;
				}
			};

			PutAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonArray)
				{
					var result = new JsonArray();
					using (DB db = await DB.CreateAsync(dbUrl, true))
					{
						foreach (var jsonEmail in (JsonArray)json)
						{
							var email = Model.CreateFromJson<Email>(jsonEmail);
							await email.UpdateAsync(db);
							result.Add(await email.LoadAsync(db));
						}
					}
					c.Response.StatusCode = 200;
					c.Response.Content = result;
				}
				else if (json is JsonObject)
				{
					var email = Model.CreateFromJson<Email>(json);
					using (DB db = await DB.CreateAsync(dbUrl, true))
					{
						await email.UpdateAsync(db);
						await email.LoadAsync(db);
					}
					c.Response.StatusCode = 200;
					c.Response.Content = email;
				}
			};

			DeleteAsync["/{id:int}"] = async (p, c) =>
			{
				Email email = null;
				using (DB db = await DB.CreateAsync(dbUrl, true))
				{
					email = await db.SelectRowAsync<Email>((int)p["id"]);
					if (email != null)
						await email.DeleteAsync(db);
				}
				if (email != null)
					c.Response.StatusCode = 200;
			};
		}

		public static async Task<string> OfferEntEmailAsync(DB db, string firstname, string lastname)
		{
			var beginEmail = (firstname.RemoveDiacritics() + "." + lastname.RemoveDiacritics()).ToLower();
			beginEmail = Regex.Replace(beginEmail.Replace(' ', '-'), @"[^\.\-a-z0-9]", "", RegexOptions.IgnoreCase);
			string endEmail;
			string aliasEmail;
			int aliasIndex = 0;
			var ent = await db.SelectRowAsync<Ent>("Laclasse");
			if (ent == null)
				throw new Exception("The ENT is not defined in the DB");
			endEmail = "@" + ent.mail_domaine;

			aliasEmail = beginEmail + endEmail;

			var emails = (await db.SelectAsync(
				"SELECT address FROM email WHERE SUBSTRING(address,1,?)=? AND type='Ent'",
				beginEmail.Length, beginEmail)).Select((arg) => (string)arg["address"]);
			while (emails.Contains(aliasEmail))
			{
				aliasEmail = beginEmail + (++aliasIndex).ToString() + endEmail;
			}

			return aliasEmail;
		}

		public async Task<ModelList<Email>> GetUserEmailsAsync(string uid)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await GetUserEmailsAsync(db, uid);
		}

		public async Task<ModelList<Email>> GetUserEmailsAsync(DB db, string uid)
		{
			return await db.SelectAsync<Email>("SELECT * FROM email WHERE user_id=?", uid);
		}

		public async Task<Email> GetUserEmailAsync(DB db, string uid, int id)
		{
			return (await db.SelectAsync<Email>("SELECT * FROM email WHERE user_id=? AND id=?", uid, id)).SingleOrDefault();
		}

		public async Task<Email> CreateUserEmailAsync(string uid, JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await CreateUserEmailAsync(db, uid, json);
		}

		public async Task<Email> CreateUserEmailAsync(DB db, string uid, JsonValue json)
		{
			Email emailResult = null;
			json.RequireFields("address", "type");
			var extracted = json.ExtractFields("address", "type");
			extracted["user_id"] = uid;
			if (await db.InsertRowAsync("email", extracted) == 1)
				emailResult = await GetUserEmailAsync(db, uid, (int)(await db.LastInsertIdAsync()));
			return emailResult;
		}

		public async Task<bool> DeleteUserEmailAsync(string uid, int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await DeleteUserEmailAsync(db, uid, id);
		}

		public async Task<bool> DeleteUserEmailAsync(DB db, string uid, int id)
		{
			return await db.DeleteAsync("DELETE FROM email WHERE user_id=? AND id=?", uid, id) == 1;
		}
	}
}
