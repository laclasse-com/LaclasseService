// BrowserLog.cs
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2018 Metropole de Lyon
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
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "browser_log", PrimaryKey = nameof(id))]
	public class BrowserLog : Model
    {
        [ModelField]
        public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
        [ModelField(Required = true)]
        public string ip { get { return GetField<string>(nameof(ip), null); } set { SetField(nameof(ip), value); } }
        [ModelField(Required = true, ForeignModel = typeof(User))]
        public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
		[ModelField]
		public string user_agent { get { return GetField<string>(nameof(user_agent), null); } set { SetField(nameof(user_agent), value); } }
		[ModelField]
		public int? screen_width { get { return GetField<int?>(nameof(screen_width), null); } set { SetField(nameof(screen_width), value); } }
		[ModelField]
		public int? screen_height { get { return GetField<int?>(nameof(screen_height), null); } set { SetField(nameof(screen_height), value); } }
		[ModelField]
		public int? inner_width { get { return GetField<int?>(nameof(inner_width), null); } set { SetField(nameof(inner_width), value); } }
		[ModelField]
		public int? inner_height { get { return GetField<int?>(nameof(inner_height), null); } set { SetField(nameof(inner_height), value); } }
		[ModelField]
        public DateTime timestamp { get { return GetField(nameof(timestamp), DateTime.Now); } set { SetField(nameof(timestamp), value); } }

        public override void FromJson(JsonObject json, string[] filterFields = null, HttpContext context = null)
        {
            base.FromJson(json, filterFields, context);
            // if create from an HTTP context, auto fill timestamp and IP address
            if (context != null)
            {
                timestamp = DateTime.Now;
                string contextIp = "unknown";
                if (context.Request.RemoteEndPoint is IPEndPoint)
                    contextIp = ((IPEndPoint)context.Request.RemoteEndPoint).Address.ToString();
                if (context.Request.Headers.ContainsKey("x-forwarded-for"))
                    contextIp = context.Request.Headers["x-forwarded-for"];
                ip = contextIp;
            }
        }
    }

	public class BrowserLogs : ModelService<BrowserLog>
    {
		public BrowserLogs(string dbUrl) : base(dbUrl)
        {
			// API only available to authenticated users
			BeforeAsync = async (p, c) =>
			{
				await c.EnsureIsAuthenticatedAsync();
				if (c.Request.Method != "POST")
					await c.EnsureIsSuperAdminAsync();
			};

			PostAsync["/"] = async (p, c) =>
            {
                await RunBeforeAsync(null, c);

                var json = await c.Request.ReadAsJsonAsync();

                var log = new BrowserLog();
                log.FromJson((JsonObject)json, null, c);

				var authUser = await c.GetAuthenticatedUserAsync();
				if (authUser.IsUser)
					log.user_id = authUser.user.id;
				if (!log.IsSet(nameof(BrowserLog.user_agent)) && c.Request.Headers.ContainsKey("user-agent"))
					log.user_agent = c.Request.Headers["user-agent"];
				log.timestamp = DateTime.Now;            
				string contextIp = "unknown";
                if (c.Request.RemoteEndPoint is IPEndPoint)
					contextIp = ((IPEndPoint)c.Request.RemoteEndPoint).Address.ToString();
				if (c.Request.Headers.ContainsKey("x-forwarded-for"))
					contextIp = c.Request.Headers["x-forwarded-for"];
                log.ip = contextIp;
                
				// save in the DB
                using (var db = await DB.CreateAsync(dbUrl))               
                    await log.SaveAsync(db, true);
                c.Response.StatusCode = 200;
                c.Response.Content = log;
            };

			GetAsync["/stats"] = async (p, c) =>
			{
				await Task<int>.FromResult(0);
				c.Response.StatusCode = 200;
				c.Response.Content = "TODO";
			};
        }
    }
}
