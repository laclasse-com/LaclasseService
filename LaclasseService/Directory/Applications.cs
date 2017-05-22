// Applications.cs
// 
//  Handle applications API. 
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
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "application", PrimaryKey = "id")]
	public class Application : Model
	{
		[ModelField(Required = true)]
		public string id { get { return GetField<string>("id", null); } set { SetField("id", value); } }
		[ModelField]
		public string name { get { return GetField<string>("name", null); } set { SetField("name", value); } }
		[ModelField(Required = true)]
		public string url { get { return GetField<string>("url", null); } set { SetField("url", value); } }
		[ModelField]
		public string password { get { return GetField<string>("password", null); } set { SetField("password", value); } }
	}

	public class Applications: HttpRouting
	{
		readonly string dbUrl;

		public Applications(string dbUrl)
		{
			this.dbUrl = dbUrl;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/"] = async (p, c) =>
			{
				var filter = "TRUE";
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					if (c.Request.QueryStringArray.ContainsKey("id"))
						filter = db.InFilter("id", c.Request.QueryStringArray["id"]);
					c.Response.Content = await db.SelectAsync<Application>($"SELECT * FROM application WHERE {filter}");
				}
				c.Response.StatusCode = 200;
			};

			GetAsync["/{id}"] = async (p, c) =>
			{
				var app = await GetApplicationAsync((string)p["id"]);
				if (app == null)
					c.Response.StatusCode = 404;
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = app;
				}
			};

			PostAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonArray)
				{
					var jsonResult = new JsonArray();
					using (DB db = await DB.CreateAsync(dbUrl))
					{
						foreach (var jsonItem in (JsonArray)json)
							jsonResult.Add(await CreateApplicationAsync(jsonItem));
					}
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = await CreateApplicationAsync(json);
				}
			};

			PutAsync["/{id}"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				var extracted = json.ExtractFields("name", "url", "password");
				if (extracted.Count == 0)
					return;
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.UpdateRowAsync("application", "id", p["id"], extracted);
					if (count > 0)
					{
						c.Response.StatusCode = 200;
						c.Response.Content = await GetApplicationAsync(db, json["id"]);
					}
					else
						c.Response.StatusCode = 404;
				}
			};

			DeleteAsync["/{id}"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.DeleteAsync("DELETE FROM application WHERE id=?", (string)p["id"]);
					if (count == 0)
						c.Response.StatusCode = 404;
					else
						c.Response.StatusCode = 200;
				}
			};

			DeleteAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				var ids = ((JsonArray)json).Select((arg) => (string)(arg.Value));

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.DeleteAsync("DELETE FROM application WHERE "+db.InFilter("id", ids));
					if (count == 0)
						c.Response.StatusCode = 404;
					else
						c.Response.StatusCode = 200;
				}
			};
		}

		public async Task<Application> GetApplicationAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await GetApplicationAsync(db, id);
		}

		public async Task<Application> GetApplicationAsync(DB db, string id)
		{
			return await db.SelectRowAsync<Application>(id);
		}

		public async Task<Application> CreateApplicationAsync(JsonValue json)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await CreateApplicationAsync(db, json);
		}

		public async Task<Application> CreateApplicationAsync(DB db, JsonValue json)
		{
			var extracted = json.ExtractFields("id", "name", "url", "password");
			// check required fields
			if (!extracted.ContainsKey("id") || !extracted.ContainsKey("url"))
				throw new WebException(400, "Bad protocol. 'id' and 'url' are needed");

			Application result = null;
			if (await db.InsertRowAsync("application", extracted) == 1)
				result = await GetApplicationAsync(db, json["id"]);
			return result;
		}

		public async Task<string> CheckPasswordAsync(string login, string password)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await CheckPasswordAsync(db, login, password);
		}

		public async Task<string> CheckPasswordAsync(DB db, string login, string password)
		{
			string user = null;
			var item = await db.SelectRowAsync<Application>(login);
			if ((item != null) && (item.password != null) && (password == item.password))
				user = item.id;
			return user;
		}
	}
}
