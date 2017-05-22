// Tiles.cs
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

using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "tile", PrimaryKey = "id")]
	public class Tile : Model
	{
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField(Required = true)]
		public string structure_id { get { return GetField<string>("structure_id", null); } set { SetField("structure_id", value); } }
		[ModelField]
		public string application_id { get { return GetField<string>("application_id", null); } set { SetField("application_id", value); } }
		[ModelField(Required = true)]
		public string type { get { return GetField<string>("type", null); } set { SetField("type", value); } }
		[ModelField]
		public string name { get { return GetField<string>("name", null); } set { SetField("name", value); } }
		[ModelField]
		public string description { get { return GetField<string>("description", null); } set { SetField("description", value); } }
		[ModelField]
		public string url { get { return GetField<string>("url", null); } set { SetField("url", value); } }
		[ModelField(Required = true)]
		public int index { get { return GetField("index", 0); } set { SetField("index", value); } }
		[ModelField]
		public string color { get { return GetField<string>("color", null); } set { SetField("color", value); } }
		[ModelField]
		public string icon { get { return GetField<string>("icon", null); } set { SetField("icon", value); } }
	}

	public class Tiles : HttpRouting
	{
		readonly string dbUrl;

		public Tiles(string dbUrl)
		{
			this.dbUrl = dbUrl;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/{uai}/tiles"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					c.Response.StatusCode = 200;
					c.Response.Content = await db.SelectAsync<Tile>("SELECT * FROM tile WHERE structure_id=?", (string)p["uai"]);
				}
			};

			PostAsync["/{uai}/tiles"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonArray)
				{
					var jsonResult = new JsonArray();
					using (DB db = await DB.CreateAsync(dbUrl))
					{
						foreach (var jsonItem in (JsonArray)json)
							jsonResult.Add(await CreateTileAsync(jsonItem));
					}
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = await CreateTileAsync(json);
				}
			};

			PutAsync["/{uai}/tiles/{id:int}"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields("name", "description", "color", "index");
				if (extracted.Count == 0)
					return;
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.UpdateRowAsync("tile", "id", (int)p["id"], extracted);
					if (count > 0)
					{
						c.Response.StatusCode = 200;
						c.Response.Content = await GetTileAsync(db, json["id"]);
					}
					else
						c.Response.StatusCode = 404;
				}
			};

			DeleteAsync["/{uai}/tiles/{id:int}"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					if (await db.DeleteAsync("DELETE FROM tile WHERE id=? AND structure_id=?", (int)p["id"], (string)p["uai"]) == 1)
						c.Response.StatusCode = 200;
					else
						c.Response.StatusCode = 404;
				}
			};
		}

		public async Task<Tile> GetTileAsync(DB db, int id)
		{
			return await db.SelectRowAsync<Tile>(id);
		}

		public async Task<Tile> CreateTileAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await CreateTileAsync(db, json);
		}

		public async Task<Tile> CreateTileAsync(DB db, JsonValue json)
		{
			// check required fields
			json.RequireFields("structure_id", "index", "type");
			var extracted = json.ExtractFields(
				"structure_id", "index", "type", "application_id", "name", "description",
				"url", "icon", "color");

			Tile tile = null;
			if (await db.InsertRowAsync("tile", extracted) == 1)
				tile = await GetTileAsync(db, (int)await db.LastInsertIdAsync());
			return tile;
		}
	}
}
