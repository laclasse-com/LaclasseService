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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public enum EmailType 
	{
		Ent,
		Academique,
		Autre
	}

	[Model(Table = "email", PrimaryKey = nameof(id))]
	public class Email : Model
	{ 
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField(Required = true, ForeignModel = typeof(User))]
		public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
		[ModelField(Required = true)]
		public string address { get { return GetField<string>(nameof(address), null); } set { SetField(nameof(address), value); } }
		[ModelField]
		public bool primary { get { return GetField(nameof(primary), false); } set { SetField(nameof(primary), value); } }
		[ModelField]
		public EmailType type { get { return GetField<EmailType>(nameof(type), EmailType.Autre); } set { SetField(nameof(type), value); } }

		public async override Task<bool> InsertAsync(DB db)
		{
			var userEmails = (ModelList<Email>)await LoadExpandFieldAsync<User>(db, nameof(User.emails), user_id);
			var primaryEmails = userEmails.FindAll((obj) => obj.primary);
			// ensure only 1 email is primary per user
			if (primaryEmails.Count == 0)
				primary = true;
			bool done = await base.InsertAsync(db);
			if (IsSet(nameof(primary)) && primary && (primaryEmails.Count > 0))
			{
				foreach (var email in primaryEmails)
					await email.DiffWithId(new Email { primary = false }).UpdateAsync(db);
			}
			return done;
		}

		public async override Task<bool> UpdateAsync(DB db)
		{
			var oldEmail = this;
			// if the user is not known, load the email data
			if (!IsSet(nameof(user_id)))
				oldEmail = await db.SelectRowAsync<Email>(id);

			var userEmails = (ModelList<Email>)await LoadExpandFieldAsync<User>(db, nameof(User.emails), oldEmail.user_id);
			var primaryEmails = userEmails.FindAll((obj) => obj.primary && (obj.id != id));

			if (primaryEmails.Count == 0)
				primary = true;

			var done = await base.UpdateAsync(db);

			if (IsSet(nameof(primary)) && primary)
			{
				foreach (var email in primaryEmails)
					await email.DiffWithId(new Email { primary = false }).UpdateAsync(db);
			}
			return done;
		}

		public async override Task<bool> DeleteAsync(DB db)
		{
			var oldEmail = this;
			// if the user is not known, load the email data
			if (!IsSet(nameof(user_id)))
				oldEmail = await db.SelectRowAsync<Email>(id);

			var userEmails = (ModelList<Email>)await LoadExpandFieldAsync<User>(db, nameof(User.emails), oldEmail.user_id);
			var primaryEmails = userEmails.FindAll((obj) => obj.primary && (obj.id != id));

			var res = await base.DeleteAsync(db);

			if (primaryEmails.Count == 0)
			{
				var email = userEmails.FirstOrDefault((arg) => arg.id != id);
				if (email != null)
					await email.DiffWithId(new Email { primary = true }).UpdateAsync(db);
			}
			return res;
		}
	}

	[Model(Table = "email_backend", PrimaryKey = nameof(id))]
	public class EmailBackend : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField(Required = true)]
		public string address { get { return GetField<string>(nameof(address), null); } set { SetField(nameof(address), value); } }
		[ModelField(Required = true)]
		public string ip_address { get { return GetField<string>(nameof(ip_address), null); } set { SetField(nameof(ip_address), value); } }
		[ModelField]
		public string master_key { get { return GetField<string>(nameof(master_key), null); } set { SetField(nameof(master_key), value); } }
	}

	public class Emails : ModelService<Email>
	{
		public Emails(string dbUrl) : base(dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/offer_ent"] = async (p, c) =>
			{
				c.Request.QueryString.RequireFields("firstname", "lastname");
				c.Response.StatusCode = 200;
				using (DB db = await DB.CreateAsync(dbUrl))
					c.Response.Content = new JsonPrimitive(await OfferEntEmailAsync(db, c.Request.QueryString["firstname"], c.Request.QueryString["lastname"]));
			};

			// TODO: remove this useless API
			GetAsync["/mail_available"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				json.RequireFields("mail");

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var emails = await db.SelectAsync("SELECT * FROM `email` WHERE `address`=?", (string)json["mail"]);
					c.Response.StatusCode = 200;
					c.Response.Content = new JsonObject
					{
						["available"] = (emails.Count() == 0)
					};
				}
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
				"SELECT `address` FROM `email` WHERE SUBSTRING(address,1,?)=? AND `type`='Ent'",
				beginEmail.Length, beginEmail)).Select((arg) => (string)arg["address"]);
			while (emails.Contains(aliasEmail))
			{
				aliasEmail = beginEmail + (++aliasIndex).ToString() + endEmail;
			}

			return aliasEmail;
		}
	}
}
