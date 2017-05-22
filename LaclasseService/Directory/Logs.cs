// Log.cs
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
using System.Net;
using System.Threading.Tasks;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "log", PrimaryKey = "id")]
	public class Log : Model
	{
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField(Required = true)]
		public string ip { get { return GetField<string>("ip", null); } set { SetField("ip", value); } }
		[ModelField(Required = true)]
		public string application_id { get { return GetField<string>("application_id", null); } set { SetField("application_id", value); } }
		[ModelField(Required = true)]
		public string user_id { get { return GetField<string>("user_id", null); } set { SetField("user_id", value); } }
		[ModelField(Required = true)]
		public string structure_id { get { return GetField<string>("structure_id", null); } set { SetField("structure_id", value); } }
		[ModelField(Required = true)]
		public string profil_id { get { return GetField<string>("profil_id", null); } set { SetField("profil_id", value); } }
		[ModelField(Required = true)]
		public string url { get { return GetField<string>("url", null); } set { SetField("url", value); } }
		[ModelField]
		public string parameters { get { return GetField<string>("parameters", null); } set { SetField("parameters", value); } }
		[ModelField]
		public DateTime timestamp { get { return GetField("timestamp", DateTime.Now); } set { SetField("timestamp", value); } }
	}

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
				var extracted = json.ExtractFields("application_id", "user_id", "structure_id", "profil_id", "url", "parameters");
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

				using (DB db = await DB.CreateAsync(dbUrl))
				{
					string filter = "";
					if (c.Request.QueryStringArray.ContainsKey("uais") &&
						c.Request.QueryStringArray["uais"].Count > 0)
						filter += " AND " + db.InFilter("uai", c.Request.QueryStringArray["uais"]);
					if (c.Request.QueryStringArray.ContainsKey("uids") &&
						c.Request.QueryStringArray["uids"].Count > 0)
						filter += " AND " + db.InFilter("uid", c.Request.QueryStringArray["uids"]);
					c.Response.Content = await db.SelectAsync<Log>(
						$"SELECT * FROM log WHERE timestamp >= ? AND timestamp <= ? {filter}",
						DateTime.Parse(c.Request.QueryString["from"]),
						DateTime.Parse(c.Request.QueryString["until"]));
				}
				c.Response.StatusCode = 200;
			};
		}

		public async Task<Log> GetLogAsync(int id)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await GetLogAsync(db, id);
		}

		public async Task<Log> GetLogAsync(DB db, int id)
		{
			return await db.SelectRowAsync<Log>(id);
		}
	}
}
