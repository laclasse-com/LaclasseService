// Tickets.cs
// 
//  API for the authentication CAS tickets
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
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Remoting.Messaging;

namespace Laclasse.Authentication
{
	[Model(Table = "ticket", PrimaryKey = nameof(id))]
	public class Ticket : Model
	{
		[ModelField]
		public string id { get { return GetField<string>(nameof(id), null); } set { SetField(nameof(id), value); } }
		[ModelField(Required = true)]
		public string session { get { return GetField<string>(nameof(session), null); } set { SetField(nameof(session), value); } }
		[ModelField]
		public DateTime start { get { return GetField(nameof(start), DateTime.Now); } set { SetField(nameof(start), value); } }
		[ModelField]
		public string code { get { return GetField<string>(nameof(code), null); } set { SetField(nameof(code), value); } }
	}

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
			var ticket = await CreateAsync(session, false);
			return ticket.id;
		}

		public async Task<Ticket> CreateRescueAsync(string session)
		{
			return await CreateAsync(session, true);
		}

		async Task<Ticket> CreateAsync(string session, bool withCode)
		{
			string ticketId;
			string code = null;
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				var sb = new StringBuilder();
				foreach (var b in BitConverter.GetBytes(DateTime.Now.Ticks))
					sb.Append(b.ToString("X2"));

				if (withCode)
					code = StringExt.RandomString(4, "0123456789");
				bool duplicate;
				int tryCount = 0;
				do
				{
					duplicate = false;
					ticketId = "ST-" + sb + StringExt.RandomString(13);
					try
					{
						if (await db.InsertAsync("INSERT INTO ticket (id,session,code) VALUES (?,?,?)", ticketId, session, code) != 1)
							ticketId = null;
					}
					catch (MySql.Data.MySqlClient.MySqlException e)
					{
						ticketId = null;
						// if the ticketId is already taken, try another
						duplicate = (e.Number == 1062);
						tryCount++;
					}
				}
				while (duplicate && (tryCount < 10));
				if (ticketId  == null)
					throw new Exception("Ticket create fails. Impossible generate a ticketId");
			}
			return new Ticket { id = ticketId, session = session, code = code, start = DateTime.Now };
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
					await db.DeleteAsync("DELETE FROM `ticket` WHERE TIMESTAMPDIFF(SECOND, start, NOW()) >= ?", ticketTimeout);
			}
		}

		async Task<Ticket> GetTicketAsync(string ticketId)
		{
			await CleanAsync();
			using (DB db = await DB.CreateAsync(dbUrl))
				return await db.SelectRowAsync<Ticket>(ticketId);
		}

		public async Task<string> GetAsync(string ticketId)
		{
			var ticket = await GetTicketAsync(ticketId);
			return ((ticket != null) && (ticket.code == null)) ? ticket.session : null;
		}

		public async Task<Ticket> GetRescueAsync(string ticketId)
		{
			var ticket = await GetTicketAsync(ticketId);
			return ((ticket != null) && (ticket.code != null)) ? ticket : null;
		}

		public async Task DeleteAsync(string ticketId)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				await db.DeleteAsync("DELETE FROM `ticket` WHERE `id`=?", ticketId);
		}
	}
}
