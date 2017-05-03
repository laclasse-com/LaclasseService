// Group.cs
// 
//  Handle groups API. 
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
using System.Collections.Generic;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class Groups : HttpRouting
	{
		readonly string dbUrl;
		readonly Niveaux niveaux;

		public Groups(string dbUrl, Niveaux niveaux)
		{
			this.dbUrl = dbUrl;
			this.niveaux = niveaux;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/{id:int}"] = async (p, c) =>
			{
				var json = await GetGroupAsync((int)p["id"]);
				if (json == null)
					c.Response.StatusCode = 404;
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = json;
				}
			};

			PostAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				c.Response.StatusCode = 200;
				c.Response.Content = await CreateGroupAsync(json);
			};

			DeleteAsync["/{id:int}"] = async (p, c) =>
			{
				if(await DeleteGroupAsync((int)p["id"]))
					c.Response.StatusCode = 200;
				else
					c.Response.StatusCode = 404;
			};
		}

		async Task<JsonObject> GroupToJsonAsync(DB db, Dictionary<string, object> group)
		{
			var id = (int)group["id"];
			var teachersCount = (long)await db.ExecuteScalarAsync("SELECT COUNT(*) FROM enseigne_dans_regroupement WHERE regroupement_id=?", id);
			var studentsCount = (long)await db.ExecuteScalarAsync("SELECT COUNT(*) FROM eleve_dans_regroupement WHERE regroupement_id=?", id);
			var code_mef_aaf = (string)group["code_mef_aaf"];
			var mef_libelle = await niveaux.GetNiveauLibelleAsync(db, code_mef_aaf);

			return new JsonObject
			{
				["id"] = id,
				["libelle"] = (string)group["libelle"],
				["description"] = (string)group["description"],
				["date_last_maj_aaf"] = (DateTime?)group["date_last_maj_aaf"],
				["libelle_aaf"] = (string)group["libelle_aaf"],
				["type_regroupement_id"] = (string)group["type_regroupement_id"],
				["code_mef_aaf"] = code_mef_aaf,
				["mef_libelle"] = mef_libelle,
				["etablissement_id"] = (int?)group["etablissement_id"],
				["date_creation"] = (DateTime)group["date_creation"],
				["profs"] = teachersCount,
				["eleves"] = studentsCount
			};
		}

		async Task<JsonObject> GroupFreeToJsonAsync(DB db, Dictionary<string, object> group)
		{
			var id = (int)group["id"];
			var membersCount = (long)await db.ExecuteScalarAsync("SELECT COUNT(*) FROM membre_regroupement_libre WHERE regroupement_libre_id=?", id);

			return new JsonObject
			{
				["id"] = (int)group["id"],
				["type_regroupement_id"] = "GPL",
				["libelle"] = (string)group["libelle"],
				["created_at"] = (DateTime?)group["created_at"],
				["created_by"] = (int)group["created_by"],
				["membres"] = membersCount,
				["is_public"] = (bool)group["is_public"]
			};
		}

		public async Task<JsonValue> GetGroupAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetGroupAsync(db, id);
			}
		}

		public async Task<JsonValue> GetGroupAsync(DB db, int id)
		{
			var group = (await db.SelectAsync("SELECT * FROM regroupement WHERE id=?", id)).First();
			return (group == null) ? null : await GroupToJsonAsync(db, group);
		}

		public async Task<JsonValue> GetGroupFreeAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetGroupFreeAsync(db, id);
			}
		}

		public async Task<JsonValue> GetGroupFreeAsync(DB db, int id)
		{
			var group = (await db.SelectAsync("SELECT * FROM regroupement_libre WHERE id=?", id)).First();
			return (group == null) ? null : await GroupFreeToJsonAsync(db, group);
		}

		public async Task<JsonValue> CreateGroupAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await CreateGroupAsync(db, json);
			}
		}

		public async Task<JsonValue> CreateGroupAsync(DB db, JsonValue json)
		{
			JsonValue jsonResult = null;
			var extracted = json.ExtractFields(
				"libelle", "description", "libelle_aaf", "type_regroupement_id",
				"code_mef_aaf", "etablissement_id");
			// check required fields
			if (!extracted.ContainsKey("libelle") || !extracted.ContainsKey("libelle"))
				throw new WebException(400, "Bad protocol. 'libelle' and 'type_regroupement_id' are needed");
			if(((string)extracted["type_regroupement_id"] != "GRP") && ((string)extracted["type_regroupement_id"] != "CLS"))
				throw new WebException(400, "Bad protocol. 'type_regroupement_id' values are (CLS|GRP|GPL)");
			
			int res = await db.InsertRowAsync("regroupement", extracted);
			if (res == 1)
				jsonResult = await GetGroupAsync(db, (int)await db.LastInsertIdAsync());
			return jsonResult;
		}

		public async Task<bool> DeleteGroupAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await DeleteGroupAsync(db, id);
			}
		}

		public async Task<bool> DeleteGroupAsync(DB db, int id)
		{
			return (await db.DeleteAsync("DELETE FROM regroupement WHERE id=?", id)) != 0;
		}

		public async Task<JsonArray> GetUserGroupsAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetUserGroupsAsync(db, id);
			}
		}

		public async Task<JsonArray> GetUserGroupsAsync(DB db, int id)
		{
			var res = new JsonArray();
			foreach (var inGroup in await db.SelectAsync("SELECT * FROM enseigne_dans_regroupement WHERE user_id=?", id))
			{
				res.Add(new JsonObject
				{
					["type"] = "ENS",
					["regroupement_id"] = (int)inGroup["regroupement_id"],
					["matiere_enseignee_id"] = (string)inGroup["matiere_enseignee_id"],
					["prof_principal"] = (string)inGroup["prof_principal"]
				});
			}

			foreach (var inGroup in await db.SelectAsync("SELECT * FROM eleve_dans_regroupement WHERE user_id=?", id))
			{
				res.Add(new JsonObject
				{
					["type"] = "ELV",
					["regroupement_id"] = (int)inGroup["regroupement_id"]
				});
			}

			foreach (var inGroup in await db.SelectAsync("SELECT * FROM membre_regroupement_libre WHERE user_id=?", id))
			{
				res.Add(new JsonObject
				{
					["type"] = "MBR",
					["regroupement_id"] = (int)inGroup["regroupement_libre_id"],
					["regroupement_libre_id"] = (int)inGroup["regroupement_libre_id"],
					["joined_at"] = (DateTime?)inGroup["joined_at"]
				});
			}
			return res;
		}

		public async Task<JsonArray> GetEtablissementGroupsAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetEtablissementGroupsAsync(db, id);
			}
		}

		public async Task<JsonArray> GetEtablissementGroupsAsync(DB db, int id)
		{
			var res = new JsonArray();
			foreach (var group in await db.SelectAsync("SELECT * FROM regroupement WHERE etablissement_id=?", id))
			{
				res.Add(await GroupToJsonAsync(db, group));
			}
			return res;
		}

		public async Task<JsonArray> GetGroupsFreeAsync()
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetGroupsFreeAsync(db);
			}
		}

		public async Task<JsonArray> GetGroupsFreeAsync(DB db)
		{
			var res = new JsonArray();
			foreach (var item in await db.SelectAsync("SELECT * FROM regroupement_libre"))
			{
				res.Add(await GroupFreeToJsonAsync(db, item));
			}
			return res;
		}

	}
}
