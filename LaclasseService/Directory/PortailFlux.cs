// PortailFlux.cs
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

using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erasme.Http;
using Erasme.Json;

namespace Laclasse.Directory
{
	[Model(Table = "flux_portail", PrimaryKey = "id")]
	public class FluxPortail : Model
	{
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField]
		public int nb { get { return GetField("nb", 0); } set { SetField("nb", value); } }
		[ModelField]
		public string url { get { return GetField<string>("url", null); } set { SetField("url", value); } }
		[ModelField]
		public string name { get { return GetField<string>("name", null); } set { SetField("name", value); } }
		[ModelField]
		public string structure_id { get { return GetField<string>("structure_id", null); } set { SetField("structure_id", value); } }
	}

	public class PortailFlux : HttpRouting
	{
		readonly string dbUrl;

		public PortailFlux(string dbUrl)
		{
			this.dbUrl = dbUrl;

			GetAsync["/"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
					c.Response.Content = await db.SelectAsync<FluxPortail>("SELECT * FROM flux_portail");
				c.Response.StatusCode = 200;
			};

			GetAsync["/{uai}/flux"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
					c.Response.Content = await db.SelectAsync<FluxPortail>("SELECT * FROM flux_portail WHERE structure_id=?", (string)p["uai"]);
				c.Response.StatusCode = 200;
			};

			GetAsync["/{uai}/flux/{id:int}"] = async (p, c) =>
			{
				var jsonResult = await GetPortailFluxAsync((int)p["id"]);
				if (jsonResult == null)
					c.Response.StatusCode = 404;
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
			};

			PostAsync["/{uai}/flux"] = async (p, c) => 
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonArray)
				{
					var jsonResult = new JsonArray();
					using (DB db = await DB.CreateAsync(dbUrl))
					{
						foreach (var jsonItem in (JsonArray)json)
							jsonResult.Add(await CreatePortailFluxAsync(jsonItem));
					}
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = await CreatePortailFluxAsync(json);
				}
			};

			PutAsync["/{uai}/flux/{id:int}"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields("url", "nb", "name");
				if (extracted.Count == 0)
					return;
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.UpdateRowAsync("flux_portail", "id", p["id"], extracted);
					if (count > 0)
					{
						c.Response.StatusCode = 200;
						c.Response.Content = await GetPortailFluxAsync(db, json["id"]);
					}
					else
						c.Response.StatusCode = 404;
				}
			};

			DeleteAsync["/{uai}/flux/{id:int}"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					if (await db.DeleteAsync("DELETE FROM flux_portail WHERE id=?", p["id"]) == 1)
						c.Response.StatusCode = 200;
					else
						c.Response.StatusCode = 404;
				}
			};
		}

		public async Task<FluxPortail> GetPortailFluxAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await GetPortailFluxAsync(db, id);
		}

		public async Task<FluxPortail> GetPortailFluxAsync(DB db, int id)
		{
			return await db.SelectRowAsync<FluxPortail>(id);
		}

		public async Task<FluxPortail> CreatePortailFluxAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await CreatePortailFluxAsync(db, json);
		}

		public async Task<FluxPortail> CreatePortailFluxAsync(DB db, JsonValue json)
		{
			// check required fields
			json.RequireFields("structure_id", "url", "name");
			var extracted = json.ExtractFields("structure_id", "url", "nb", "name");

			FluxPortail result = null;
			if (await db.InsertRowAsync("flux_portail", extracted) == 1)
				result = await GetPortailFluxAsync(db, (int)await db.LastInsertIdAsync());
			return result;
		}
	}
}
