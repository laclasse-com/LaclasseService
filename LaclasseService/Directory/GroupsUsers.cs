// GroupsUsers.cs
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

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "group_user", PrimaryKey = "id")]
	public class GroupUser : Model
	{
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField]
		public string type { get { return GetField<string>("type", null); } set { SetField("type", value); } }
		[ModelField(Required = true)]
		public int group_id { get { return GetField("group_id", 0); } set { SetField("group_id", value); } }
		[ModelField(Required = true)]
		public string user_id { get { return GetField<string>("user_id", null); } set { SetField("user_id", value); } }
		[ModelField]
		public string subject_id { get { return GetField<string>("subject_id", null); } set { SetField("subject_id", value); } }
		[ModelField]
		public DateTime ctime { get { return GetField("ctime", DateTime.Now); } set { SetField("ctime", value); } }
		[ModelField]
		public DateTime? aaf_mtime { get { return GetField<DateTime?>("aaf_mtime", null); } set { SetField("aaf_mtime", value); } }
		[ModelField]
		public bool pending_validation { get { return GetField("pending_validation", false); } set { SetField("pending_validation", value); } }
	}

	public class GroupsUsers : HttpRouting
	{
		readonly string dbUrl;

		public GroupsUsers(string dbUrl)
		{
			this.dbUrl = dbUrl;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
					c.Response.Content = await Model.SearchAsync<GroupUser>(
						db, new List<string> { "id", "type", "group_id", "user_id", "subject_id", "ctime", "pending_validation" }, c);
				c.Response.StatusCode = 200;
			};

			GetAsync["/{id:int}"] = async (p, c) =>
			{
				GroupUser groupUser = null;
				using (DB db = await DB.CreateAsync(dbUrl))
					groupUser = await db.SelectRowAsync<GroupUser>((int)p["id"]);
				if (groupUser != null)
				{
					c.Response.StatusCode = 200;
					c.Response.Content = groupUser;
				}
			};

			PostAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonArray)
				{
					var result = new JsonArray();
					using (DB db = await DB.CreateAsync(dbUrl, true))
					{
						foreach (var jsonItem in (JsonArray)json)
						{
							var item = Model.CreateFromJson<GroupUser>(jsonItem);
							result.Add(await item.SaveAsync(db));
						}
					}
					c.Response.StatusCode = 200;
					c.Response.Content = result;
				}
				else if (json is JsonObject)
				{
					var item = Model.CreateFromJson<GroupUser>(json);
					using (DB db = await DB.CreateAsync(dbUrl))
						await item.SaveAsync(db);
					c.Response.StatusCode = 200;
					c.Response.Content = item;
				}
			};

			PutAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonArray)
				{
					var result = new JsonArray();
					using (DB db = await DB.CreateAsync(dbUrl, true))
					{
						foreach (var jsonItem in (JsonArray)json)
						{
							var item = Model.CreateFromJson<GroupUser>(jsonItem);
							await item.UpdateAsync(db);
							result.Add(await item.LoadAsync(db));
						}
					}
					c.Response.StatusCode = 200;
					c.Response.Content = result;
				}
				else if (json is JsonObject)
				{
					var item = Model.CreateFromJson<GroupUser>(json);
					using (DB db = await DB.CreateAsync(dbUrl, true))
					{
						await item.UpdateAsync(db);
						await item.LoadAsync(db);
					}
					c.Response.StatusCode = 200;
					c.Response.Content = item;
				}
			};

			DeleteAsync["/{id:int}"] = async (p, c) =>
			{
				GroupUser item = null;
				using (DB db = await DB.CreateAsync(dbUrl, true))
				{
					item = await db.SelectRowAsync<GroupUser>((int)p["id"]);
					if (item != null)
						await item.DeleteAsync(db);
				}
				if (item != null)
					c.Response.StatusCode = 200;
			};
		}
	}
}
