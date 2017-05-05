// Profils.cs
// 
//  Handle profils API. 
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
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;

namespace Laclasse.Directory
{
	public class Profils : HttpRouting
	{
		readonly string dbUrl;

		public Profils(string dbUrl)
		{
			this.dbUrl = dbUrl;

			GetAsync["/"] = async (p, c) =>
			{
				var res = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var app in await db.SelectAsync("SELECT * FROM profil_national"))
					{
						res.Add(new JsonObject
						{
							["id"] = (string)app["id"],
							["description"] = (string)app["description"],
							["code_national"] = (string)app["code_national"]
						});
					}
				}
				c.Response.Content = res;
			};

			GetAsync["/fonctions"] = async (p, c) =>
			{
				var res = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var app in await db.SelectAsync("SELECT * FROM fonction"))
					{
						res.Add(new JsonObject
						{
							["id"] = (int)app["id"],
							["libelle"] = (string)app["libelle"],
							["description"] = (string)app["description"],
							["code_men"] = (string)app["code_men"]
						});
					}
				}
				c.Response.Content = res;
			};
		}

		public async Task<JsonArray> GetUserProfilsAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetUserProfilsAsync(db, id);
			}
		}

		public async Task<JsonArray> GetUserProfilsAsync(DB db, string id)
		{
			var res = new JsonArray();
			foreach (var profil in await db.SelectAsync(
				"SELECT * FROM profil_user "+
				"WHERE user_id=?", id))
			{
				res.Add(new JsonObject
				{
					["profil_id"] = (string)profil["profil_id"],
					["etablissement_id"] = (string)profil["etablissement_id"],
					["actif"] = (profil["actif"] != null) && Convert.ToBoolean(profil["actif"])
				});
			}
			return res;
		}

		public async Task<JsonArray> GetEtablissementProfilsAsync(DB db, string id)
		{
			var res = new JsonArray();
			foreach (var profil in await db.SelectAsync(
				"SELECT * FROM profil_user WHERE etablissement_id=?", id))
			{
				res.Add(new JsonObject
				{
					["profil_id"] = (string)profil["profil_id"],
					["user_id"] = (int)profil["user_id"]
				});
			}
			return res;
		}
	}
}
