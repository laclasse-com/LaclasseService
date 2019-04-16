// MainClass.cs
// 
//  HTTP server for the directory service of Laclasse. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
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
using System.IO;
using System.Text;
using System.Collections.Generic;
using Mono.Unix;
using Mono.Unix.Native;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Directory;
using Laclasse.Authentication;
using Laclasse.Doc;

using Laclasse.Aaf;

namespace Laclasse
{
    class MainClass
    {
        public static string ReadWithoutComment(Stream stream)
        {
            var sb = new StringBuilder();
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (!(line.TrimStart(' ', '\t')).StartsWith("//", StringComparison.InvariantCulture))
                        sb.Append(line);
                }
            }
            return sb.ToString();
        }

        public static JsonValue ReadCommentedJson(Stream stream)
        {
            var sb = new StringBuilder();
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (!(line.TrimStart(' ', '\t')).StartsWith("//", StringComparison.InvariantCulture))
                        sb.Append(line);
                }
            }
            return JsonValue.Parse(sb.ToString());
        }

        public static void Main(string[] args)
        {
            // load the default setup from an embeded resource
            var setup = new Setup();

            // get the config file from args
            string configFile = null;
            bool interactive = false;
            bool checkDB = false;
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "-c") || (args[i] == "--configFile"))
                    configFile = args[++i];
                else if (args[i] == "-i")
                    interactive = true;
                else if ((args[i] == "-d") || (args[i] == "--checkDB"))
                    checkDB = true;
            }

            // load the current setup
            if (configFile != null)
            {
                using (FileStream stream = File.OpenRead(configFile))
                    setup = JsonValue.ParseToObject<Setup>(ReadWithoutComment(stream));
                Console.WriteLine("Setup loaded from '" + configFile + "'");
            }
            else
            {
                Console.WriteLine("Default setup loaded");
            }

            string dbUrl = setup.database.url;

            // quick check to validate the currents models.
            // If not compatible with the DB Schema. STOP HERE
            if (!DB.CheckDBModels(new Dictionary<string, string>() { ["DEFAULT"] = dbUrl, ["DOCS"] = setup.doc.url }))
                return;

            // if only check DB is asked, stop here
            if (checkDB)
                return;

            if (!System.IO.Directory.Exists(setup.server.temporaryDirectory))
                System.IO.Directory.CreateDirectory(setup.server.temporaryDirectory);

            // clean the temporary dir
            foreach (var file in System.IO.Directory.EnumerateFiles(setup.server.temporaryDirectory))
                File.Delete(file);

            var logger = new Logger(setup.log, setup.mail);

            var server = new Server(setup.server.port, logger);
            server.StopOnException = setup.server.stopOnException;
            server.AllowGZip = setup.http.allowGZip;
            server.KeepAliveMax = setup.http.keepAliveMax;
            server.KeepAliveTimeout = setup.http.keepAliveTimeout;

            var sessions = new Sessions(
                dbUrl, setup.authentication.session.timeout,
                setup.authentication.session.longTimeout,
                setup.authentication.session.cookie);

            var contextInjector = new ContextInjector();
            server.Add(contextInjector);

            var mapper = new PathMapper();
            server.Add(mapper);
            mapper.Add("/api/sessions", sessions);
            mapper.Add("/api/subjects", new Subjects(dbUrl));
            mapper.Add("/api/grades", new Grades(dbUrl));
            var applications = new Applications(dbUrl);
            mapper.Add("/api/applications", applications);
            mapper.Add("/api/resources", new Resources(dbUrl, setup.server.storage));
            mapper.Add("/api/structures_resources", new StructuresResources(dbUrl));
            mapper.Add("/api/profiles_types", new ProfilesTypes(dbUrl));
            mapper.Add("/api/profiles", new Profiles(dbUrl));
            mapper.Add("/api/emails", new Emails(dbUrl));
            mapper.Add("/api/phones", new Phones(dbUrl));
            mapper.Add("/api/groups", new Groups(dbUrl));
            mapper.Add("/api/groups_users", new GroupsUsers(dbUrl));
            mapper.Add("/api/groups_grades", new GroupsGrades(dbUrl));
            mapper.Add("/api/structures_types", new StructuresTypes(dbUrl));
            mapper.Add("/api/structures", new Structures(dbUrl, setup.server.storage));
            mapper.Add("/api/user_links", new UserLinks(dbUrl));
            var users = new Users(dbUrl, setup.server.storage, setup.authentication.masterPassword);
            mapper.Add("/api/users", users);
            mapper.Add("/api/users_extended", new UsersExtended(dbUrl));
            mapper.Add("/api/sso", new Sso(dbUrl, users));
            mapper.Add("/api/tiles", new Tiles(dbUrl));
            mapper.Add("/api/flux", new PortailFlux(dbUrl));
            mapper.Add("/api/news", new PortailNews(dbUrl));
            mapper.Add("/api/users", new PortailRss(dbUrl));
            mapper.Add("/api/logs", new Logs(dbUrl));
            mapper.Add("/api/browser_logs", new BrowserLogs(dbUrl));
            mapper.Add("/api/ent", new Ents(dbUrl));
            mapper.Add("/api/publipostages", new Publipostages(dbUrl, setup.mail));

            mapper.Add("/api/structures", new StructureRss(logger, dbUrl));

            mapper.Add("/api/avatar/user", new StaticFiles(
                Path.Combine(setup.server.storage, "avatar"),
                setup.http.defaultCacheDuration));

            mapper.Add("/sso", new Cas(logger,
                dbUrl, sessions, users, setup.authentication,
                setup.mail, setup.sms, setup.gar));
            
            //mapper.Add("/sso/oidc", new OidcSso(setup.authentication.oidcSso, users, cas));

            mapper.Add("/api/aaf/synchronizations", new AafSyncService(
                dbUrl, setup.aaf.logPath, logger, setup.aaf.path, setup.aaf.zipPath, setup.aaf.logPath));

            mapper.Add("/api/aaf", new Aaf.Aaf(dbUrl, setup.aaf.path, setup.aaf.zipPath));

            mapper.Add("/api/setup", new SetupService(setup));
            mapper.Add("/api/manage", new Manage.ManageService());

            mapper.Add("/api/users", new Mail.ImapCheck(dbUrl));
            mapper.Add("/api/mailboxes", new Mail.Mailboxes(dbUrl, setup.mail.server.path));

            var blobs = new Blobs(logger, setup.doc.url, Path.Combine(setup.server.storage, "blobs"), setup.server.temporaryDirectory);
            mapper.Add("/api/blobs", blobs);
            mapper.Add("/api/docs/onlyoffice/sessions", new OnlyOfficeSessions(setup.doc.url));
            var docs = new Docs(setup.doc.url, setup.doc.path, setup.server.temporaryDirectory, blobs, setup.http.defaultCacheDuration, dbUrl, setup);
            mapper.Add("/api/docs", docs);

            //mapper.Add("/api/icons", new Icons(dbUrl));
            mapper.Add("/api/icons", new StaticIcons(setup.server.publicIcons, setup.http.defaultCacheDuration));

            mapper.Add("/api/sso_clients", new SsoClients(dbUrl));
            mapper.Add("/api/sso_clients_urls", new SsoClientsUrls(dbUrl));
            mapper.Add("/api/sso_clients_attributes", new SsoClientsAttributes(dbUrl));

            var smsService = new Sms.SmsService(logger, dbUrl, setup.sms);
            mapper.Add("/api/sms", smsService);

            mapper.Add("/api/bonapp", new BonApp.BonAppService(setup.restaurant.bonApp));

            mapper.Add("/api/book_allocations", new Textbook.BookAllocations(dbUrl));
            mapper.Add("/api/edulib", new Textbook.EduLibService(setup.textbook.eduLib, dbUrl));

            mapper.Add("/api/gar", new GAR.Resources(setup.gar, logger));

            mapper.Add("/docs", new ElFinder(setup.doc.url, setup.doc.path, setup.server.temporaryDirectory, blobs, setup.http.defaultCacheDuration, dbUrl, setup, docs));

            // if the request is not already handled, try static files
            server.Add(new StaticFiles(setup.server.publicFiles, setup.http.defaultCacheDuration));

            // inject some object needed for the HttpContextExtensions and ContextExtensions
            contextInjector.Inject("users", users);
            contextInjector.Inject("sessions", sessions);
            contextInjector.Inject("applications", applications);
            contextInjector.Inject("publicUrl", setup.server.publicUrl);
            contextInjector.Inject("setup", setup);

            // start a day scheduler to run the AAF sync task
            var dayScheduler = new Scheduler.DayScheduler(logger);
            foreach (var dayRun in setup.aaf.runs)
            {
                dayScheduler.Add(new Scheduler.DaySchedule
                {
                    Day = dayRun.day,
                    Time = dayRun.time,
                    // schedule the AAF synchronization task
                    Action = () => Synchronizer.DaySyncTask(
                        logger, setup.aaf.path, setup.aaf.zipPath, setup.aaf.logPath, dbUrl)
                });
            }

            server.Start();

            if (interactive)
            {
                Console.WriteLine("Press 'Q' to stop...");
                while (Console.ReadKey().Key != ConsoleKey.Q) { }
            }
            else
            {
                Console.WriteLine("Press 'Ctrl + C' to stop...");

                // catch signals for service stop
                var signals = new UnixSignal[] {
                    new UnixSignal(Signum.SIGINT),
                    new UnixSignal(Signum.SIGTERM),
                    new UnixSignal(Signum.SIGUSR2),
                };

                Signum signal;
                bool run = true;
                do
                {
                    var index = UnixSignal.WaitAny(signals, -1);
                    signal = signals[index].Signum;
                    switch (signal)
                    {
                        case Signum.SIGINT:
                            run = false;
                            break;
                        case Signum.SIGTERM:
                            run = false;
                            break;
                        case Signum.SIGUSR2:
                            run = false;
                            break;
                    }
                } while (run);
            }

            server.Stop();

            dayScheduler.Dispose();
            blobs.Dispose();
            smsService.Dispose();

        }
    }
}
