// Server.cs
// 
//  The Laclasse HTTP server.
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2013-2015 Departement du Rhone - Metropole de Lyon
// Copyright (c) 2017 Metropole de Lyon
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
using System.Globalization;
using System.Threading.Tasks;
using System.Net.Mail;
using Erasme.Http;
using Erasme.Json;

namespace Laclasse
{
	public class Server: HttpServer
	{
		public Server(int port) : base(port)
		{
		}

		protected override async Task ProcessRequestAsync(HttpContext context)
		{
			await base.ProcessRequestAsync(context);

			// if response not sent, send it
			if (!context.Response.Sent)
				await context.SendResponseAsync();

			// log the request

			// remote address
			string log = context.Request.RemoteEndPoint + " ";
			// user
			if (context.User != null)
				log += context.User + " ";
			else
				log += "- ";
			// request 
			log += "\"" + context.Request.Method + " " + context.Request.FullPath + "\" ";
			// response
			if (context.WebSocket != null)
				log += "WS ";
			else
				log += context.Response.StatusCode + " ";
			// bytes received
			log += context.Request.ReadCounter + "/" + context.Request.WriteCounter + " ";
			// time
			log += Math.Round((DateTime.Now - context.Request.StartTime).TotalMilliseconds).ToString(CultureInfo.InvariantCulture) + "ms";

			// write the log
			await Console.Out.WriteLineAsync(log);
		}

		protected override void OnWebSocketHandlerMessage(WebSocketHandler handler, string message)
		{
			// log the message

			// remote address
			string log = handler.Context.Request.RemoteEndPoint + " ";
			// user
			if (handler.Context.User != null)
				log += handler.Context.User + " ";
			else
				log += "- ";
			// request 
			log += "\"WSMI " + handler.Context.Request.FullPath + "\" \"" + message + "\"";

			// write the log
			Console.WriteLine(log);

			// handle the message
			base.OnWebSocketHandlerMessage(handler, message);
		}

		protected override void WebSocketHandlerSend(WebSocketHandler handler, string message)
		{
			base.WebSocketHandlerSend(handler, message);

			// log the message

			// remote address
			string log = handler.Context.Request.RemoteEndPoint + " ";
			// user
			if (handler.Context.User != null)
				log += handler.Context.User + " ";
			else
				log += "- ";
			// request 
			log += "\"WSMO " + handler.Context.Request.FullPath + "\" \"" + message + "\"";

			// write the log
			Console.WriteLine(log);
		}

		protected override void OnProcessRequestError(HttpContext context, Exception exception)
		{
			base.OnProcessRequestError(context, exception);

			string detail = null;

			// handle web exceptions
			if ((exception is WebException) && (context.WebSocket == null))
			{
				var webException = (WebException)exception;
				context.Response.StatusCode = webException.StatusCode;
				JsonValue json = new JsonObject();
				json["error"] = webException.Message;
				context.Response.Content = json;
				if (webException.Exception != null)
					exception = webException.Exception;
			}

			// dont log WebException
			if (exception is WebException)
				return;

			var log = new StringBuilder();

			// remote address
			log.Append(context.Request.RemoteEndPoint.ToString());
			log.Append(" ");

			// x-forwarded-for
			if (context.Request.Headers.ContainsKey("x-forwarded-for"))
			{
				log.Append("[");
				log.Append(context.Request.Headers["x-forwarded-for"]);
				log.Append("] ");
			}

			// user
			if (context.User != null)
			{
				log.Append(context.User);
				log.Append(" ");
			}
			else
				log.Append("- ");

			// request 
			log.Append("\"");
			log.Append(context.Request.Method);
			log.Append(" ");
			log.Append(context.Request.FullPath);
			log.Append("\" ");
			// response
			if (context.WebSocket != null)
				log.Append("WS ");
			else
			{
				log.Append(context.Response.StatusCode);
				log.Append(" ");
			}
			// bytes received
			log.Append(context.Request.ReadCounter);
			log.Append("/");
			log.Append(context.Request.WriteCounter);
			log.Append(" ");
			// time
			log.Append(Math.Round((DateTime.Now - context.Request.StartTime).TotalMilliseconds).ToString(CultureInfo.InvariantCulture));
			log.Append("ms\n");
			// exception details
			if (detail != null)
			{
				log.Append(detail);
				log.Append("\n");
			}
			log.Append(exception.ToString());

			// write the log
			Console.WriteLine(log);
		}
	}
}
