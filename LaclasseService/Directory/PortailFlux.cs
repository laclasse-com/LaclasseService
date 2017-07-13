// PortailFlux.cs
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
using System.Xml;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "flux_portail", PrimaryKey = nameof(id))]
	public class FluxPortail : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField]
		public int nb { get { return GetField(nameof(nb), 0); } set { SetField(nameof(nb), value); } }
		[ModelField]
		public string url { get { return GetField<string>(nameof(url), null); } set { SetField(nameof(url), value); } }
		[ModelField]
		public string name { get { return GetField<string>(nameof(name), null); } set { SetField(nameof(name), value); } }
		[ModelField(Required = true, ForeignModel = typeof(Structure))]
		public string structure_id { get { return GetField<string>(nameof(structure_id), null); } set { SetField(nameof(structure_id), value); } }
	}

	public class PortailFlux : ModelService<FluxPortail>
	{
		public PortailFlux(string dbUrl) : base(dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();
		}
	}

	public class Rss : Model
	{
		[ModelField]
		public string title { get { return GetField<string>(nameof(title), null); } set { SetField(nameof(title), value); } }
		[ModelField]
		public string content { get { return GetField<string>(nameof(content), null); } set { SetField(nameof(content), value); } }
		[ModelField]
		public DateTime pubDate { get { return GetField(nameof(pubDate), DateTime.Now); } set { SetField(nameof(pubDate), value); } }
		[ModelField]
		public string link { get { return GetField<string>(nameof(link), null); } set { SetField(nameof(link), value); } }
		[ModelField]
		public string image { get { return GetField<string>(nameof(image), null); } set { SetField(nameof(image), value); } }
	}

	public class StructureRss : HttpRouting
	{ 
		public StructureRss(string dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

			GetAsync["/{uai}/rss"] = async (p, c) =>
			{
				var structure = new Structure { id = (string)p["uai"] };
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					if (await structure.LoadAsync(db))
					{
						await structure.LoadExpandFieldAsync(db, nameof(structure.flux));

						var infos = new ModelList<Rss>();

						// parallel loading
						await Task.WhenAll(structure.flux.Select((flux) => Task.Run(() =>
						{
							try
							{
								var settings = new XmlReaderSettings();
								settings.IgnoreComments = true;
								settings.DtdProcessing = DtdProcessing.Ignore;

								var uri = new Uri(flux.url);
								using (var client = HttpClient.Create(uri))
								{
									var clientRequest = new HttpClientRequest();
									clientRequest.Method = "GET";
									clientRequest.Path = uri.PathAndQuery;
									client.SendRequest(clientRequest);

									var response = client.GetResponse();

									var reader = XmlReader.Create(response.InputStream, settings);
									var feed = SyndicationFeed.Load(reader);
									reader.Close();
									foreach (SyndicationItem item in feed.Items)
									{
										var rss = new Rss
										{
											title = item.Title.Text,
											content = item.Summary.Text,
											pubDate = item.PublishDate.DateTime
										};

										var encodedContent = item.ElementExtensions.SingleOrDefault((arg) => arg.OuterName == "encoded" && arg.OuterNamespace == "http://purl.org/rss/1.0/modules/content/");
										if (encodedContent != null)
											rss.content = encodedContent.GetObject<XmlElement>().InnerText;

										// load the document using sgml reader
										var document = new XmlDocument();
										using (var sgmlReader = new Sgml.SgmlReader())
										{
											sgmlReader.CaseFolding = Sgml.CaseFolding.ToLower;
											sgmlReader.DocType = "HTML";
											sgmlReader.WhitespaceHandling = WhitespaceHandling.None;

											using (var sr = new StringReader(rss.content))
											{
												sgmlReader.InputStream = sr;
												document.Load(sgmlReader);
											}
										}

										string imageUrl = null;
										var images = document.GetElementsByTagName("img");
										foreach (XmlNode image in images)
										{
											if (image.Attributes["src"] != null)
											{
												imageUrl = image.Attributes["src"].Value;
												break;
											}
										}
										if (imageUrl != null)
											rss.image = imageUrl;

										if (item.Links.Count > 0)
											rss.link = item.Links[0].Uri.AbsoluteUri;

										lock (infos)
											infos.Add(rss);
									}
								}
							}
							catch (Exception)
							{
								Console.WriteLine($"ERROR: invalid RSS feed '{flux.url}'");
							}
						})));
						c.Response.StatusCode = 200;
						c.Response.Content = infos.Filter(c);
					}
				}
			};
		}
	}
}
