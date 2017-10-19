// Sso.cs
// 
//  API for the SSO 
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
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class Sso : HttpRouting
	{
		readonly string dbUrl;

		public Sso(string dbUrl, Users users)
		{
			this.dbUrl = dbUrl;

			GetAsync["/"] = async (p, c) =>
			{
				if (!c.Request.QueryString.ContainsKey("login") || !c.Request.QueryString.ContainsKey("password"))
					throw new WebException(400, "Bad protocol. 'login' and 'password' are needed");

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var uid = await users.CheckPasswordAsync(
						db, c.Request.QueryString["login"], c.Request.QueryString["password"]);
					if (uid == null)
						c.Response.StatusCode = 403;
					else
					{
						c.Response.StatusCode = 200;
						c.Response.Content = await users.GetUserAsync(db, uid);
					}
				}
			};

			GetAsync["/sso_attributes/{login}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				var attributes = await GetUserSsoAttributesAsync((string)p["login"]);
				if (attributes != null)
				{
					c.Response.StatusCode = 200;
					c.Response.Content = attributes;
				}
				else
					c.Response.StatusCode = 404;
			};

			GetAsync["/sso_attributes_men/{login}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				var attributes = await GetUserSsoAttributesAsync((string)p["login"]);
				if (attributes != null)
				{
					c.Response.StatusCode = 200;
					c.Response.Content = attributes;
				}
				else
					c.Response.StatusCode = 404;
			};

			GetAsync["/pronote/{login}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				var attributes = await GetUserSsoAttributesAsync((string)p["login"]);
				if (attributes != null)
				{
					c.Response.StatusCode = 200;
					c.Response.Content = attributes;
				}
				else
					c.Response.StatusCode = 404;
			};

			GetAsync["/nginx"] = async (p, c) =>
			{
				// ensure super admin only
				var authUser = await c.GetAuthenticatedUserAsync();
				if ((authUser == null) || !authUser.IsSuperAdmin)
				{
					c.Response.StatusCode = 200;
					c.Response.Headers["Auth-Status"] = "Invalid login or password";
					return;
				}

				string userId = null;
				// check for HTTP Basic authorization
				if (c.Request.Headers.ContainsKey("auth-user") && c.Request.Headers.ContainsKey("auth-pass"))
				{
					var login = c.Request.Headers["auth-user"];
					var password = c.Request.Headers["auth-pass"];
					// check in the users
					userId = await ((Users)c.Data["users"]).CheckPasswordAsync(login, password);
				}
				if (userId == null)
				{
					c.Response.StatusCode = 200;
					c.Response.Headers["Auth-Status"] = "Invalid login or password";
				}
				else
				{
					using (DB db = await DB.CreateAsync(dbUrl))
					{
						var user = await users.GetUserAsync(userId);

						// update the user atime field
						var userDiff = new User { id = userId, atime = DateTime.Now };
						await userDiff.UpdateAsync(db);

						var emailBackend = (await db.SelectAsync("SELECT * FROM email_backend WHERE id=?", user.email_backend_id)).SingleOrDefault();
						if (emailBackend == null)
							throw new WebException(500, "email_backend not found");

						c.Response.StatusCode = 200;
						c.Response.Headers["Auth-User"] = user.id + "@" + emailBackend["address"];
						c.Response.Headers["Auth-Status"] = "OK";

						c.Response.Headers["Auth-Server"] = (string)emailBackend["ip_address"];
						if (c.Request.Headers.ContainsKey("auth-protocol"))
						{
							switch (c.Request.Headers["auth-protocol"])
							{
								case "smtp":
									c.Response.Headers["Auth-Port"] = "25";
									break;
								case "pop":
									c.Response.Headers["Auth-Port"] = "110";
									break;
								case "pop3":
									c.Response.Headers["Auth-Port"] = "110";
									break;
								case "imap":
									c.Response.Headers["Auth-Port"] = "143";
									break;
								default:
									c.Response.Headers["Auth-Port"] = "80";
									break;
							}
						}
						c.Response.Headers["Auth-Pass"] = (string)emailBackend["master_key"];
					}
				}
			};
		}

		static Dictionary<string, string> ProfilIdToSdet3 = new Dictionary<string, string>
		{
			["CPE"] = "National_5",
			["AED"] = "National_5",
			["EVS"] = "National_5",
			["ENS"] = "National_3",
			["ELV"] = "National_1",
			["ETA"] = "National_6",
			["ACA"] = "National_7",
			["DIR"] = "National_4",
			["TUT"] = "National_2",
			["COL"] = "National_4",
			["DOC"] = "National_3"
		};

		JsonValue UserToSsoAttributes(User user)
		{
			// TODO: add ENTEleveClasses and ENTEleveNivFormation

			string ENTPersonStructRattachRNE = null;
			string ENTPersonProfils = null;
			string categories = null;
			foreach (var p in user.profiles)
			{
				if (p.active)
				{
					ENTPersonStructRattachRNE = p.structure_id;
					if (ProfilIdToSdet3.ContainsKey(p.type))
						categories = ProfilIdToSdet3[p.type];
					if (p.type == "ELV")
					{
					}
				}

				if (ENTPersonProfils == null)
					ENTPersonProfils = "";
				else
					ENTPersonProfils += ",";
				ENTPersonProfils += p.type + ":" + p.structure_id;
			}

			return new JsonObject
			{
				["uid"] = user.id,
				["user"] = user.id,
				["login"] = user.login,
				["nom"] = user.lastname,
				["prenom"] = user.firstname,
				["dateNaissance"] = (user.birthdate == null) ? null : ((DateTime)user.birthdate).ToString("yyyy-MM-dd"),
				["codePostal"] = user.zip_code,
				["ENTPersonProfils"] = ENTPersonProfils,
				["ENTPersonStructRattach"] = ENTPersonStructRattachRNE,
				["ENTPersonStructRattachRNE"] = ENTPersonStructRattachRNE,
				["categories"] = categories,
				["LaclasseNom"] = user.lastname,
				["LaclassePrenom"] = user.firstname
			};
		}

		public async Task<JsonValue> GetUserSsoAttributesAsync(string login)
		{
			JsonValue attributes = null;
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				var item = (await db.SelectExpandAsync<User>("SELECT * FROM `user` WHERE `login`=?", new object[] { login })).SingleOrDefault();
				if (item != null)
					attributes = UserToSsoAttributes(item);
			}
			return attributes;
		}
	}
}
