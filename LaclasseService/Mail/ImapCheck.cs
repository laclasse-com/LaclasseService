// ImapCheck.cs
// 
//  API to check the user's recent emails
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

using System.Linq;
using Erasme.Http;
using Laclasse.Authentication;

using MailKit.Net.Imap;
using MailKit;

using Laclasse.Directory;

namespace Laclasse.Mail
{
	public class ImapCheckResult : Model
	{
		[ModelField]
		public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
		[ModelField]
		public long total { get { return GetField<long>(nameof(total), 0); } set { SetField(nameof(total), value); } }
		[ModelField]
		public long recent { get { return GetField<long>(nameof(recent), 0); } set { SetField(nameof(recent), value); } }
	}

	public class ImapCheck : HttpRouting
	{
		public ImapCheck(string dbUrl)
		{
			GetAsync["/{user_id}/imapcheck"] = async (p, c) =>
			{
				var user = new User { id = (string)p["user_id"] };
				using (var db = await DB.CreateAsync(dbUrl, true))
				{
					// if user exists and as an Ent email
					if (await user.LoadAsync(db, true) && user.email_backend_id != null && user.emails.Any((email) => email.type == EmailType.Ent))
					{
						// check rights
						await c.EnsureHasRightsOnUserAsync(user, false, true, false);
							
						// find the user's email backend
						var backend = new EmailBackend { id = (int)user.email_backend_id };
						if (await backend.LoadAsync(db))
						{
							var result = GetUserImapCheck(user.id, backend);
							c.Response.StatusCode = 200;
							c.Response.Content = result;
						}
					}
				}
			};
		}

		ImapCheckResult GetUserImapCheck(string user_id, EmailBackend backend)
		{
			var result = new ImapCheckResult() { user_id = user_id };
			using (var client = new ImapClient())
			{
				// Accept all SSL certificates (internal server)
				client.ServerCertificateValidationCallback = (s, c, h, e) => true;

				client.Connect(backend.address, 143, false);

				// Note: since we don't have an OAuth2 token, disable
				// the XOAUTH2 authentication mechanism.
				client.AuthenticationMechanisms.Remove("XOAUTH2");

				client.Authenticate(user_id, backend.master_key);

				// The Inbox folder is always available on all IMAP servers...
				var inbox = client.Inbox;
				inbox.Open(FolderAccess.ReadOnly);

				result.total = inbox.Count;
				result.recent = inbox.Recent;

				client.Disconnect(true);
			}
			return result;
		}
	}
}
