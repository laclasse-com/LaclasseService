// Resources.cs
// 
//  Handle resources API. 
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

using System;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "resource", PrimaryKey = "id")]
	public class Resource : Model
	{
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField]
		public string name { get { return GetField<string>("name", null); } set { SetField("name", value); } }
		[ModelField]
		public string url { get { return GetField<string>("url", null); } set { SetField("url", value); } }
		[ModelField]
		public string site_web { get { return GetField<string>("site_web", null); } set { SetField("site_web", value); } }
		[ModelField]
		public DateTime? mtime { get { return GetField<DateTime?>("mtime", null); } set { SetField("mtime", value); } }
		[ModelField]
		public string type { get { return GetField<string>("type", null); } set { SetField("type", value); } }
	}

	public class Resources: HttpRouting
	{
		string DBUrl;

		public Resources(string dbUrl)
		{
			DBUrl = dbUrl;

			GetAsync["/"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
					c.Response.Content = await db.SelectAsync<Resource>("SELECT * FROM resource");
				c.Response.StatusCode = 200;
			};

			GetAsync["/{id:int}"] = async (p, c) =>
			{
				var json = await GetResourceAsync((int)p["id"]);
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
				await c.EnsureIsAuthenticatedAsync();

				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields(
					"name", "url", "site_web", "resource");
				// check required fields
				if (!extracted.ContainsKey("lib"))
					throw new WebException(400, "Missing arguments");

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int res = await db.InsertRowAsync("resource", extracted);
					if (res == 1)
					{
						var jsonResult = await GetResourceAsync(db, (int)await db.LastInsertIdAsync());
						if(jsonResult != null) 
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

			PutAsync["/{id:int}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields(
					"name", "url", "site_web", "type");
				JsonValue jsonResult = null;
				if (extracted.Count > 0)
				{
					using (DB db = await DB.CreateAsync(dbUrl))
					{
						if ((await db.UpdateRowAsync("resource", "id", p["id"], extracted)) > 0)
							jsonResult = await GetResourceAsync(db, (int)p["id"]);
					}
				}
				if (jsonResult == null)
					c.Response.StatusCode = 404;
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
			};

			DeleteAsync["/{id:int}"] = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.DeleteAsync("DELETE FROM resource WHERE id=?", (int)p["id"]);
					if (count == 0)
						c.Response.StatusCode = 404;
					else
						c.Response.StatusCode = 200;
				}
			};
		}

		public async Task<Resource> GetResourceAsync(int id)
		{
			using (DB db = await DB.CreateAsync(DBUrl))
				return await GetResourceAsync(db, id);
		}

		public async Task<Resource> GetResourceAsync(DB db, int id)
		{
			return await db.SelectRowAsync<Resource>(id);
		}
	}
}
