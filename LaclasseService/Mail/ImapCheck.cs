// ImapCheck.cs
// 
//  API for the SSO 
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
using System.Collections.Generic;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using MimeKit;

namespace Laclasse.Mail
{
	public class ImapCheck : HttpRouting
	{
		readonly string dbUrl;

		public ImapCheck(string dbUrl)
		{
			GetAsync["/test"] = async (p, c) =>
			{
				/*using (var client = new ImapClient())
				{
					// For demo-purposes, accept all SSL certificates
					//client.ServerCertificateValidationCallback = (s, c, h, e) => true;

					client.Connect("v3dev.laclasse.com", 993, true);

					// Note: since we don't have an OAuth2 token, disable
					// the XOAUTH2 authentication mechanism.
					client.AuthenticationMechanisms.Remove("XOAUTH2");

					client.Authenticate("login", "password");

					// The Inbox folder is always available on all IMAP servers...
					var inbox = client.Inbox;
					inbox.Open(FolderAccess.ReadOnly);

					Console.WriteLine("Total messages: {0}", inbox.Count);
					Console.WriteLine("Recent messages: {0}", inbox.Recent);

					for (int i = 0; i < inbox.Count; i++)
					{
						var message = inbox.GetMessage(i);
						Console.WriteLine("Subject: {0}", message.Subject);
					}

					client.Disconnect(true);
				}*/

				// TODO
				c.Response.StatusCode = 200;
				c.Response.Content = "TODO";
				await Task.FromResult(true);


			};
		}
	}
}
