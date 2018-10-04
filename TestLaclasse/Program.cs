using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using Erasme.Http;
using Erasme.Json;

namespace TestLaclasse
{
	delegate bool TestHandler();

	class MainClass
	{
		static int NbRequest = 100;
		static int NbThread = 4;
		static int SlowRequestLevel = 50;
		static string HostName = "localhost";
		static int Port = 80;
		static string User = "admin";
		static string Password = "masterPassword";

		public static void Display(string desc, TestHandler handler)
		{
			var watch = new Stopwatch();
			watch.Start();
			var res = handler();
			watch.Stop();
			Console.Write($"Test {desc} in {watch.ElapsedMilliseconds} ms : ");
			if (res)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("DONE");
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("FAILS");
			}
			Console.ForegroundColor = ConsoleColor.Black;
		}

		public static void Bench(string display, TestHandler handler)
		{
			var watch = new Stopwatch();
			watch.Start();
			if (NbThread == 1)
			{
				BenchThreadStart(handler);
			}
			else
			{
				List<Thread> threads = new List<Thread>();
				for (int i = 0; i < NbThread; i++)
				{
					Thread thread = new Thread(BenchThreadStart);
					threads.Add(thread);
					thread.Start(handler);
				}
				foreach (Thread thread in threads)
				{
					thread.Join();
				}
			}
			watch.Stop();
			TimeSpan duration = watch.Elapsed;
			Console.Write("Bench " + display + " ");
			int reqSecond = (int)Math.Round((NbRequest * NbThread) / duration.TotalSeconds);
			if (reqSecond < SlowRequestLevel)
				Console.ForegroundColor = ConsoleColor.Red;
			else
				Console.ForegroundColor = ConsoleColor.DarkBlue;
			Console.Write("{0}", reqSecond);
			Console.ForegroundColor = ConsoleColor.Black;
			Console.WriteLine(" req/s");
		}

		static void BenchThreadStart(object obj)
		{
			TestHandler handler = (TestHandler)obj;
			for (int i = 0; i < NbRequest; i++)
			{
				handler();
			}
		}

		static string GetHttpBasic()
		{
			return "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{User}:{Password}"));
		}

		static void ParseArgs(string[] args)
		{
			for (int i = 0; i < args.Length; i++)
			{
				var arg = args[i];
				if (arg == "-h")
				{
					Console.WriteLine(
@"Help:
  -s hostname
  -p port
  -u username:password
");
					Environment.Exit(0);
				}
				if (i == args.Length - 1)
					break;
				if (arg == "-s")
					HostName = args[++i];
				else if (arg == "-p")
					Port = int.Parse(args[++i]);
				else if (arg == "-u")
				{
					var tab = args[++i].Split(':');
					User = tab[0];
					Password = tab[1];
				}
			}
		}

		////////////////////////////////////////////////////////////////////////////////
		/// TESTS START
		////////////////////////////////////////////////////////////////////////////////

		public static bool TestGet1000Users(bool expand = false)
		{
			bool done = false;
			using (HttpClient client = HttpClient.Create(HostName, Port))
			{
				HttpClientRequest request = new HttpClientRequest();
				request.Method = "GET";
				request.Path = "/api/users";
				request.QueryString["limit"] = "1000";
				request.QueryString["expand"] = expand ? "true" : "false";
				request.Headers["authorization"] = GetHttpBasic();
				client.SendRequest(request);
				HttpClientResponse response = client.GetResponse();
				done = (response.StatusCode == 200);
			}
			return done;
		}

		public static bool TestGet1000UsersNotExpanded()
		{
			return TestGet1000Users(false);
		}

		public static bool TestGet1000UsersExpanded()
		{
			return TestGet1000Users(true);
		}

		public static bool TestGet10000Logs()
		{
			bool done = false;
			using (HttpClient client = HttpClient.Create(HostName, Port))
			{
				HttpClientRequest request = new HttpClientRequest();
				request.Method = "GET";
				request.Path = "/api/logs";
				request.QueryString["limit"] = "10000";
				request.Headers["authorization"] = GetHttpBasic();
				client.SendRequest(request);
				HttpClientResponse response = client.GetResponse();
				done = (response.StatusCode == 200);
			}
			return done;
		}

		public static bool TestGetWholeStructure()
		{
			bool done = false;
			using (HttpClient client = HttpClient.Create(HostName, Port))
			{
				HttpClientRequest request = new HttpClientRequest();
				request.Method = "GET";
				request.Path = "/api/structures/0691669P";
				request.QueryString["expand"] = "true";
				request.Headers["authorization"] = GetHttpBasic();
				client.SendRequest(request);
				HttpClientResponse response = client.GetResponse();
				done = (response.StatusCode == 200);
			}
			return done;
		}

		public static bool TestGet1MonthStats()
		{
			bool done = false;
			using (HttpClient client = HttpClient.Create(HostName, Port))
			{
				HttpClientRequest request = new HttpClientRequest();
				request.Method = "GET";
				request.Path = "/api/logs/stats";
				request.QueryString["mode"] = "1MONTH";
				request.QueryString["timestamp>"] = "2018-08-31T22:00:00.000Z";
				request.QueryString["timestamp<"] = "2018-09-30T22:00:00.000Z";
				request.Headers["authorization"] = GetHttpBasic();
				client.SendRequest(request);
				HttpClientResponse response = client.GetResponse();
				done = (response.StatusCode == 200);
			}
			return done;
		}

		public static bool TestGet1MonthBrowserStats()
		{
			bool done = false;
			using (HttpClient client = HttpClient.Create(HostName, Port))
			{
				HttpClientRequest request = new HttpClientRequest();
				request.Method = "GET";
				request.Path = "/api/browser_logs/stats";
				request.QueryString["mode"] = "1MONTH";
				request.QueryString["timestamp>"] = "2018-08-31T22:00:00.000Z";
				request.QueryString["timestamp<"] = "2018-09-30T22:00:00.000Z";
				request.Headers["authorization"] = GetHttpBasic();
				client.SendRequest(request);
				HttpClientResponse response = client.GetResponse();
				done = (response.StatusCode == 200);
			}
			return done;
		}

		////////////////////////////////////////////////////////////////////////////////
		/// TESTS END
		////////////////////////////////////////////////////////////////////////////////

		public static void Main(string[] args)
		{
			ParseArgs(args);

			var watch = new Stopwatch();
			watch.Start();
			Console.WriteLine("Start tests...");

			Display("Get 10000 Logs", TestGet10000Logs);
			//Display("Get 1000 Users", TestGet1000UsersNotExpanded);
			Display("Get 1000 Users Expand", TestGet1000UsersExpanded);
			Display("Get whole structure", TestGetWholeStructure);
			Display("Get 1 month general stats", TestGet1MonthStats);
			Display("Get 1 month browsers stats", TestGet1MonthBrowserStats);

			watch.Stop();
			Console.WriteLine($"Stop tests. Total time: {watch.ElapsedMilliseconds} ms");
		}
	}
}
