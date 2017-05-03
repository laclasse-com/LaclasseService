// Niveaux.cs
// 
//  Handle matieres API. 
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

using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class Niveaux : HttpRouting
	{
		readonly string dbUrl;

		public Niveaux(string dbUrl)
		{
			this.dbUrl = dbUrl;

			GetAsync["/"] = async (p, c) =>
			{
				var res = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var item in await db.SelectAsync("SELECT * FROM niveau"))
					{
						res.Add(NiveauToJson(item));
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = res;
			};

			GetAsync["/{id}"] = async (p, c) =>
			{
				var jsonResult = await GetNiveauAsync((string)p["id"]);
				if (jsonResult == null)
					c.Response.StatusCode = 404;
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
			};

			PostAsync["/"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields("ent_mef_jointure", "mef_libelle", "ent_mef_rattach", "ent_mef_stat");
				// check required fields
				if (!extracted.ContainsKey("ent_mef_jointure"))
					throw new WebException(400, "Bad protocol. 'ent_mef_jointure' field is needed");

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int res = await db.InsertRowAsync("niveau", extracted);
					if (res == 1)
					{
						var jsonResult = await GetNiveauAsync(db, json["ent_mef_jointure"]);
						if (jsonResult != null)
						{
							c.Response.StatusCode = 200;
							c.Response.Content = jsonResult;
						}
						else
							c.Response.StatusCode = 500;
					}
					else
						c.Response.StatusCode = 500;
				}
			};

			PutAsync["/{id}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields("mef_libelle", "ent_mef_rattach", "ent_mef_stat");
				if (extracted.Count == 0)
					return;
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.UpdateRowAsync("niveau", "ent_mef_jointure", p["id"], extracted);
					if (count > 0)
					{
						c.Response.StatusCode = 200;
						c.Response.Content = await GetNiveauAsync(db, (string)p["id"]);
					}
					else
						c.Response.StatusCode = 404;
				}
			};

			DeleteAsync["/{id}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.DeleteAsync("DELETE FROM niveau WHERE ent_mef_jointure=?", (string)p["id"]);
					if (count == 0)
						c.Response.StatusCode = 404;
					else
						c.Response.StatusCode = 200;
				}
			};
		}

		JsonObject NiveauToJson(Dictionary<string, object> item)
		{
			return new JsonObject
			{
				["ent_mef_jointure"] = (string)item["ent_mef_jointure"],
				["mef_libelle"] = (string)item["mef_libelle"],
				["ent_mef_rattach"] = (string)item["ent_mef_rattach"],
				["ent_mef_stat"] = (string)item["ent_mef_stat"]
			};
		}

		public async Task<JsonValue> GetNiveauAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetNiveauAsync(db, id);
			}
		}

		public async Task<JsonValue> GetNiveauAsync(DB db, string id)
		{
			var item = (await db.SelectAsync("SELECT * FROM niveau WHERE ent_mef_jointure=?", id)).SingleOrDefault();
			return (item == null) ? null : NiveauToJson(item);
		}

		public async Task<string> GetNiveauLibelleAsync(DB db, string id)
		{
			return (string)await db.ExecuteScalarAsync("SELECT mef_libelle FROM niveau WHERE ent_mef_jointure=?", id);
		}
	}
}
