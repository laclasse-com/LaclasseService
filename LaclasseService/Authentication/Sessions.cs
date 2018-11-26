// Sessions.cs
// 
//  API for the authentication sessions
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
using Erasme.Http;
using Erasme.Json;
using Laclasse.Directory;

namespace Laclasse.Authentication
{
    public enum Idp
    {
        ENT,
        AAF,
        EMAIL,
        SMS,
        CUT,
        GRANDLYON
    }

    [Model(Table = "session", PrimaryKey = nameof(id))]
    public class Session : Model
    {
        [ModelField]
        public string id { get { return GetField<string>(nameof(id), null); } set { SetField(nameof(id), value); } }
        [ModelField]
        public string user { get { return GetField<string>(nameof(user), null); } set { SetField(nameof(user), value); } }
        [ModelField]
        public DateTime start { get { return GetField(nameof(start), DateTime.Now); } set { SetField(nameof(start), value); } }
        [ModelField]
        public Idp idp { get { return GetField(nameof(idp), Idp.ENT); } set { SetField(nameof(idp), value); } }
        [ModelField]
        public bool long_session { get { return GetField(nameof(long_session), false); } set { SetField(nameof(long_session), value); } }

        public TimeSpan duration;

        public async override Task<bool> InsertAsync(DB db)
        {
            // update the user atime field
            var userDiff = new User { id = user, atime = DateTime.Now, last_idp = idp };
            await userDiff.UpdateAsync(db);

            return await base.InsertAsync(db);
        }
    }

    public class Sessions : HttpRouting
    {
        readonly string dbUrl;
        readonly double sessionTimeout;
        readonly double sessionLongTimeout;
        readonly string cookieName;

        public Sessions(string dbUrl, double timeout, double longTimeout, string cookieName)
        {
            this.dbUrl = dbUrl;
            sessionTimeout = timeout;
            sessionLongTimeout = longTimeout;
            this.cookieName = cookieName;

            GetAsync["/current"] = async (p, c) =>
            {
                var session = await GetCurrentSessionAsync(c);
                if (session == null)
                    c.Response.StatusCode = 404;
                else
                {
                    c.Response.StatusCode = 200;
                    c.Response.Content = new JsonObject
                    {
                        ["id"] = session.id,
                        ["start"] = session.start,
                        ["duration"] = session.duration.TotalSeconds,
                        ["user"] = session.user,
                    };
                }
            };

            GetAsync["/{id}"] = async (p, c) =>
            {
                var session = await GetSessionAsync((string)p["id"]);
                if (session == null)
                    c.Response.StatusCode = 404;
                else
                {
                    c.Response.StatusCode = 200;
                    c.Response.Content = new JsonObject
                    {
                        ["id"] = session.id,
                        ["start"] = session.start,
                        ["duration"] = session.duration.TotalSeconds,
                        ["user"] = session.user,
                    };
                }
            };
        }

        public async Task<Session> CreateSessionAsync(string user, Idp idp, bool longSession = false)
        {
            string sessionId;
            Session session = null;
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
                        session = new Session
                        {
                            id = sessionId,
                            user = user,
                            idp = idp,
                            long_session = longSession,
                            duration = longSession ? TimeSpan.FromSeconds(sessionLongTimeout) : TimeSpan.FromSeconds(sessionTimeout)
                        };
                        if (!await session.InsertAsync(db))
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
            return session;
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
                    await db.DeleteAsync("DELETE FROM `session` WHERE (`long_session` = FALSE AND TIMESTAMPDIFF(SECOND, start, NOW()) >= ?) OR (`long_session` = TRUE AND TIMESTAMPDIFF(SECOND, start, NOW()) >= ?)", sessionTimeout, sessionLongTimeout);
                }
            }
        }

        public async Task<Session> GetSessionAsync(string sessionId)
        {
            await CleanAsync();
            Session session = null;
            using (DB db = await DB.CreateAsync(dbUrl))
            {
                session = await db.SelectRowAsync<Session>(sessionId);
                if (session != null)
                    session.duration = session.long_session ? TimeSpan.FromSeconds(sessionLongTimeout) : TimeSpan.FromSeconds(sessionTimeout);
            }
            return session;
        }

        public async Task DeleteSessionAsync(string sessionId)
        {
            using (DB db = await DB.CreateAsync(dbUrl))
                await (new Session { id = sessionId }).DeleteAsync(db);
        }

        public async Task<Session> GetCurrentSessionAsync(HttpContext context)
        {
            string session = (context.Request.Cookies.ContainsKey(cookieName)) ?
                context.Request.Cookies[cookieName] : (context.Request.QueryString.ContainsKey("session") ? context.Request.QueryString["session"] : null);
            return session != null ? await GetSessionAsync(session) : null;
        }

        public async Task<string> GetAuthenticatedUserAsync(HttpContext context)
        {
            var session = await GetCurrentSessionAsync(context);
            return session == null ? null : session.user;
        }
    }
}
