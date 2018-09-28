// Log.cs
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
using System.Diagnostics;
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
                }
                c.Response.StatusCode = 200;
            
                // calcul stats
				var totalCount = 0;
				var ssoProfilesLogs = new Dictionary<string,long>();
				var ssoProfilesUsers = new Dictionary<string, HashSet<string>>();
				var ssoTypesLogs = new Dictionary<string,long>();
				var ssoTypesUsers = new Dictionary<string, HashSet<string>>();
				var netTypesLogs = new Dictionary<string, long>();
				var netTypesUsers = new Dictionary<string, HashSet<string>>();
				var resourcesLogs = new Dictionary<int,long>();
				var resourcesUsers = new Dictionary<int, HashSet<string>>();
				var structuresLogs = new Dictionary<string, long>();
				var structuresUsers = new Dictionary<string, HashSet<string>>();
				var usersLogs = new Dictionary<string, long>();
				var usersUsers = new Dictionary<string, HashSet<string>>();
                
				var startHour = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
				int[] hours = new int[(int)Math.Floor((end-startHour).TotalHours) + 1];

				if (mode == "4WEEK")
				{
					for (var i = 0; i < 4; i++) {
						var weekNb = WeekOfYear(new DateTime(start.Ticks + (3600L * 24L * 7L * 10000000L * i) + (3600L * 24L * 3L * 10000000L)));
						var k = weekNb.Year.ToString("0000") + "-" + weekNb.Week.ToString("00");
						usersUsers[k] = new HashSet<string>();
						usersLogs[k] = 0;
					}
				}
                else
				{
					for (var i = 0; i < ( (mode == "1MONTH") ? 1 : ((mode == "6MONTH") ? 6 : 12)); i++) {
						var month = start.AddMonths(i);
						var m = month.Year.ToString("0000") + "-" + month.Month.ToString("00");
						usersUsers[m] = new HashSet<string>();
						usersLogs[m] = 0;
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
							if (!ssoTypesLogs.ContainsKey(ph["idp"]))
								ssoTypesLogs[ph["idp"]] = 1;
							else
								ssoTypesLogs[ph["idp"]] += 1;

							if (!ssoTypesUsers.ContainsKey(ph["idp"]))
								ssoTypesUsers[ph["idp"]] = new HashSet<string>();
							ssoTypesUsers[ph["idp"]].Add(log.user_id);
						}
						if (log.profil_id != null)
						{
							if (!ssoProfilesLogs.ContainsKey(log.profil_id))
								ssoProfilesLogs[log.profil_id] = 1;
							else
								ssoProfilesLogs[log.profil_id] += 1;

							if (!ssoProfilesUsers.ContainsKey(log.profil_id))
								ssoProfilesUsers[log.profil_id] = new HashSet<string>();
							ssoProfilesUsers[log.profil_id].Add(log.user_id);
						}
						if (log.ip != null)
						{
							var intern = log.ip.StartsWith("193.51.226.", StringComparison.InvariantCulture);
							var key = intern ? "Interne collège" : "Internet";
							if (!netTypesLogs.ContainsKey(key))
								netTypesLogs[key] = 1;
							else
								netTypesLogs[key] += 1;
							if (!netTypesUsers.ContainsKey(key))
								netTypesUsers[key] = new HashSet<string>();
							netTypesUsers[key].Add(log.user_id);                     
						}
					}
					if (log.resource_id != null) 
					{
						if (!resourcesLogs.ContainsKey((int)log.resource_id))
                            resourcesLogs[(int)log.resource_id] = 1;
						else
							resourcesLogs[(int)log.resource_id] += 1;
						if (!resourcesUsers.ContainsKey((int)log.resource_id))
							resourcesUsers[(int)log.resource_id] = new HashSet<string>();
						resourcesUsers[(int)log.resource_id].Add(log.user_id);
					}
					if (log.structure_id != null) 
                    {
						if (!structuresLogs.ContainsKey(log.structure_id))
							structuresLogs[log.structure_id] = 1;
                        else
							structuresLogs[log.structure_id] += 1;
						if (!structuresUsers.ContainsKey(log.structure_id))
							structuresUsers[log.structure_id] = new HashSet<string>();
						structuresUsers[log.structure_id].Add(log.user_id);
                    }
					if (mode == "4WEEK")
					{
						var time = log.timestamp;
                        var weekNb = WeekOfYear(time);
						var k = weekNb.Year.ToString("0000") + "-" + weekNb.Week.ToString("00");
						if (usersUsers.ContainsKey(k))
							usersUsers[k].Add(log.user_id);
						if (usersLogs.ContainsKey(k))
                            usersLogs[k] += 1;
					}
					else
					{
						var time = log.timestamp;
						var m = time.Year.ToString("0000") + "-" + time.Month.ToString("00");
						if (usersUsers.ContainsKey(m))
							usersUsers[m].Add(log.user_id);
						if (usersLogs.ContainsKey(m))
                            usersLogs[m] += 1;
					}
                    // handle nb logs per hours
					hours[Math.Max(0, (int)Math.Floor((log.timestamp - start).TotalHours))]++;
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
					["connections"] = new JsonObject {
						["logs"] = new JsonArray(ssoTypesLogs.Select((arg) => new JsonObject {
							["name"] = ssoNames.ContainsKey(arg.Key) ? ssoNames[arg.Key] : arg.Key,
							["value"] = arg.Value
						})),
						["users"] = new JsonArray(ssoTypesUsers.Select((arg) => new JsonObject {
                            ["name"] = arg.Key,
                            ["value"] = arg.Value.Count
                        }).OrderByDescending((arg) => (int)arg["value"]))
					},
					["networks"] = new JsonObject {
						["logs"] = new JsonArray(netTypesLogs.Select((arg) => new JsonObject {
							["name"] = arg.Key,
							["value"] = arg.Value
						}).OrderByDescending((arg) => (int)arg["value"])),
						["users"] = new JsonArray(netTypesUsers.Select((arg) => new JsonObject {
                            ["name"] = arg.Key,
                            ["value"] = arg.Value.Count
                        }).OrderByDescending((arg) => (int)arg["value"]))
					},
					["profiles"] = new JsonObject {
                        ["logs"] = 	new JsonArray(ssoProfilesLogs.Select((arg) => new JsonObject {
							["name"] = arg.Key,
							["value"] = arg.Value
						}).OrderByDescending((arg) => (int)arg["value"])),
						["users"] = new JsonArray(ssoProfilesUsers.Select((arg) => new JsonObject {
                            ["name"] = arg.Key,
                            ["value"] = arg.Value.Count
                        }).OrderByDescending((arg) => (int)arg["value"]))
					},
					["resources"] = new JsonObject {
						["logs"] = new JsonArray(resourcesLogs.Select((arg) => new JsonObject {
                            ["name"] = arg.Key,
                            ["value"] = arg.Value
                        }).OrderByDescending((arg) => (int)arg["value"])),
                        ["users"] = new JsonArray(resourcesUsers.Select((arg) => new JsonObject {
							["name"] = arg.Key,
							["value"] = arg.Value.Count
						}).OrderByDescending((arg) => (int)arg["value"]))
					},
					["structures"] = new JsonObject {
						["logs"] = new JsonArray(structuresLogs.Select((arg) => new JsonObject {
                            ["name"] = arg.Key,
                            ["value"] = arg.Value
                        }).OrderByDescending((arg) => (int)arg["value"])),
						["users"] = new JsonArray(structuresUsers.Select((arg) => new JsonObject {
							["name"] = arg.Key,
							["value"] = arg.Value.Count
						}).OrderByDescending((arg) => (int)arg["value"]))
					},
					["users"] = new JsonObject {
						["logs"] = new JsonArray(usersLogs.Select((arg) => new JsonObject {
                            ["name"] = arg.Key,
                            ["value"] = arg.Value
                        }).OrderByDescending((arg) => (int)arg["value"])),
						["users"] = new JsonArray(usersUsers.Select((arg) => new JsonObject {
							["name"] = arg.Key,
							["value"] = arg.Value.Count
						}))
					},
					["activity"] = new JsonObject {
						["maxY"] = hours.Max(),
						["start"] = startHour,
						["data"] = new JsonArray(hours.Select((arg) => new JsonPrimitive(arg)))
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
