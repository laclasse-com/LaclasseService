﻿// Sessions.cs
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
using System.Net;
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
        [ModelField]
        public string ip { get { return GetField<string>(nameof(ip), null); } set { SetField(nameof(ip), value); } }
        [ModelField]
        public string user_agent { get { return GetField<string>(nameof(user_agent), null); } set { SetField(nameof(user_agent), value); } }
        [ModelField]
        public bool tech { get { return GetField<bool>(nameof(tech), false); } set { SetField(nameof(tech), value); } }

        public TimeSpan duration;

        public override void FromJson(JsonObject json, string[] filterFields = null, HttpContext context = null)
        {
            base.FromJson(json, filterFields, context);
            // if create from an HTTP context, auto fill timestamp and IP address
            if (context != null)
            {
                string contextIp = "unknown";
                if (context.Request.RemoteEndPoint is IPEndPoint)
                    contextIp = ((IPEndPoint)context.Request.RemoteEndPoint).Address.ToString();
                if (context.Request.Headers.ContainsKey("x-forwarded-for"))
                    contextIp = context.Request.Headers["x-forwarded-for"];
                ip = contextIp;
                if (context.Request.Headers.ContainsKey("user-agent"))
                    user_agent = context.Request.Headers["user-agent"];
            }
        }

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
                        ["start"] = session.start,
                        ["duration"] = session.duration.TotalSeconds,
                        ["user"] = session.user,
                        ["idp"] = session.idp.ToString()
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
                        ["idp"] = session.idp.ToString()
                    };
                }
            };
        }

        public async Task<Session> CreateSessionAsync(string user, Idp idp, bool longSession = false, string ip = null, string userAgent = null, bool isTech = false)
        {
            string sessionId;
            Session session = null;
            using (DB db = await DB.CreateAsync(dbUrl))
            {
                var sb = new StringBuilder();
                foreach (var b in BitConverter.GetBytes(DateTime.Now.Ticks))
                    sb.Append(b.ToString("X2"));

                int tryCount = 0;
                do
                {
                    sessionId = sb + StringExt.RandomSecureString(10);
                    try
                    {
                        session = new Session
                        {
                            id = sessionId,
                            user = user,
                            idp = idp,
                            ip = ip,
                            user_agent = userAgent,
                            long_session = longSession,
                            tech = isTech,
                            duration = longSession ? TimeSpan.FromSeconds(sessionLongTimeout) : TimeSpan.FromSeconds(sessionTimeout)
                        };
                        if (!await session.InsertAsync(db))
                            sessionId = null;
                    }
                    catch (MySql.Data.MySqlClient.MySqlException e)
                    {
                        // if it is not a duplicate ticketId re throw the Exception
                        if (e.Number != 1062)
                            throw;
                        sessionId = null;
                    }
                    tryCount++;
                }
                while (sessionId == null && tryCount < 10);
                if (sessionId == null)
                    throw new Exception("Session create fails. Impossible generate a sessionId");
                // if its not a long session, only 1 short session is allowed at a time
                // invalidate previous sessions
                if (!isTech && !longSession)
                {
                    try
                    {
                        await db.DeleteAsync($"DELETE FROM `session` WHERE `{nameof(session.long_session)}` = FALSE AND `{nameof(session.tech)}` = FALSE AND `{nameof(session.id)}` != ? AND {nameof(session.user)} = ?", sessionId, user);
                    }
                    catch (MySql.Data.MySqlClient.MySqlException e)
                    {
                        // if a dead lock is detected while deleting, accept
                        // to delete later. Else re throw the exception
                        if (e.Number != 1213)
                            throw;
                    }
                }
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
                    try
                    {
                        await db.DeleteAsync("DELETE FROM `session` WHERE (`long_session` = FALSE AND TIMESTAMPDIFF(SECOND, start, NOW()) >= ?) OR (`long_session` = TRUE AND TIMESTAMPDIFF(SECOND, start, NOW()) >= ?)", sessionTimeout, sessionLongTimeout);
                    }
                    catch (MySql.Data.MySqlClient.MySqlException e)
                    {
                        // if a dead lock is detected while deleting, accept
                        // to delete later. Else re throw the exception
                        if (e.Number != 1213)
                            throw;
                    }
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
            // get the session ID
            string sessionId = (context.Request.Cookies.ContainsKey(cookieName)) ?
                context.Request.Cookies[cookieName] : null;
            // get the corresponding session
            Session session = null;
            if (sessionId != null)
                session = await GetSessionAsync(sessionId);
            // check security on the session
            if (session != null)
            {
                // if its not a long session
                if (!session.long_session)
                {
                    string contextIp = null;
                    if (context.Request.RemoteEndPoint is IPEndPoint)
                        contextIp = ((IPEndPoint)context.Request.RemoteEndPoint).Address.ToString();
                    if (context.Request.Headers.ContainsKey("x-forwarded-for"))
                        contextIp = context.Request.Headers["x-forwarded-for"];
                    string contextUserAgent = null;
                    if (context.Request.Headers.ContainsKey("user-agent"))
                        contextUserAgent = context.Request.Headers["user-agent"];
                    // if the IP or the User Agent has changed, delete the current
                    // session to ask for a new authentication
                    if (session.ip != contextIp || session.user_agent != contextUserAgent)
                    {
                        await DeleteSessionAsync(sessionId);
                        session = null;
                    }
                }
            }
            return session;
        }

        public async Task<string> GetAuthenticatedUserAsync(HttpContext context)
        {
            var session = await GetCurrentSessionAsync(context);
            return session == null ? null : session.user;
        }
    }
}
