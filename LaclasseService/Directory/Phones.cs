// Phones.cs
// 
//  Handle phones API. 
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

using System.Collections.Generic;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{

	[Model(Table = "phone", PrimaryKey = "id")]
	public class Phone : Model
	{
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField]
		public string number { get { return GetField<string>("number", null); } set { SetField("number", value); } }
		[ModelField]
		public string type { get { return GetField<string>("type", null); } set { SetField("type", value); } }
		[ModelField]
		public string user_id { get { return GetField<string>("user_id", null); } set { SetField("user_id", value); } }
	}

	public class Phones : HttpRouting
	{
		public Phones(string dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/{id:int}"] = async (p, c) =>
			{
				Phone phone = null;
				using (DB db = await DB.CreateAsync(dbUrl))
					phone = await db.SelectRowAsync<Phone>((int)p["id"]);
				if (phone != null)
				{
					c.Response.StatusCode = 200;
					c.Response.Content = phone;
				}
			};

			GetAsync["/"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
					c.Response.Content = await Model.SearchAsync<Phone>(
						db, new List<string> { "id", "number", "user_id", "type" }, c);
				c.Response.StatusCode = 200;
			};

			PostAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				if (json is JsonArray)
				{
					var result = new JsonArray();
					using (DB db = await DB.CreateAsync(dbUrl, true))
					{
						foreach (var jsonPhone in (JsonArray)json)
						{
							var phone = Model.CreateFromJson<Phone>(jsonPhone);
							result.Add(await phone.SaveAsync(db));
						}
					}
					c.Response.StatusCode = 200;
					c.Response.Content = result;
				}
				else if (json is JsonObject)
				{
					var email = Model.CreateFromJson<Phone>(json);
					using (DB db = await DB.CreateAsync(dbUrl))
						await email.SaveAsync(db);
					c.Response.StatusCode = 200;
					c.Response.Content = email;
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
						foreach (var jsonPhone in (JsonArray)json)
						{
							var phone = Model.CreateFromJson<Phone>(jsonPhone);
							await phone.UpdateAsync(db);
							result.Add(await phone.LoadAsync(db));
						}
					}
					c.Response.StatusCode = 200;
					c.Response.Content = result;
				}
				else if (json is JsonObject)
				{
					var phone = Model.CreateFromJson<Phone>(json);
					using (DB db = await DB.CreateAsync(dbUrl, true))
					{
						await phone.UpdateAsync(db);
						await phone.LoadAsync(db);
					}
					c.Response.StatusCode = 200;
					c.Response.Content = phone;
				}
			};

			DeleteAsync["/{id:int}"] = async (p, c) =>
			{
				Phone phone = null;
				using (DB db = await DB.CreateAsync(dbUrl, true))
				{
					phone = await db.SelectRowAsync<Phone>((int)p["id"]);
					if (phone != null)
						await phone.DeleteAsync(db);
				}
				if (phone != null)
					c.Response.StatusCode = 200;
			};
		}
	}
}
