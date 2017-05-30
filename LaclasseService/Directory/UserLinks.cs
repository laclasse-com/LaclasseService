// UserLinks.cs
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
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class UserLinks : HttpRouting
	{
		public UserLinks(string dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/"] = async (p, c) =>
			{
				using (var db = await DB.CreateAsync(dbUrl))
				{
					var filter = "TRUE";
					if (c.Request.QueryString.ContainsKey("parent_id"))
						filter += $" AND `parent_id`='{db.EscapeString(c.Request.QueryString["parent_id"])}'";
					if (c.Request.QueryString.ContainsKey("type"))
						filter += $" AND `type`='{db.EscapeString(c.Request.QueryString["type"])}'";
					if (c.Request.QueryString.ContainsKey("child_id"))
						filter += $" AND `child_id`='{db.EscapeString(c.Request.QueryString["child_id"])}'";
					if (c.Request.QueryString.ContainsKey("id"))
						filter += $" AND `id`='{db.EscapeString(c.Request.QueryString["id"])}'";
					if (c.Request.QueryStringArray.ContainsKey("parent_id"))
						filter += " AND " + db.InFilter("parent_id", c.Request.QueryStringArray["parent_id"]);
					if (c.Request.QueryStringArray.ContainsKey("type"))
						filter += " AND " + db.InFilter("type", c.Request.QueryStringArray["type"]);
					if (c.Request.QueryStringArray.ContainsKey("type"))
						filter += " AND " + db.InFilter("child_id", c.Request.QueryStringArray["child_id"]);
					if (c.Request.QueryStringArray.ContainsKey("id"))
						filter += " AND " + db.InFilter("id", c.Request.QueryStringArray["id"]);
					c.Response.StatusCode = 200;
					c.Response.Content = await db.SelectAsync<UserChild>($"SELECT * FROM user_child WHERE {filter}");
				}
			};

			GetAsync["/{id:int}"] = async (p, c) =>
			{
				UserChild userChild = null;
				using (var db = await DB.CreateAsync(dbUrl))
					userChild = await db.SelectRowAsync<UserChild>((int)p["id"]);
				if (userChild != null)
				{
					c.Response.StatusCode = 200;
					c.Response.Content = userChild;
				}
			};

			PostAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonArray)
				{
					var result = new JsonArray();
					using (var db = await DB.CreateAsync(dbUrl))
					{
						foreach (var jsonChild in (JsonArray)json)
						{
							var userChild = Model.CreateFromJson<UserChild>(jsonChild);
							await userChild.SaveAsync(db);
							result.Add(userChild);
						}
					}
					c.Response.StatusCode = 200;
					c.Response.Content = result;
				}
				else if (json is JsonObject)
				{
					var userChild = Model.CreateFromJson<UserChild>(json);
					using (var db = await DB.CreateAsync(dbUrl))
						await userChild.SaveAsync(db);
					c.Response.StatusCode = 200;
					c.Response.Content = userChild;
				}
			};

			DeleteAsync["/{id:int}"] = async (p, c) =>
			{
				var userChild = new UserChild { id = (int)p["id"] };
				using (var db = await DB.CreateAsync(dbUrl))
					c.Response.StatusCode = await userChild.DeleteAsync(db) ? 200 : 404;
			};

			DeleteAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonArray)
				{
					using (var db = await DB.CreateAsync(dbUrl))
					{
						foreach (var jsonChild in (JsonArray)json)
						{
							var userChild = new UserChild { id = Convert.ToInt32(jsonChild.Value) };
							await userChild.DeleteAsync(db);
						}
					}
					c.Response.StatusCode = 200;
				}
			};
		}
	}
}
