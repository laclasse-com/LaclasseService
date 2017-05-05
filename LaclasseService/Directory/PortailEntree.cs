// PortailEntree.cs
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

using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class PortailEntree : HttpRouting
	{
		readonly string dbUrl;

		public PortailEntree(string dbUrl, Etablissements etabs)
		{
			this.dbUrl = dbUrl;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/{uai}/tuiles"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var etab = await etabs.GetEtablissementAsync(db, (string)p["uai"]);
					if (etab == null)
						c.Response.StatusCode = 404;
					else
					{
						var jsonResult = new JsonArray();
						foreach (var item in await db.SelectAsync("SELECT * FROM entree_portail WHERE etablissement_id=?", (int)etab["id"]))
						{
							jsonResult.Add(PortailEntreeToJson(item));
						}
						c.Response.StatusCode = 200;
						c.Response.Content = jsonResult;
					}
				}
			};

			PostAsync["/{uai}/tuiles"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonArray)
				{
					var jsonResult = new JsonArray();
					using (DB db = await DB.CreateAsync(dbUrl))
					{
						foreach (var jsonItem in (JsonArray)json)
							jsonResult.Add(await CreatePortailEntreeAsync(jsonItem));
					}
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = await CreatePortailEntreeAsync(json);
				}
			};

			PutAsync["/{uai}/tuiles/{id:int}"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields("name", "description", "color", "index");
				if (extracted.Count == 0)
					return;
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.UpdateRowAsync("entree_portail", "id", (int)p["id"], extracted);
					if (count > 0)
					{
						c.Response.StatusCode = 200;
						c.Response.Content = await GetPortailEntreeAsync(db, json["id"]);
					}
					else
						c.Response.StatusCode = 404;
				}
			};

			DeleteAsync["/{uai}/tuiles/{id:int}"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					if (await db.DeleteAsync("DELETE FROM entree_portail WHERE id=?", (int)p["id"]) == 1)
						c.Response.StatusCode = 200;
					else
						c.Response.StatusCode = 404;
				}
			};
		}

		JsonObject PortailEntreeToJson(Dictionary<string, object> item)
		{
			return new JsonObject
			{
				["id"] = (int)item["id"],
				["etablissement_id"] = (int)item["etablissement_id"],
				["application_id"] = (string)item["application_id"],
				["type"] = (string)item["type"],
				["name"] = (string)item["name"],
				["description"] = (string)item["description"],
				["url"] = (string)item["url"],
				["index"] = (int)item["index"],
				["color"] = (string)item["color"],
				["icon"] = (string)item["icon"]
			};
		}

		public async Task<JsonValue> GetPortailEntreeAsync(DB db, int id)
		{
			var item = (await db.SelectAsync("SELECT * FROM entree_portail WHERE id=?", id)).SingleOrDefault();
			return (item == null) ? null : PortailEntreeToJson(item);
		}

		public async Task<JsonValue> CreatePortailEntreeAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await CreatePortailEntreeAsync(db, json);
			}
		}

		public async Task<JsonValue> CreatePortailEntreeAsync(DB db, JsonValue json)
		{
			// check required fields
			json.RequireFields("etablissement_id", "index", "type");
			var extracted = json.ExtractFields(
				"etablissement_id", "index", "type", "application_id", "name", "description",
				"url", "icon", "color");

			JsonValue jsonResult = null;
			if (await db.InsertRowAsync("entree_portail", extracted) == 1)
				jsonResult = await GetPortailEntreeAsync(db, (int)await db.LastInsertIdAsync());
			return jsonResult;
		}
	}
}
