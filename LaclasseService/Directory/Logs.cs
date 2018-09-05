﻿// Log.cs
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017-2018 Metropole de Lyon
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
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "log", PrimaryKey = nameof(id))]
	public class Log : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField(Required = true)]
		public string ip { get { return GetField<string>(nameof(ip), null); } set { SetField(nameof(ip), value); } }
		[ModelField(ForeignModel = typeof(Application))]
		public string application_id { get { return GetField<string>(nameof(application_id), null); } set { SetField(nameof(application_id), value); } }
		[ModelField(ForeignModel = typeof(User))]
		public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
		[ModelField(ForeignModel = typeof(Structure))]
		public string structure_id { get { return GetField<string>(nameof(structure_id), null); } set { SetField(nameof(structure_id), value); } }
		[ModelField]
		public string profil_id { get { return GetField<string>(nameof(profil_id), null); } set { SetField(nameof(profil_id), value); } }
		[ModelField(Required = true)]
		public string url { get { return GetField<string>(nameof(url), null); } set { SetField(nameof(url), value); } }
		[ModelField]
		public string parameters { get { return GetField<string>(nameof(parameters), null); } set { SetField(nameof(parameters), value); } }
		[ModelField]
		public DateTime timestamp { get { return GetField(nameof(timestamp), DateTime.Now); } set { SetField(nameof(timestamp), value); } }
		[ModelField(ForeignModel = typeof(Resource))]
		public int? resource_id { get { return GetField<int?>(nameof(resource_id), null); } set { SetField(nameof(resource_id), value); } }
		[ModelField(ForeignModel = typeof(Tile))]
        public int? tile_id { get { return GetField<int?>(nameof(tile_id), null); } set { SetField(nameof(tile_id), value); } }

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

		public override SqlFilter FilterAuthUser(AuthenticatedUser user)
        {
            if (user.IsSuperAdmin || user.IsApplication)
                return new SqlFilter();
                
            // Limit logs to the structures where the logged user has an admin profile
			var structuresIds = user.user.profiles.Where((arg) => arg.type == "ADM" || arg.type == "DIR").Select((arg) => arg.structure_id).Distinct();
			if (structuresIds.Count() == 0)
				return new SqlFilter() { Where = "FALSE" };
			else
				return new SqlFilter() { Where = $"{DB.InFilter("structure_id", structuresIds)}" };
        }
	}

	public class Logs : ModelService<Log>
	{
		public Logs(string dbUrl) : base(dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/stats"] = async (p, c) =>
			{
				var mode = "4WEEK";
				if (c.Request.QueryString.ContainsKey("mode"))
					mode = c.Request.QueryString["mode"];
				var start = DateTime.Parse(c.Request.QueryString["timestamp>"]);
				var end = DateTime.Parse(c.Request.QueryString["timestamp<"]);
                var authUser = await c.GetAuthenticatedUserAsync();    
                var filterAuth = (new Log()).FilterAuthUser(authUser);
				SearchResult<Log> result;
                using (DB db = await DB.CreateAsync(dbUrl))
                {
                    result = await Model.SearchAsync<Log>(db, c, filterAuth);
                    foreach (var item in result.Data)
                        await item.EnsureRightAsync(c, Right.Read, null);
                }
                c.Response.StatusCode = 200;

                // calcul stats
				var totalCount = 0;
				// sso types
				var ssoProfiles = new Dictionary<string,long>();
				var ssoTypes = new Dictionary<string,long>();
				var netTypes = new Dictionary<string, long>();
				var resources = new Dictionary<int, Dictionary<string,int>>();
				var structures = new Dictionary<string, Dictionary<string,int>>();
				var users = new Dictionary<string, Dictionary<string,int>>();

				var maxY = 0;
				var lastDataHour = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
				var startHour = lastDataHour;
				var currentHour = DateTime.MinValue;
                var count = 0; var first = true;
				var hours = new List<HourValue>();

				if (mode == "4WEEK")
				{
					for (var i = 0; i < 4; i++) {
						var weekNb = WeekOfYear(new DateTime(start.Ticks + (3600L * 24L * 7L * 10000000L * i) + (3600L * 24L * 3L * 10000000L)));
						var k = weekNb.Year.ToString("0000") + "-" + weekNb.Week.ToString("00");
						users[k] = new Dictionary<string, int>();
					}
				}
                else
				{
					for (var i = 0; i < ((mode == "6MONTH") ? 6 : 12); i++) {
						var month = start.AddMonths(i);
						var m = month.Year.ToString("0000") + "-" + month.Month.ToString("00");
						users[m] = new Dictionary<string, int>();
					}
				}
                

				foreach (var log in result.Data)
				{
					totalCount++;
					if (log.application_id == "SSO" && log.parameters != null)
					{
						var pt = log.parameters.Split('&');
						var ph = new Dictionary<string, string>();
						foreach (var pi in pt)
						{
							var at = pi.Split('=');                  
							ph[Uri.UnescapeDataString(at[0])] = at[1];
						}
						if (ph.ContainsKey("idp"))
						{
							if (!ssoTypes.ContainsKey(ph["idp"]))
								ssoTypes[ph["idp"]] = 1;
							else
								ssoTypes[ph["idp"]] += 1;
						}
						if (log.profil_id != null)
						{
							if (!ssoProfiles.ContainsKey(log.profil_id))
								ssoProfiles[log.profil_id] = 1;
							else
								ssoProfiles[log.profil_id] += 1;
						}
						if (log.ip != null)
						{
							var intern = log.ip.StartsWith("193.51.226.", StringComparison.InvariantCulture);
							var key = intern ? "Interne collège" : "Internet";
							if (!netTypes.ContainsKey(key))
								netTypes[key] = 1;
							else
								netTypes[key] += 1;
						}
					}
					if (log.resource_id != null) 
					{
						if (!resources.ContainsKey((int)log.resource_id))
							resources[(int)log.resource_id] = new Dictionary<string,int>();
						if (!resources[(int)log.resource_id].ContainsKey(log.user_id))
							resources[(int)log.resource_id][log.user_id] = 1;
						else
							resources[(int)log.resource_id][log.user_id] += 1;
					}
					if (log.structure_id != null) 
                    {
						if (!structures.ContainsKey(log.structure_id))
							structures[log.structure_id] = new Dictionary<string,int>();
						if (!structures[log.structure_id].ContainsKey(log.user_id))
							structures[log.structure_id][log.user_id] = 1;
                        else
							structures[log.structure_id][log.user_id] += 1;
                    }
					if (mode == "4WEEK")
					{
						var time = log.timestamp;
                        var weekNb = WeekOfYear(time);
						var k = weekNb.Year.ToString("0000") + "-" + weekNb.Week.ToString("00");
						if (users.ContainsKey(k)) {
							if (!users[k].ContainsKey(log.user_id))
								users[k][log.user_id] = 1;
                            else
								users[k][log.user_id] += 1;
                        }
					}
					else
					{
						var time = log.timestamp;
						var m = time.Year.ToString("0000") + "-" + time.Month.ToString("00");
						if (users.ContainsKey(m)) {
							if (!users[m].ContainsKey(log.user_id))
								users[m][log.user_id] = 1;
							else
								users[m][log.user_id] += 1;
						}
					}
                    // handle nb logs per hours
					var timestamp = log.timestamp;
                    var hour = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0);
                    if (first) {
                        first = false;
                        currentHour = hour;
                        count++;
                    }
                    else if (hour == currentHour)
                        count++;
                    else {
                        while (lastDataHour < currentHour) {
							hours.Add(new HourValue { Time = lastDataHour, Value = 0 });                     
							lastDataHour = lastDataHour.AddHours(1);
                        }
                        if (count > maxY)
                            maxY = count;
                        hours.Add(new HourValue { Time = currentHour, Value = count });
						lastDataHour = lastDataHour.AddHours(1);
                        currentHour = hour;
                        count = 1;
                    }
				}

				var ssoNames = new Dictionary<string,string> {
                    ["ENT"] = "Compte laclasse",
					["AAF"] = "Compte Académique",
					["CUT"] = "Compte Grand Lyon Connect",
					["EMAIL"] =  "Récup. par email",
					["SMS"] = "Récup. par SMS"
                };
                
				c.Response.Content = new JsonObject
				{
					["count"] = totalCount,
					["connections"] = new JsonArray(ssoTypes.Select((arg) => new JsonObject {
						["name"] = ssoNames.ContainsKey(arg.Key) ? ssoNames[arg.Key] : arg.Key,
						["value"] = arg.Value
					})),
					["networks"] = new JsonArray(netTypes.Select((arg) => new JsonObject {
						["name"] = arg.Key,
                        ["value"] = arg.Value
					})),
					["profiles"] = new JsonArray(ssoProfiles.Select((arg) => new JsonObject {
                        ["name"] = arg.Key,
                        ["value"] = arg.Value
                    })),
					["resources"] = new JsonArray(resources.Select((arg) => new JsonObject {
						["name"] = arg.Key,
						["value"] = arg.Value.Keys.Count()
					}).OrderByDescending((arg) => (int)arg["value"])),
					["structures"] = new JsonArray(structures.Select((arg) => new JsonObject {
                        ["name"] = arg.Key,
                        ["value"] = arg.Value.Keys.Count()
					}).OrderByDescending((arg) => (int)arg["value"])),
					["users"] = new JsonArray(users.Select((arg) => new JsonObject {
                        ["name"] = arg.Key,
                        ["value"] = arg.Value.Keys.Count()
                    })),
					["activity"] = new JsonObject {
						["maxY"] = maxY,
						["start"] = startHour,
						["data"] = new JsonArray(hours.Select((arg) => new JsonPrimitive(arg.Value)))
					}
				};
			};
		}

        public struct HourValue
		{
			public DateTime Time;
			public int Value;
		}

        public struct WeekOfYearResult
		{
			public int Week;
			public int Year;
		}

		public static WeekOfYearResult WeekOfYear(DateTime day)
		{
			var year = day.Year;
            var jan4 = new DateTime(year, 1, 4);
			var week1day = new DateTime(jan4.Ticks - ((((int)jan4.DayOfWeek - 1) % 7) * 24 * 60 * 60 * 10000000));
            var weekNb = ((day.Ticks - week1day.Ticks) / (24L * 60L * 60L * 10000000L * 7L)) + 1;
            if (weekNb < 1) {
                year--;
                jan4 = new DateTime(year, 1, 4);
				week1day = new DateTime(jan4.Ticks - ((((int)jan4.DayOfWeek - 1) % 7) * 24 * 60 * 60 * 10000000));
				weekNb = ((day.Ticks - week1day.Ticks) / (24L * 60L * 60L * 10000000L * 7L)) + 1;
            }
			return new WeekOfYearResult { Week = (int)weekNb, Year = year };
        }
	}
}
