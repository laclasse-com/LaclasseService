// Log.cs
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
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class Logs : HttpRouting
	{
		readonly string dbUrl;

		public Logs(string dbUrl)
		{
			this.dbUrl = dbUrl;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();


			GetAsync["/{id:int}"] = async (p, c) =>
			{
				c.Response.StatusCode = 200;
				c.Response.Content = await GetLogAsync((int)p["id"]);
			};

			PostAsync["/"] = async (p, c) =>
			{
				var json = await c.Request.ReadAsJsonAsync();
				// check required fields
				json.RequireFields("application_id", "user_id", "structure_id", "profil_id", "url");
				var extracted = json.ExtractFields("application_id", "user_id", "structure_id", "profil_id", "url", "params");
				// append the sender IP address
				string ip = "unknown";
				if (c.Request.RemoteEndPoint is IPEndPoint)
					ip = ((IPEndPoint)c.Request.RemoteEndPoint).Address.ToString();
				if (c.Request.Headers.ContainsKey("x-forwarded-for"))
					ip = c.Request.Headers["x-forwarded-for"];
				extracted["ip"] = ip;
				extracted["timestamp"] = DateTime.Now;

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					if(await db.InsertRowAsync("log", extracted) == 1)
					{
						c.Response.StatusCode = 200;
						c.Response.Content = await GetLogAsync(db, (int)await db.LastInsertIdAsync());
					}
					else
						c.Response.StatusCode = 500;
				}
			};

			GetAsync["/stats"] = async (p, c) =>
			{
				c.Request.QueryString.RequireFields(
					"until", "from");

				var jsonResult = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					string filter = "";
					if (c.Request.QueryStringArray.ContainsKey("uais") &&
						c.Request.QueryStringArray["uais"].Count > 0)
						filter += " AND " + db.InFilter("uai", c.Request.QueryStringArray["uais"]);
					if (c.Request.QueryStringArray.ContainsKey("uids") &&
						c.Request.QueryStringArray["uids"].Count > 0)
						filter += " AND " + db.InFilter("uid", c.Request.QueryStringArray["uids"]);
					var items = await db.SelectAsync(
						$"SELECT * FROM log WHERE timestamp >= ? AND timestamp <= ? {filter}",
						DateTime.Parse(c.Request.QueryString["from"]),
						DateTime.Parse(c.Request.QueryString["until"]));
					foreach (var item in items)
					{
						jsonResult.Add(LogToJson(item));
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = jsonResult;
			};

		}

		JsonObject LogToJson(Dictionary<string, object> item)
		{
			return new JsonObject
			{
				["id"] = (int)item["id"],
				["ip"] = (string)item["ip"],
				["application_id"] = (string)item["application_id"],
				["user_id"] = (string)item["user_id"],
				["structure_id"] = (string)item["structure_id"],
				["profil_id"] = (string)item["profil_id"],
				["url"] = (string)item["url"],
				["params"] = (string)item["params"],
				["timestamp"] = (DateTime)item["timestamp"]
			};
		}

		public async Task<JsonObject> GetLogAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return await GetLogAsync(db, id);
			}
		}

		public async Task<JsonObject> GetLogAsync(DB db, int id)
		{
			var item = (await db.SelectAsync("SELECT * FROM log WHERE id=?", id)).First();
			return (item == null) ? null : LogToJson(item);
		}
	}
}
