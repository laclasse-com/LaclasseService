// Sessions.cs
// 
//  API for the authentication sessions
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
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.Remoting.Messaging;
using Erasme.Http;

namespace Laclasse.Authentication
{
	public class Sessions : HttpRouting
	{
		readonly string dbUrl;
		readonly double sessionTimeout;
		readonly string cookieName;

		public Sessions(string dbUrl, double timeout, string cookieName)
		{
			this.dbUrl = dbUrl;
			sessionTimeout = timeout;
			this.cookieName = cookieName;

			GetAsync["/current"] = async (p, c) =>
			{
				var user = await GetAuthenticatedUserAsync(c);
				if (user == null)
					c.Response.StatusCode = 404;
				else
				{
					c.Response.StatusCode = 200;
					c.Response.Content = user;
				}
			};
		}

		public async Task<string> CreateSessionAsync(string user)
		{
			string sessionId;
			using (DB db = await DB.CreateAsync(dbUrl))
			{

				var sb = new StringBuilder();
				foreach (var b in BitConverter.GetBytes(DateTime.Now.Ticks))
					sb.Append(b.ToString("X2"));

				bool duplicate;
				int tryCount = 0;
				do
				{
					duplicate = false;
					sessionId = sb + StringExt.RandomString(10);
					try
					{
						if (await db.InsertAsync("INSERT INTO session (id,user) VALUES (?,?)", sessionId, user) != 1)
							sessionId = null;
					}
					catch (MySql.Data.MySqlClient.MySqlException e)
					{
						sessionId = null;
						// if the ticketId is already taken, try another
						duplicate = (e.Number == 1062);
						tryCount++;
					}
				}
				while (duplicate && (tryCount < 10));
				if (sessionId == null)
					throw new Exception("Session create fails. Impossible generate a sessionId");
			}
			return sessionId;
		}

		async Task CleanAsync()
		{
			var res = CallContext.LogicalGetData("Laclasse.Authentication.Sessions.lastClean");
			DateTime lastClean = (res == null) ? DateTime.MinValue : (DateTime)res;

			DateTime now = DateTime.Now;
			TimeSpan delta = now - lastClean;
			if (delta.TotalSeconds > sessionTimeout)
			{
				CallContext.LogicalSetData("Laclasse.Authentication.Sessions.lastClean", now);
				// delete old sessions
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					await db.DeleteAsync("DELETE FROM session WHERE TIMESTAMPDIFF(SECOND, start, NOW()) >= ?", sessionTimeout);
				}
			}
		}

		public async Task<string> GetSessionAsync(string sessionId)
		{
			await CleanAsync();
			string user = null;
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				var item = (await db.SelectAsync("SELECT * FROM session WHERE id=?", sessionId)).SingleOrDefault();
				if (item != null)
					user = (string)item["user"];
			}
			return user;
		}

		public async Task DeleteSessionAsync(string sessionId)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				await db.DeleteAsync("DELETE FROM session WHERE id=?", sessionId);
			}
		}

		public async Task<string> GetAuthenticatedUserAsync(HttpContext context)
		{
			string user = null;
			if (context.Request.Cookies.ContainsKey(cookieName))
			{
				user = await GetSessionAsync(context.Request.Cookies[cookieName]);
			}
			return user;
		}
	}
}
