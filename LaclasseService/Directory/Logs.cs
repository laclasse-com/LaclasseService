// Log.cs
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
		[ModelField(Required = true, ForeignModel = typeof(User))]
		public string application_id { get { return GetField<string>(nameof(application_id), null); } set { SetField(nameof(application_id), value); } }
		[ModelField(Required = true, ForeignModel = typeof(User))]
		public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
		[ModelField(Required = true, ForeignModel = typeof(Structure))]
		public string structure_id { get { return GetField<string>(nameof(structure_id), null); } set { SetField(nameof(structure_id), value); } }
		[ModelField(Required = true)]
		public string profil_id { get { return GetField<string>(nameof(profil_id), null); } set { SetField(nameof(profil_id), value); } }
		[ModelField(Required = true)]
		public string url { get { return GetField<string>(nameof(url), null); } set { SetField(nameof(url), value); } }
		[ModelField]
		public string parameters { get { return GetField<string>(nameof(parameters), null); } set { SetField(nameof(parameters), value); } }
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

	public class Logs : ModelService<Log>
	{
		public Logs(string dbUrl) : base(dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();
		}
	}
}
