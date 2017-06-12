// PortailNews.cs
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
using System.IO;
using System.Xml;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "news", PrimaryKey = nameof(id))]
	public class News : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField]
		public string title { get { return GetField<string>(nameof(title), null); } set { SetField(nameof(title), value); } }
		[ModelField]
		public string description { get { return GetField<string>(nameof(description), null); } set { SetField(nameof(description), value); } }
		[ModelField]
		public DateTime pubDate { get { return GetField(nameof(pubDate), DateTime.Now); } set { SetField(nameof(pubDate), value); } }
		[ModelField]
		public string guid { get { return GetField<string>(nameof(guid), null); } set { SetField(nameof(guid), value); } }
		[ModelField(Required = true, ForeignModel = typeof(User))]
		public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
		[ModelField]
		public int? publipostage_id { get { return GetField<int?>(nameof(publipostage_id), null); } set { SetField(nameof(publipostage_id), value); } }
	}

	public class PortailNews : ModelService<News>
	{
		public PortailNews(string dbUrl) : base(dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();
		}
	}

	public class PortailRss : HttpRouting
	{
		public PortailRss(string dbUrl)
		{
			GetAsync["/{uid}/rss"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var dom = new XmlDocument();
					var dc = "http://purl.org/dc/elements/1.1/";
					var ns = new XmlNamespaceManager(dom.NameTable);
					ns.AddNamespace("dc", dc);

					var rss = dom.CreateElement("rss");
					rss.SetAttribute("version", "2.0");
					rss.SetAttribute("xmlns:dc", dc);
					dom.AppendChild(rss);

					var channel = dom.CreateElement("channel");
					rss.AppendChild(channel);

					var title = dom.CreateElement("title");
					title.InnerText = "News feed for " + p["uid"];
					channel.AppendChild(title);

					var link = dom.CreateElement("link");
					link.InnerText = c.Request.FullPath;
					channel.AppendChild(link);

					var description = dom.CreateElement("description");
					description.InnerText = "News feed for " + p["uid"];
					channel.AppendChild(description);

					foreach (var item in await db.SelectAsync("SELECT * FROM news WHERE user_id=?", (string)p["uid"]))
					{
						var xmlItem = dom.CreateElement("item");
						channel.AppendChild(xmlItem);

						var itemTitle = dom.CreateElement("title");
						itemTitle.InnerText = (string)item["title"];
						xmlItem.AppendChild(itemTitle);

						var itemLink = dom.CreateElement("link");
						itemLink.InnerText = "notYetImplemented";
						xmlItem.AppendChild(itemLink);

						var itemDescription = dom.CreateElement("description");
						itemDescription.InnerText = (string)item["description"];
						xmlItem.AppendChild(itemDescription);

						var pubDate = dom.CreateElement("pubDate");
						pubDate.InnerText = ((DateTime)item["pubDate"]).ToString("R");
						xmlItem.AppendChild(pubDate);

						var dcDate = dom.CreateElement("dc:date", dc);
						dcDate.InnerText = ((DateTime)item["pubDate"]).ToString("O");
						xmlItem.AppendChild(dcDate);
					}
					c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "application/rss+xml";

					using (var stringWriter = new StringWriter())
					{
						var settings = new XmlWriterSettings();
						settings.Encoding = System.Text.Encoding.UTF8;
						settings.Indent = true;
						using (var xmlTextWriter = XmlWriter.Create(stringWriter, settings))
						{
							dom.Save(xmlTextWriter);
							c.Response.Content = stringWriter.ToString();
						}
					}
				}
			};
		}
	}
}
