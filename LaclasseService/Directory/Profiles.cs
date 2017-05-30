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
using System.Linq;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "user_profile", PrimaryKey = "id")]
	public class UserProfile : Model
	{
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField]
		public string type { get { return GetField<string>("type", null); } set { SetField("type", value); } }
		[ModelField]
		public string structure_id { get { return GetField<string>("structure_id", null); } set { SetField("structure_id", value); } }
		[ModelField]
		public string user_id { get { return GetField<string>("user_id", null); } set { SetField("user_id", value); } }
		[ModelField]
		public bool active { get { return GetField("active", false); } set { SetField("active", value); } }
		[ModelField]
		public DateTime? aaf_mtime { get { return GetField<DateTime?>("aaf_mtime", null); } set { SetField("aaf_mtime", value); } }
	}

	public class Profiles : HttpRouting
	{
		readonly string dbUrl;

		public Profiles(string dbUrl)
		{
			this.dbUrl = dbUrl;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var filter = "TRUE";
					if (c.Request.QueryString.ContainsKey("structure_id"))
						filter += $" AND `structure_id`='{db.EscapeString(c.Request.QueryString["structure_id"])}'";
					if (c.Request.QueryString.ContainsKey("type"))
						filter += $" AND `type`='{db.EscapeString(c.Request.QueryString["type"])}'";
					if (c.Request.QueryString.ContainsKey("user_id"))
						filter += $" AND `user_id`='{db.EscapeString(c.Request.QueryString["user_id"])}'";
					if (c.Request.QueryString.ContainsKey("id"))
						filter += $" AND `id`='{db.EscapeString(c.Request.QueryString["id"])}'";
					if (c.Request.QueryStringArray.ContainsKey("structure_id"))
						filter += " AND " + db.InFilter("structure_id", c.Request.QueryStringArray["structure_id"]);
					if (c.Request.QueryStringArray.ContainsKey("user_id"))
						filter += " AND " + db.InFilter("user_id", c.Request.QueryStringArray["user_id"]);
					if (c.Request.QueryStringArray.ContainsKey("type"))
						filter += " AND " + db.InFilter("type", c.Request.QueryStringArray["type"]);
					if (c.Request.QueryStringArray.ContainsKey("id"))
						filter += " AND " + db.InFilter("id", c.Request.QueryStringArray["id"]);

					System.Console.WriteLine($"SELECT * FROM user_profile WHERE {filter}");

					c.Response.Content = await db.SelectAsync<UserProfile>($"SELECT * FROM user_profile WHERE {filter}");
				}
				c.Response.StatusCode = 200;
			};

			GetAsync["/{id:int}"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var item = await db.SelectRowAsync<UserProfile>((int)p["id"]);
					if (item != null) {
						c.Response.StatusCode = 200;
						c.Response.Content = item;
					}
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
							jsonResult.Add(await Model.CreateFromJson<UserProfile>(jsonItem).SaveAsync(db));
					}
					c.Response.StatusCode = 200;
					c.Response.Content = jsonResult;
				}
				else
				{
					var profile = Model.CreateFromJson<UserProfile>(json);
					using (DB db = await DB.CreateAsync(dbUrl))
						await profile.SaveAsync(db);
					c.Response.StatusCode = 200;
					c.Response.Content = profile;
				}
			};

			DeleteAsync["/{id:int}"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.DeleteAsync("DELETE FROM user_profile WHERE id=?", (int)p["id"]);
					if (count == 0)
						c.Response.StatusCode = 404;
					else
						c.Response.StatusCode = 200;
				}
			};

			DeleteAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				var ids = ((JsonArray)json).Select((arg) => Convert.ToInt32(arg.Value));

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					int count = await db.DeleteAsync("DELETE FROM user_profile WHERE " + db.InFilter("id", ids));
					if (count == 0)
						c.Response.StatusCode = 404;
					else
						c.Response.StatusCode = 200;
				}
			};

		}

		public async Task<ModelList<UserProfile>> GetUserProfilesAsync(string id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await GetUserProfilesAsync(db, id);
		}

		public async Task<ModelList<UserProfile>> GetUserProfilesAsync(DB db, string id)
		{
			var profiles = await db.SelectAsync<UserProfile>("SELECT * FROM user_profile WHERE user_id=?", id);
			await EnsureUserHasActiveProfile(db, profiles);
			return profiles;
		}

		public async Task<ModelList<UserProfile>> GetStructureProfilesAsync(DB db, string id)
		{
			return await db.SelectAsync<UserProfile>("SELECT * FROM user_profile WHERE structure_id=?", id);
		}

		internal static async Task EnsureUserHasActiveProfile(DB db, ModelList<UserProfile> profiles)
		{
			if (!profiles.Any((arg) => arg.active) && (profiles.Count > 0))
			{
				await profiles[0].DiffWithId(new UserProfile { active = true }).UpdateAsync(db);
				await profiles[0].LoadAsync(db);
			}
		}
	}
}
