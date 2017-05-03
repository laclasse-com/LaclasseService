// Tickets.cs
// 
//  API for the authentication CAS tickets
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
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.Remoting.Messaging;

namespace Laclasse.Authentication
{
	public class Tickets
	{
		readonly string dbUrl;
		readonly double ticketTimeout;

		public Tickets(string dbUrl, double timeout)
		{
			this.dbUrl = dbUrl;
			ticketTimeout = timeout;
		}

		public async Task<string> CreateAsync(string session)
		{
			string ticketId;
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				bool duplicate = false;
				do
				{
					ticketId = "ST-"+StringExt.RandomString(10);
					try
					{
						var count = await db.InsertAsync("INSERT INTO ticket (id,session) VALUES (?,?)", ticketId, session);
						if (count != 1)
							throw new Exception("Ticket create fails");
					}
					catch (MySql.Data.MySqlClient.MySqlException e)
					{
						// if the ticketId is already taken, try another
						duplicate = e.Number == 1062;
					}
				}
				while (duplicate);
			}
			return ticketId;
		}

		async Task CleanAsync()
		{
			var res = CallContext.LogicalGetData("Laclasse.Authentication.Tickets.lastClean");
			DateTime lastClean = (res == null) ? DateTime.MinValue : (DateTime)res;

			DateTime now = DateTime.Now;
			TimeSpan delta = now - lastClean;
			if (delta.TotalSeconds > ticketTimeout)
			{
				CallContext.LogicalSetData("Laclasse.Authentication.Tickets.lastClean", now);
				// delete old tickets
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					await db.DeleteAsync("DELETE FROM ticket WHERE TIMESTAMPDIFF(SECOND, start, NOW()) >= ?", ticketTimeout);
				}
			}
		}

		public async Task<string> GetAsync(string ticketId)
		{
			await CleanAsync();
			string user = null;
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				var item = (await db.SelectAsync("SELECT * FROM ticket WHERE id=?", ticketId)).SingleOrDefault();
				if (item != null)
					user = (string)item["session"];
			}
			return user;
		}

		public async Task DeleteAsync(string ticketId)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				await db.DeleteAsync("DELETE FROM ticket WHERE id=?", ticketId);
			}
		}
	}
}
