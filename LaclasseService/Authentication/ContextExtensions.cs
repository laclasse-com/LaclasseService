// ContextExtensions.cs
// 
//  Inject object in an HttpContext. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
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
using System.Threading.Tasks;
using Erasme.Http;

namespace Laclasse.Authentication
{
	public static class ContextExtensions
	{
		public async static Task<string> RequireAuthenticatedAsync(this HttpContext context)
		{
			var user = await context.GetAuthenticatedUserAsync();
			if (user == null)
			{
				// TODO: handle CAS authentication process
				//throw new WebException(401, "Authentication is needed");
			}
			return user;
		}

		public async static Task<string> EnsureIsAuthenticatedAsync(this HttpContext context)
		{
			var user = await context.GetAuthenticatedUserAsync();
			if (user == null)
				throw new WebException(401, "Authentication is needed");
			return user;
		}

		public async static Task<string> GetAuthenticatedUserAsync(this HttpContext context)
		{
			// check in the sessions
			var user = await ((Authentication.Sessions)context.Data["sessions"]).GetAuthenticatedUserAsync(context);

			// check for HTTP Basic authorization
			if ((user == null) && (context.Request.Headers.ContainsKey("authorization")))
			{
				var parts = context.Request.Headers["authorization"].Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
				if (parts[0].ToLowerInvariant() == "basic")
				{
					var authorization = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
					var pos = authorization.IndexOf(':');
					if (pos != -1)
					{
						var login = authorization.Substring(0, pos);
						var password = authorization.Substring(pos + 1);
						// check in the users
						user = await ((Directory.Users)context.Data["users"]).CheckPasswordAsync(login, password);

						// check in the applications
						if (user == null)
							user = await ((Directory.Applications)context.Data["applications"]).CheckPasswordAsync(login, password);
					}
				}
			}

			// check for Query string authentication
			if ((user == null) && context.Request.QueryString.ContainsKey("app_id") &&
				context.Request.QueryString.ContainsKey("app_key"))
			{
				var login = context.Request.QueryString["app_id"];
				var password = context.Request.QueryString["app_key"];
				// check in the applications
				user = await ((Directory.Applications)context.Data["applications"]).CheckPasswordAsync(login, password);
			}

			context.User = user;
			return context.User;
		}
	}

}
