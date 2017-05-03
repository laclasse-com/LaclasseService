// CasClient.cs
// 
//  API for a CAS client
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

using System.Xml;
using System.Collections.Generic;
using Erasme.Http;

namespace Laclasse.Authentication
{
	public class CasClient : HttpRouting
	{
		public CasClient(string ssoUrl)
		{
			string service = "http://localhost:4321/auth/cas/callback";
			Get["/auth/cas"] = (p, c) =>
			{
				c.Response.StatusCode = 302;
				c.Response.Headers["location"] = ssoUrl+"login?service=" +
					HttpUtility.UrlEncode(service);
			};

			GetAsync["/auth/cas/callback"] = async (p, c) =>
			{
				string ticket = c.Request.QueryString["ticket"];
				var xmlString = await HttpClient.GetAsStringAsync(
					ssoUrl+"serviceValidate?" +
					"ticket=" + HttpUtility.UrlEncode(ticket) +
					"&service=" + HttpUtility.UrlEncode(service)
				);

				var dom = new XmlDocument();
				dom.LoadXml(xmlString);

				var ns = new XmlNamespaceManager(dom.NameTable);
				ns.AddNamespace("cas", "http://www.yale.edu/tp/cas");

				var attributes = new Dictionary<string, string>();

				foreach (XmlElement node in dom.SelectSingleNode("//cas:attributes", ns).ChildNodes)
				{
					attributes[node.LocalName] = node.InnerText;
				}

				if (OnSessionOpen != null)
					OnSessionOpen(attributes);

				c.Response.StatusCode = 302;
				c.Response.Headers["location"] = "/";
			};
		}

		public delegate void SessionOpen(Dictionary<string, string> attributes);

		public SessionOpen OnSessionOpen { set; private get; }
	}
}
