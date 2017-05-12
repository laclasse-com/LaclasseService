// Profiles.cs
// 
//  Handle profiles API. 
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
	public class Profiles : HttpRouting
	{
		readonly string dbUrl;

		public Profiles(string dbUrl)
		{
			this.dbUrl = dbUrl;

			GetAsync["/"] = async (p, c) =>
			{
				var res = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var app in await db.SelectAsync("SELECT * FROM profile_type"))
					{
						res.Add(new JsonObject
						{
							["id"] = (string)app["id"],
							["name"] = (string)app["name"],
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

		public async Task<JsonArray> GetUserProfilesAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetUserProfilesAsync(db, id);
			}
		}

		public async Task<JsonArray> GetUserProfilesAsync(DB db, string id)
		{
			var res = new JsonArray();
			foreach (var profile in await db.SelectAsync(
				"SELECT * FROM user_profile "+
				"WHERE user_id=?", id))
			{
				res.Add(new JsonObject
				{
					["id"] = (int)profile["id"],
					["type"] = (string)profile["type"],
					["structure_id"] = (string)profile["structure_id"],
					["active"] = (profile["active"] != null) && Convert.ToBoolean(profile["active"])
				});
			}
			return res;
		}

		public async Task<JsonArray> GetStructureProfilesAsync(DB db, string id)
		{
			var res = new JsonArray();
			foreach (var profile in await db.SelectAsync(
				"SELECT * FROM user_profile WHERE structure_id=?", id))
			{
				res.Add(new JsonObject
				{
					["id"] = (int)profile["id"],
					["type"] = (string)profile["type"],
					["user_id"] = (string)profile["user_id"]
				});
			}
			return res;
		}
	}
}
