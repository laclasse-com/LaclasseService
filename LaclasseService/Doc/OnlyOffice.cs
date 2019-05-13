using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Doc
{
    public class OnlyOffice : Document
    {
        public OnlyOffice(Context context, Node node) : base(context, node)
        {
        }

        public override async Task<Stream> GetContentAsync()
        {
            var newItem = await WaitForceSaveContentAsync();
            if (newItem != null)
                return await newItem.GetItemContentAsync();
            return await base.GetContentAsync();
        }

        internal async Task<Stream> GetItemContentAsync()
        {
            return await base.GetContentAsync();
        }

        public async Task<OnlyOffice> WaitForceSaveContentAsync()
        {
            // if no editing session in progress
            if (await GetEditingKeyAsync() == null)
                return null;
            OnlyOffice newItem = null;

            Docs.OnlyOfficeCallbackData data = new Docs.OnlyOfficeCallbackData(5000);
            lock (context.docs.onlyOfficeCallbacksLock)
            {
                List<Docs.OnlyOfficeCallbackData> list = null;
                if (!context.docs.onlyOfficeCallbacks.ContainsKey(node.id))
                {
                    list = new List<Docs.OnlyOfficeCallbackData>();
                    context.docs.onlyOfficeCallbacks[node.id] = list;
                }
                list.Add(data);
            }
            try
            {
                var json = await ForceSaveAsync();
                // if forcesave in progress, wait for the document update
                if (json != null && json.ContainsKey("error") && json["error"] == 0)
                    newItem = await data.Task;
            }
            catch (Exception e)
            {
                Console.WriteLine($"GOT EXCEPTION: {e}");
            }
            finally
            {
                lock (context.docs.onlyOfficeCallbacksLock)
                {
                    if (context.docs.onlyOfficeCallbacks.ContainsKey(node.id))
                    {
                        var list = context.docs.onlyOfficeCallbacks[node.id];
                        list.Remove(data);
                        if (list.Count == 0)
                            context.docs.onlyOfficeCallbacks.Remove(node.id);
                    }
                }
            }
            return newItem;
        }

        // Return the current OnlyOffice document editing key or null
        // if not editing sessions are in progress
        public async Task<string> GetEditingKeyAsync()
        {
            var sessions = await context.db.SelectAsync<OnlyOfficeSession>("SELECT * FROM `onlyoffice_session` WHERE `node_id` = ?", node.id);
            return (sessions.Count > 0) ? sessions[0].key : null;
        }


        // Get OnlyOffice editing status for a given item
        // curl -X POST http://daniel.erasme.lan/onlyoffice/coauthoring/CommandService.ashx
        // -H "Content-Type: application/json" -d
        // '{"key": "3180REV3", "c": "info"}'
        public static async Task<JsonValue> GetInfoAsync(string publicUrl, string key)
        {
            JsonValue res = null;
            var uri = new Uri(new Uri(publicUrl), "/onlyoffice/coauthoring/CommandService.ashx");
            using (HttpClient client = await HttpClient.CreateAsync(uri))
            {
                HttpClientRequest request = new HttpClientRequest();
                request.Method = "POST";
                request.Path = uri.PathAndQuery;
                request.Headers["accept"] = "application/json";
                request.Content = new JsonObject
                {
                    ["key"] = key,
                    ["c"] = "info"
                };
                await client.SendRequestAsync(request);
                HttpClientResponse response = await client.GetResponseAsync();
                if (response.StatusCode == 200)
                    res = await response.ReadAsJsonAsync();
            }
            return res;
        }

        public async Task<JsonValue> GetInfoAsync()
        {
            return await GetInfoAsync(context.httpContext.SelfURL(), await GetEditingKeyAsync());
        }


        // Ask OnlyOffice to force save the the given item
        // curl -X POST http://daniel.erasme.lan/onlyoffice/coauthoring/CommandService.ashx
        // -H "Content-Type: application/json" -d
        // '{"key": "3180REV3", "c": "forcesave"}'
        public static async Task<JsonValue> ForceSaveAsync(string publicUrl, string key)
        {
            JsonValue res = null;
            var uri = new Uri(new Uri(publicUrl), "/onlyoffice/coauthoring/CommandService.ashx");
            using (HttpClient client = await HttpClient.CreateAsync(uri))
            {
                HttpClientRequest request = new HttpClientRequest();
                request.Method = "POST";
                request.Path = uri.PathAndQuery;
                request.Headers["accept"] = "application/json";
                request.Content = new JsonObject
                {
                    ["key"] = key,
                    ["c"] = "forcesave"
                };
                await client.SendRequestAsync(request);
                HttpClientResponse response = await client.GetResponseAsync();
                if (response.StatusCode == 200)
                {
                    res = await response.ReadAsJsonAsync();
                }
            }
            return res;
        }

        // Ask OnlyOffice to force save the the given item
        // curl -X POST http://daniel.erasme.lan/onlyoffice/coauthoring/CommandService.ashx
        // -H "Content-Type: application/json" -d
        // '{"key": "3180REV3", "c": "forcesave"}'
        public async Task<JsonValue> ForceSaveAsync()
        {
            return await ForceSaveAsync(context.httpContext.SelfURL(), await GetEditingKeyAsync());
        }

        //# Thumbnail avec OnlyOffice
        //        curl -X POST http://hostname/onlyoffice/ConvertService.ashx
        // -H "Content-Type: application/json" -d
        // '{"async": false, "outputtype":"jpg","thumbnail":{"aspect": 0, "width": 64, "height": 64 },"filetype":"docx","url": "http://hostname/api/docs/3180/content"}'
        public static async Task<string> ConvertAsync(HttpContext c, string fileUrl, OnlyOfficeFileType fileType, OnlyOfficeFileType destFileType)
        {
            string resFileUrl = null;
            var uri = new Uri(new Uri(c.SelfURL()), "/onlyoffice/ConvertService.ashx");
            using (HttpClient client = await HttpClient.CreateAsync(uri))
            {
                HttpClientRequest request = new HttpClientRequest();
                request.Method = "POST";
                request.Path = uri.PathAndQuery;
                request.Headers["accept"] = "application/json";
                request.Content = new JsonObject
                {
                    ["key"] = Guid.NewGuid().ToString(),
                    ["async"] = false,
                    ["codePage"] = 65001,
                    ["filetype"] = fileType.ToString(),
                    ["outputtype"] = destFileType.ToString(),
                    ["url"] = fileUrl
                };
                await client.SendRequestAsync(request);
                HttpClientResponse response = await client.GetResponseAsync();
                if (response.StatusCode == 200)
                {
                    var json = await response.ReadAsJsonAsync();
                    if (json.ContainsKey("fileUrl"))
                        resFileUrl = json["fileUrl"];
                }
            }
            return resFileUrl;
        }

        // Get OnlyOffice editing status for a given item
        // curl -X POST http://daniel.erasme.lan/onlyoffice/coauthoring/CommandService.ashx
        // -H "Content-Type: application/json" -d
        // '{"key": "3180REV3", "c": "info"}'
        public static async Task<JsonValue> DisconnectUserAsync(string publicUrl, string key, string userId)
        {
            JsonValue res = null;
            var uri = new Uri(new Uri(publicUrl), "/onlyoffice/coauthoring/CommandService.ashx");
            using (HttpClient client = await HttpClient.CreateAsync(uri))
            {
                HttpClientRequest request = new HttpClientRequest();
                request.Method = "POST";
                request.Path = uri.PathAndQuery;
                request.Headers["accept"] = "application/json";
                request.Content = new JsonObject
                {
                    ["key"] = key,
                    ["c"] = "drop",
                    ["users"] = new JsonArray(new JsonPrimitive[] { new JsonPrimitive(userId) })

                };
                await client.SendRequestAsync(request);
                HttpClientResponse response = await client.GetResponseAsync();
                if (response.StatusCode == 200)
                {
                    res = await response.ReadAsJsonAsync();
                }
            }
            return res;
        }

        public async Task<JsonValue> DisconnectUserAsync(string userId)
        {
            return await DisconnectUserAsync(context.httpContext.SelfURL(), await GetEditingKeyAsync(), userId);
        }

        public static void NodeToFileType(Node node, out OnlyOfficeDocumentType documentType, out OnlyOfficeFileType fileType)
        {
            MimeToFileType(node.mime, out documentType, out fileType);
        }

        public static void MimeToFileType(string mime, out OnlyOfficeDocumentType documentType, out OnlyOfficeFileType fileType)
        {
            documentType = OnlyOfficeDocumentType.text;
            fileType = OnlyOfficeFileType.docx;

            // text
            if (mime == "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.docx;
            }
            else if (mime == "application/msword")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.doc;
            }
            else if (mime == "application/epub+zip")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.epub;
            }
            else if (mime == "application/vnd.oasis.opendocument.text")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.odt;
            }
            else if (mime == "application/rtf")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.rtf;
            }
            else if (mime == "text/plain")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.txt;
            }
            else if (mime == "application/vnd.ms-xpsdocument")
            {
                documentType = OnlyOfficeDocumentType.text;
                fileType = OnlyOfficeFileType.xps;
            }

            // spreadsheet
            else if (mime == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            {
                documentType = OnlyOfficeDocumentType.spreadsheet;
                fileType = OnlyOfficeFileType.xlsx;
            }
            else if (mime == "application/vnd.ms-excel")
            {
                documentType = OnlyOfficeDocumentType.spreadsheet;
                fileType = OnlyOfficeFileType.xls;
            }
            else if (mime == "application/vnd.oasis.opendocument.spreadsheet")
            {
                documentType = OnlyOfficeDocumentType.spreadsheet;
                fileType = OnlyOfficeFileType.ods;
            }
            else if (mime == "text/csv")
            {
                documentType = OnlyOfficeDocumentType.spreadsheet;
                fileType = OnlyOfficeFileType.csv;
            }

            // presentation
            else if (mime == "application/vnd.openxmlformats-officedocument.presentationml.presentation")
            {
                documentType = OnlyOfficeDocumentType.presentation;
                fileType = OnlyOfficeFileType.pptx;
            }
            else if (mime == "application/vnd.ms-powerpoint")
            {
                documentType = OnlyOfficeDocumentType.presentation;
                fileType = OnlyOfficeFileType.ppt;
            }
            else if (mime == "application/vnd.oasis.opendocument.presentation")
            {
                documentType = OnlyOfficeDocumentType.presentation;
                fileType = OnlyOfficeFileType.odp;
            }
        }
    }

    public class OnlyOfficeSessions : Directory.ModelService<OnlyOfficeSession>, IDisposable
    {
        string dbUrl;
        Logger logger;
        string publicUrl;
        Thread sessionStatusThread;

        object sessionStatusLock = new object();
        bool sessionStatusStop = false;

        public OnlyOfficeSessions(Logger logger, string dbUrl, string publicUrl) : base(dbUrl)
        {
            this.dbUrl = dbUrl;
            this.logger = logger;
            this.publicUrl = publicUrl;

            sessionStatusThread = new Thread(ThreadStart);
            sessionStatusThread.Name = "OnlyOfficeSessionStatusThread";
            sessionStatusThread.Start();

            BeforeAsync = async (p, c) => await c.EnsureIsSuperAdminAsync();

            Get["/checkall"] = (p, c) =>
            {
                lock (sessionStatusLock)
                    Monitor.Pulse(sessionStatusLock);
                c.Response.StatusCode = 200;
            };

            GetAsync["/{id}/info"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                using (DB db = await DB.CreateAsync(dbUrl, true))
                {
                    var session = new OnlyOfficeSession { id = id };
                    if (await session.LoadAsync(db))
                    {
                        c.Response.StatusCode = 200;
                        c.Response.Content = await OnlyOffice.GetInfoAsync(publicUrl, session.key);
                    }
                    await db.CommitAsync();
                }
            };

            DeleteAsync["/{id}/users/{userId}"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                var userId = (string)p["userId"];
                using (DB db = await DB.CreateAsync(dbUrl, true))
                {
                    var session = new OnlyOfficeSession { id = id };
                    if (await session.LoadAsync(db))
                    {
                        await OnlyOffice.DisconnectUserAsync(publicUrl, session.key, userId);
                        c.Response.StatusCode = 200;
                    }
                    await db.CommitAsync();
                }
            };

            GetAsync["/{id}/forcesave"] = async (p, c) =>
            {
                var id = long.Parse((string)p["id"]);
                using (DB db = await DB.CreateAsync(dbUrl, true))
                {
                    var session = new OnlyOfficeSession { id = id };
                    if (await session.LoadAsync(db))
                    {
                        c.Response.StatusCode = 200;
                        c.Response.Content = await OnlyOffice.ForceSaveAsync(publicUrl, session.key);
                    }
                    await db.CommitAsync();
                }
            };
        }

        void ThreadStart()
        {
            logger.Log(LogLevel.Info, "OnlyOffice sessions Thread START");
            bool stop = false;
            lock (sessionStatusLock)
            {
                while (!stop)
                {
                    try
                    {
                        ModelList<OnlyOfficeSession> sessions;
                        using (var db = DB.Create(dbUrl, true))
                        {
                            var task = db.SelectAsync<OnlyOfficeSession>("SELECT * FROM `onlyoffice_session` WHERE TIMESTAMPDIFF(SECOND, `ctime`, NOW()) > 30");
                            task.Wait();
                            sessions = task.Result;
                        }

                        logger.Log(LogLevel.Info, $"OnlyOffice sessions count: {sessions.Count()}");

                        if (sessions.Any())
                        {
                            foreach (var session in sessions)
                            {
                                var task = OnlyOffice.GetInfoAsync(publicUrl, session.key);
                                task.Wait();
                                var json = task.Result;

                                if (json != null && json.ContainsKey("error") && json["error"] is JsonPrimitive && ((JsonPrimitive)json["error"]).JsonType == JsonType.Number)
                                {
                                    int error = json["error"];
                                    if (error == 1)
                                    {
                                        logger.Log(LogLevel.Error, $"OnlyOffice session {session.id} not found for key {session.key}. Delete the session");
                                        using (var db = DB.Create(dbUrl))
                                            session.Delete(db);
                                    }
                                    else if (error == 2)
                                        logger.Log(LogLevel.Error, $"OnlyOffice session {session.id} with key {session.key}. Callback url not valid");
                                    else if (error == 3)
                                        logger.Log(LogLevel.Error, $"OnlyOffice session {session.id} with key {session.key}. Internal server error.");
                                    else if (error == 5)
                                        logger.Log(LogLevel.Error, $"OnlyOffice session {session.id} with key {session.key}. Command not correct.");
                                    else if (error == 6)
                                        logger.Log(LogLevel.Error, $"OnlyOffice session {session.id} with key {session.key}. Invalid token.");
                                    else if (error != 0 && error != 4)
                                        logger.Log(LogLevel.Error, $"OnlyOffice session {session.id} with key {session.key}. Unknown error code {error}.");
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(LogLevel.Error, $"OnlyOffice Thread Exception: {e.ToString()}");
                    }
                    Monitor.Wait(sessionStatusLock, TimeSpan.FromHours(1));
                    stop = sessionStatusStop;
                }
            }
            logger.Log(LogLevel.Info, "OnlyOffice session Thread STOP");
        }

        public void Dispose()
        {
            lock (sessionStatusLock)
            {
                sessionStatusStop = true;
                Monitor.PulseAll(sessionStatusLock);
            }
        }
    }
}