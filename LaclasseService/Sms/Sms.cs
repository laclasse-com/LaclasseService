// Sms.cs
// 
//  Handle SMS API. 
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;
using Laclasse.Directory;

namespace Laclasse.Sms
{
    public enum SmsStatusState
    {
        RUNNING,
        DONE,
        FAILED
    }

    [Model(Table = "sms_user", PrimaryKey = nameof(id))]
    public class SmsUser : Model
    {
        [ModelField]
        public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
        [ModelField]
        public string number { get { return GetField<string>(nameof(number), null); } set { SetField(nameof(number), value); } }
        [ModelField]
        public string user_firstname { get { return GetField<string>(nameof(user_firstname), null); } set { SetField(nameof(user_firstname), value); } }
        [ModelField]
        public string user_lastname { get { return GetField<string>(nameof(user_lastname), null); } set { SetField(nameof(user_lastname), value); } }
        [ModelField(ForeignModel = typeof(Sms))]
        public int sms_id { get { return GetField<int>(nameof(sms_id), 0); } set { SetField(nameof(sms_id), value); } }
        [ModelField(ForeignModel = typeof(User))]
        public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
        [ModelField]
        public string status_id { get { return GetField<string>(nameof(status_id), null); } set { SetField(nameof(status_id), value); } }
        [ModelField]
        public SmsStatusState? status_state { get { return GetField<SmsStatusState?>(nameof(status_state), null); } set { SetField(nameof(status_state), value); } }
        [ModelField]
        public string status_text { get { return GetField<string>(nameof(status_text), null); } set { SetField(nameof(status_text), value); } }
        [ModelField]
        public DateTime? status_mtime { get { return GetField<DateTime?>(nameof(status_mtime), null); } set { SetField(nameof(status_mtime), value); } }
    }

    [Model(Table = "sms", PrimaryKey = nameof(id))]
    public class Sms : Model
    {
        [ModelField]
        public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
        [ModelField]
        public DateTime ctime { get { return GetField(nameof(ctime), DateTime.Now); } set { SetField(nameof(ctime), value); } }
        [ModelField]
        public string content { get { return GetField<string>(nameof(content), null); } set { SetField(nameof(content), value); } }
        [ModelField(ForeignModel = typeof(User))]
        public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
        [ModelExpandField(Name = nameof(targets), ForeignModel = typeof(SmsUser))]
        public ModelList<SmsUser> targets { get { return GetField<ModelList<SmsUser>>(nameof(targets), null); } set { SetField(nameof(targets), value); } }

        public override void FromJson(JsonObject json, string[] filterFields = null, HttpContext context = null)
        {
            base.FromJson(json, filterFields, context);
            // if create from an HTTP context, auto fill the user_id
            if (context != null)
            {
                var authUser = context.GetAuthenticatedUser();
                if ((authUser != null) && (user_id == null || !authUser.IsSuperAdmin))
                    user_id = authUser.user.id;
            }
        }

        public override SqlFilter FilterAuthUser(AuthenticatedUser user)
        {
            if (user.IsSuperAdmin || user.IsApplication)
                return new SqlFilter();
            var structuresIds = user.user.profiles.Select((arg) => arg.structure_id).Distinct();
            var filter = $"INNER JOIN(SELECT '{DB.EscapeString(user.user.id)}' AS `allow_id` ";
            foreach (var structureId in structuresIds)
            {
                if (user.HasRightsOnStructure(structureId, true, true, true))
                    filter += $"UNION SELECT DISTINCT(`user_id`) as `allow_id` FROM `user_profile` WHERE `structure_id`='{DB.EscapeString(structureId)}' ";
            }
            filter += ") `allow` ON (`user_id` = `allow_id`)";
            return new SqlFilter() { Inner = filter };
        }
    }

    public class SmsService : ModelService<Sms>, IDisposable
    {
        Logger logger;
        string dbUrl;
        SmsSetup smsSetup;
        Thread smsStatusThread;

        object smsStatusLock = new object();
        bool smsStatusStop = false;

        public SmsService(Logger logger, string dbUrl, SmsSetup smsSetup) : base(dbUrl)
        {
            this.logger = logger;
            this.dbUrl = dbUrl;
            this.smsSetup = smsSetup;

            smsStatusThread = new Thread(ThreadStart);
            smsStatusThread.Name = "SmsStatusThread";
            smsStatusThread.Start();

            // API only available to admin users
            BeforeAsync = async (p, c) => await c.EnsureIsNotRestrictedUserAsync();

            PostAsync["/updateStatus"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                lock (smsStatusLock)
                    Monitor.PulseAll(smsStatusLock);
            };

            PostAsync["/"] = async (p, c) =>
            {
                await RunBeforeAsync(null, c);

                var json = await c.Request.ReadAsJsonAsync();

                var sms = new Sms();
                sms.FromJson((JsonObject)json, null, c);


                using (var db = await DB.CreateAsync(dbUrl, true))
                {
                    JsonArray phones = new JsonArray();

                    var targets = new List<SmsUser>();
                    // resolv targets
                    foreach (var target in sms.targets)
                    {
                        var user = new User { id = target.user_id };
                        await user.LoadAsync(db, true);
                        target.user_firstname = user.firstname;
                        target.user_lastname = user.lastname;
                        var phone = user.phones.Find((ph) => ph.type == "PORTABLE");
                        if (phone != null)
                        {
                            target.number = phone.number;
                            phones.Add(phone.number);
                        }
                        targets.Add(target);
                    }

                    // send the SMS
                    await SendSmsAsync(targets, sms.content);

                    // save in the DB
                    await sms.SaveAsync(db, true);

                    // commit
                    await db.CommitAsync();
                }
                c.Response.StatusCode = 200;
                c.Response.Content = sms;
                await c.SendResponseAsync();

                // wait 10s before trying to get the SMS status
                await Task.Delay(TimeSpan.FromSeconds(10));

                // wake up the Thread that handle the SMS status
                lock (smsStatusLock)
                    Monitor.PulseAll(smsStatusLock);
            };
        }

        void ThreadStart()
        {
            logger.Log(LogLevel.Info, "SMS Thread START");
            bool stop = false;
            lock (smsStatusLock)
            {
                while (!stop)
                {
                    bool hasRunning = false;
                    bool hasNull = false;
                    try
                    {
                        ModelList<SmsUser> smsUsers;
                        using (var db = DB.Create(dbUrl, true))
                        {
                            var task = db.SelectAsync<SmsUser>("SELECT * FROM `sms_user` WHERE `status_id` IS NOT NULL AND (`status_state` IS NULL OR `status_state`='RUNNING') AND `sms_id` IN (SELECT `id` FROM `sms` WHERE TIMESTAMPDIFF(SECOND, `ctime`, NOW()) < 3600*24)");
                            task.Wait();
                            smsUsers = task.Result;
                        }

                        logger.Log(LogLevel.Info, $"SMS Get status needed: {smsUsers.Count()}");

                        if (smsUsers.Count() > 0)
                        {
                            var targetsTask = GetSmsResultAsync(smsUsers);
                            targetsTask.Wait();
                            ModelList<SmsUser> smsUsersResult;
                            (smsUsersResult, hasNull) = targetsTask.Result;

                            var diff = Model.Diff(smsUsers, smsUsersResult, (a, b) => a.status_id == b.status_id);
                            if (diff.change != null && diff.change.Count > 0)
                            {
                                using (var db = DB.Create(dbUrl, false))
                                {
                                    diff.change.ForEach((c) =>
                                    {
                                        var updateTask = c.UpdateAsync(db);
                                        updateTask.Wait();
                                    });
                                }
                                hasRunning = diff.change.Any(c => c.status_state == SmsStatusState.RUNNING);
                            }
                            logger.Log(LogLevel.Info, $"SMS Get status res hasNull: {hasNull}, #res: {smsUsersResult.Count()}");
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(LogLevel.Error, $"SMS Thread Exception: {e.ToString()}");
                    }
                    // if some delivery are in RUNNING state, re-check in 15 min. Else wait 1 hour
                    // or to be waked up
                    Monitor.Wait(smsStatusLock, hasNull ? TimeSpan.FromSeconds(30) :
                        (hasRunning ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(1)));
                    stop = smsStatusStop;
                }
            }
            logger.Log(LogLevel.Info, "SMS Thread STOP");
        }

        async Task<(ModelList<SmsUser>, bool)> GetSmsResultAsync(ModelList<SmsUser> targets)
        {
            var hasNull = false;
            var result = new ModelList<SmsUser>();
            if (targets.Count <= 50)
            {
                var res = await GetSmsResultMax50Async(targets);
                result.AddRange(res.Item1);
                if (res.Item2)
                    hasNull = true;
            }
            else
            {
                var pos = 0;
                while (pos < targets.Count)
                {
                    var limitedTargets = new ModelList<SmsUser>();
                    for (var i = 0; i < 50 && (pos < targets.Count); i++, pos++)
                        limitedTargets.Add(targets[pos]);
                    var res = await GetSmsResultMax50Async(limitedTargets);
                    result.AddRange(res.Item1);
                    if (res.Item2)
                        hasNull = true;
                }
            }
            return (result, hasNull);
        }

        async Task<(ModelList<SmsUser>, bool)> GetSmsResultMax50Async(List<SmsUser> targets)
        {
            var hasNull = false;
            var result = new ModelList<SmsUser>();
            var statusIds = new JsonArray();
            targets.ForEach((t) => statusIds.Add(t.status_id));
            // get the SMS delivery status
            var uri = new Uri(smsSetup.url);
            using (var client = await HttpClient.CreateAsync(uri))
            {
                var requestUri = new Uri(uri, "/api/smsStatus");
                var clientRequest = new HttpClientRequest
                {
                    Method = "POST",
                    Path = requestUri.AbsolutePath,
                    Headers =
                    {
                        ["authorization"] = "Bearer " + smsSetup.token,
                        ["content-type"] = "application/json"
                    },
                    Content = new JsonObject { ["cra"] = statusIds }
                };
                await client.SendRequestAsync(clientRequest);
                var response = await client.GetResponseAsync();
                if (response.StatusCode != 200)
                    logger.Log(LogLevel.Error, $"SMS smsState fails. HTTP STATUS {response.StatusCode}");
                else
                {
                    var json = await response.ReadAsJsonAsync();
                    if (!(json is JsonArray))
                        logger.Log(LogLevel.Error, $"SMS smsState fails. Result not JsonArray");
                    else
                    {
                        foreach (var item in json as JsonArray)
                        {
                            if (item == null)
                                hasNull = true;
                            if (item is JsonObject && ((JsonObject)item).ContainsKey("ID") &&
                                ((JsonObject)item).ContainsKey("STATUS") &&
                                ((JsonObject)item).ContainsKey("CALLRESULT") &&
                                ((JsonObject)item).ContainsKey("LASTCHANGE"))
                            {
                                var itemObj = item as JsonObject;
                                var smsUser = new SmsUser();
                                smsUser.status_id = itemObj["ID"];
                                smsUser.status_text = itemObj["CALLRESULT"];
                                SmsStatusState state;
                                if (Enum.TryParse(itemObj["STATUS"], out state))
                                    smsUser.status_state = state;
                                DateTime lastChange;
                                if (DateTime.TryParseExact(itemObj["LASTCHANGE"], "dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out lastChange))
                                    smsUser.status_mtime = lastChange;
                                result.Add(smsUser);
                            }
                        }
                    }
                }
            }
            return (result, hasNull);
        }

        async Task SendSmsAsync(List<SmsUser> targets, string message)
        {
            if (targets.Count <= 200)
                await SendSmsMax200Async(targets, message);
            else
            {
                var pos = 0;
                while (pos < targets.Count)
                {
                    var limitedPhones = new List<SmsUser>();
                    for (var i = 0; i < 200 && (pos < targets.Count); i++, pos++)
                        limitedPhones.Add(targets[pos]);
                    await SendSmsMax200Async(limitedPhones, message);
                }
            }
        }

        async Task SendSmsMax200Async(List<SmsUser> targets, string message)
        {
            var phones = new JsonArray();
            targets.ForEach((t) => phones.Add(t.number));
            // send the SMS
            var uri = new Uri(smsSetup.url);
            using (var client = await HttpClient.CreateAsync(uri))
            {
                var requestUri = new Uri(uri, "/api/send");
                var clientRequest = new HttpClientRequest();
                clientRequest.Method = "POST";
                clientRequest.Path = requestUri.AbsolutePath;
                clientRequest.Headers["authorization"] = "Bearer " + smsSetup.token;
                clientRequest.Headers["content-type"] = "application/json";
                var jsonData = new JsonObject
                {
                    ["content"] = message,
                    ["receiver"] = phones
                };
                clientRequest.Content = jsonData.ToString();
                await client.SendRequestAsync(clientRequest);
                var response = await client.GetResponseAsync();
                if (response.StatusCode != 200)
                {
                    logger.Log(LogLevel.Error, $"Send SMS service fails. HTTP fails (status: {response.StatusCode}, uri: {requestUri})");
                    throw new WebException(500, "Send SMS service fails. HTTP fails");
                }

                var json = await response.ReadAsJsonAsync();
                if (!(json is JsonArray) || (json.Count == 0) ||
                    !(json[0] is JsonObject) || !json[0].ContainsKey("status") ||
                    (json[0]["status"] != 200) ||
                    !json[0].ContainsKey("content") || !(json[0]["content"] is JsonObject) ||
                    !json[0]["content"].ContainsKey("success") ||
                    !json[0]["content"]["success"] ||
                    !json[0]["content"].ContainsKey("response") ||
                    !(json[0]["content"]["response"] is JsonArray))
                {
                    logger.Log(LogLevel.Error, $"Send SMS service fails. Invalid response (uri: {requestUri})");
                    throw new WebException(500, "Send SMS service fails. Invalid response");
                }
                if (json[0]["content"]["response"].Count != phones.Count)
                {
                    logger.Log(LogLevel.Error, $"Send SMS service fails. Only {json[0]["content"]["response"].Count} responses ids for {phones.Count} sent (uri: {requestUri})");
                    throw new WebException(500, $"Send SMS service fails. Only {json[0]["content"]["response"].Count} responses ids for {phones.Count} sent");
                }
                var i = 0;
                foreach (JsonValue responseId in json[0]["content"]["response"] as JsonArray)
                {
                    if (!(responseId is JsonPrimitive) || ((responseId as JsonPrimitive).JsonType != JsonType.String))
                    {
                        logger.Log(LogLevel.Error, $"Send SMS service fails. Invalid reponseId type (uri: {requestUri})");
                        throw new WebException(500, $"Send SMS service fails. Invalid reponseId type");
                    }
                    targets[i].status_id = responseId.Value as string;
                    i++;
                }
            }
        }

        public void Dispose()
        {
            lock (smsStatusLock)
            {
                smsStatusStop = true;
                Monitor.PulseAll(smsStatusLock);
            }
        }
    }
}
