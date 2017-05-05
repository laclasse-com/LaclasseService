// Emails.cs
// 
//  Handle profils API. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
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
				if (!json.ContainsKey("prenom") || !json.ContainsKey("nom"))
					throw new WebException(400, "Missing arguments");
				c.Response.StatusCode = 200;
				c.Response.Content = new JsonPrimitive(await OfferEntEmailAsync(json["prenom"], json["nom"]));
			};

			GetAsync["/mail_available"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (!json.ContainsKey("mail"))
					throw new WebException(400, "Missing arguments");

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var emails = await db.SelectAsync("SELECT * FROM email WHERE adresse=?", (string)json["mail"]);
					c.Response.StatusCode = 200;
					c.Response.Content = new JsonObject
					{
						["available"] = (emails.Count() == 0)
					};
				}
			};
		}

		public async Task<string> OfferEntEmailAsync(string prenom, string nom)
		{
			var beginEmail = (prenom.RemoveDiacritics() + "." + nom.RemoveDiacritics()).ToLower();
			beginEmail = Regex.Replace(beginEmail.Replace(' ', '-'), @"[^\.\-a-z0-9]", "", RegexOptions.IgnoreCase);
			string endEmail;
			string aliasEmail;
			int aliasIndex = 0;
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				var ent = (await db.SelectAsync("SELECT * FROM ent WHERE code='Laclasse'")).First();
				if (ent == null)
					throw new Exception("The ENT is not defined in the DB");
				endEmail = "@" + ent["mail_domaine"];

				aliasEmail = beginEmail + endEmail;

				var emails = (await db.SelectAsync(
					"SELECT adresse FROM email WHERE SUBSTRING(adresse,1,?)=? AND type='Ent'",
					beginEmail.Length, beginEmail)).Select((arg) => (string)arg["adresse"]);
				while (emails.Contains(aliasEmail))
				{
					aliasEmail = beginEmail + (++aliasIndex).ToString() + endEmail;
				}
			}
			return aliasEmail;
		}

		public async Task<JsonArray> GetUserEmailsAsync(string uid)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetUserEmailsAsync(db, uid);
			}
		}

		public async Task<JsonArray> GetUserEmailsAsync(DB db, string uid)
		{
			var res = new JsonArray();
			foreach (var email in await db.SelectAsync("SELECT * FROM email WHERE user_id=?", uid))
			{
				res.Add(new JsonObject
				{
					["id"] = (int)email["id"],
					["adresse"] = (string)email["adresse"],
					["principal"] = (bool)email["principal"],
					["type"] = (string)email["type"]
				});
			}
			return res;
		}
	}
}
