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
using System.Reflection;
using System.Text;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Directory;
using Laclasse.Authentication;

namespace Laclasse
{
	class MainClass
	{
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
			JsonValue setup;
			using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Laclasse.laclasse.conf"))
			{
				setup = ReadCommentedJson(stream);
			}

			// get the config file from args
			string configFile = null;
			for (int i = 0; i < args.Length; i++)
			{
				if ((args[i] == "-c") || (args[i] == "--configFile"))
					configFile = args[++i];
			}

			// load the current setup
			if (configFile != null)
			{
				JsonValue currentSetup;
				using (FileStream stream = File.OpenRead(configFile))
				{
					currentSetup = ReadCommentedJson(stream);
				}
				setup.Merge(currentSetup);
				Console.WriteLine("Setup loaded from '" + configFile + "'");
			}
			else
			{
				Console.WriteLine("Default setup loaded");
			}

			string dbUrl = setup["database"]["url"];

			var server = new Server(setup["server"]["port"]);
			server.StopOnException = setup["server"]["stopOnException"];
			server.AllowGZip = setup["http"]["allowGZip"];
			server.KeepAliveMax = setup["http"]["keepAliveMax"];
			server.KeepAliveTimeout = setup["http"]["keepAliveTimeout"];

			var sessions = new Sessions(
				dbUrl, setup["authentication"]["session"]["timeout"],
				setup["authentication"]["session"]["cookie"]);

			var contextInjector = new ContextInjector();
			server.Add(contextInjector);

			var mapper = new PathMapper();
			server.Add(mapper);
			mapper.Add("/api/sessions", sessions);
			var matieres = new Matieres(dbUrl);
			mapper.Add("/api/matieres", matieres);
			var niveaux = new Niveaux(dbUrl);
			mapper.Add("/api/niveaux", niveaux);
			var applications = new Applications(dbUrl);
			mapper.Add("/api/applications", applications);
			var resources = new Resources(dbUrl);
			mapper.Add("/api/resources", resources);
			var profils = new Profils(dbUrl);
			mapper.Add("/api/profils", profils);
			var emails = new Emails(dbUrl);
			mapper.Add("/api/emails", emails);
			var groups = new Groups(dbUrl, niveaux);
			mapper.Add("/api/groups", groups);
			mapper.Add("/api/structures_types", new StructuresTypes(dbUrl));
			var structures = new Structures(dbUrl, groups, resources, profils);
			mapper.Add("/api/structures", structures);
			var users = new Users(
				dbUrl, emails, profils, groups, structures, resources, setup["server"]["storage"],
				setup["authentication"]["masterPassword"]);
			mapper.Add("/api/users", users);
			mapper.Add("/api/app/users", users);
			mapper.Add("/api/sso", new Sso(dbUrl, users));
			mapper.Add("/api/structures", new PortailEntree(dbUrl, structures));
			mapper.Add("/api/structures", new PortailFlux(dbUrl));
			mapper.Add("/api/users", new PortailNews(dbUrl));
			mapper.Add("/api/logs", new Logs(dbUrl));

			mapper.Add("/api/avatar/user", new StaticFiles(
				Path.Combine(setup["server"]["storage"], "avatar"),
				setup["http"]["defaultCacheDuration"]));

			mapper.Add("/sso", new Cas(
				dbUrl, sessions, users, structures, setup["authentication"]["session"]["cookie"],
				setup["authentication"]["cas"]["ticketTimeout"],
				setup["authentication"]["aafSso"]));

			// if the request is not already handled, try static files
			server.Add(new StaticFiles(
				setup["server"]["static"], setup["http"]["defaultCacheDuration"]));

			// inject some object needed for the HttpContextExtensions and ContextExtensions
			contextInjector.Inject("users", users);
			contextInjector.Inject("sessions", sessions);
			contextInjector.Inject("applications", applications);
			contextInjector.Inject("publicUrl", (string)setup["server"]["publicUrl"]);

			//var n1 = new Niveau { id = "12345", name = "quiche", rattach = "34566", stat = "112233" };
			//var n2 = new Niveau { id = "12345", name = "quiche", rattach = "34566", stat = "112233" };

			//Console.WriteLine("Test n1 et n2: " + (n1 != n2));

			//return;
			//var sync = new Laclasse.Aaf.Synchronizer(dbUrl, matieres, niveaux);
			//sync.Synchronize().Wait();
			//return;

			server.Start();
			Console.WriteLine("Press 'Q' to stop...");
			while (Console.ReadKey().Key != ConsoleKey.Q) { }
			server.Stop();
		}
	}
}
