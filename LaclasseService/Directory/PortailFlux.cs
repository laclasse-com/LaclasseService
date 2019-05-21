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
using System.Xml.Linq;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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

        public override SqlFilter FilterAuthUser(AuthenticatedUser user)
        {
            if (user.IsSuperAdmin || user.IsApplication)
                return new SqlFilter();
            var structuresIds = user.user.profiles.Select((arg) => arg.structure_id).Distinct();
            return new SqlFilter() { Where = DB.InFilter(nameof(structure_id), structuresIds) };
        }

        public override async Task EnsureRightAsync(HttpContext context, Right right, Model diff)
        {
            bool loadDone = false;
            var structure = new Structure { id = structure_id };
            using (var db = await DB.CreateAsync(context.GetSetup().database.url))
                loadDone = await structure.LoadAsync(db, true);
            if (!loadDone)
                throw new WebException(403, "Insufficient rights");
            //context.EnsureHasRightsOnStructureAsync(
            await context.EnsureHasRightsOnStructureAsync(
                structure, true, (right == Right.Update), (right == Right.Delete | right == Right.Update | right == Right.Create));
        }
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
        Logger logger;

        public StructureRss(Logger logger, string dbUrl)
        {
            this.logger = logger;

            // API only available to authenticated users
            BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

            var cache = new Utils.Cache<List<Rss>>(400, TimeSpan.FromMinutes(20), async (string key) =>
            {
                logger.Log(LogLevel.Info, $"Load RSS: {key}");
                var items = new List<Rss>();
                try
                {
                    var settings = new XmlReaderSettings
                    {
                        IgnoreComments = true,
                        DtdProcessing = DtdProcessing.Ignore
                    };
                    var uri = new Uri(key);
                    var doc = await LoadXmlAsync(uri);
                    XElement root = doc.Root;
                    XNamespace ns = doc.Root.Name.Namespace;
                    var contentns = "http://purl.org/rss/1.0/modules/content/";
                    var dcns = "http://purl.org/dc/elements/1.1/";

                    // RSS 2.0
                    if (root.Name.LocalName == "rss")
                    {
                        foreach (XElement item in root.Element("channel").Elements("item"))
                        {
                            var rss = new Rss();
                            var title = item.Element("title");
                            if (title != null)
                                rss.title = title.Value;
                            var content = item.Element("description");
                            if (content != null)
                                rss.content = content.Value;
                            var link = item.Element("link");
                            if (link != null)
                                rss.link = link.Value;
                            var date = item.Element("pubDate");
                            if (date == null)
                                date = item.Element(XName.Get("date", dcns));
                            if (date != null)
                                rss.pubDate = DateTime.Parse(date.Value);
                            var encoded = item.Element(XName.Get("encoded", contentns));
                            if (encoded != null)
                            {
                                var imageUrl = GetImageFromHtml(encoded.Value);
                                if (imageUrl != null)
                                    rss.image = imageUrl;
                            }
                            items.Add(rss);
                        }
                    }
                    // ATOM
                    else if (root.Name.LocalName == "feed")
                    {
                        var atomns = "http://www.w3.org/2005/Atom";
                        foreach (XElement item in root.Elements(XName.Get("entry", atomns)))
                        {
                            var rss = new Rss();
                            var title = item.Element(XName.Get("title", atomns));
                            if (title != null)
                                rss.title = title.Value;
                            var summary = item.Element(XName.Get("summary", atomns));
                            if (summary != null)
                                rss.content = summary.Value;
                            var content = item.Element(XName.Get("content", atomns));
                            if (content != null)
                            {
                                var type = "text";
                                if (content.Attribute("type") != null)
                                    type = content.Attribute("type").Value;
                                var textContent = content.Value;
                                if (type == "html")
                                {
                                    textContent = GetTextFromHtml(content.Value);
                                    var imageUrl = GetImageFromHtml(content.Value);
                                    if (imageUrl != null)
                                        rss.image = imageUrl;
                                }
                                if (rss.content == null)
                                    rss.content = textContent;
                            }
                            var link = item.Element(XName.Get("link", atomns));
                            if (link != null)
                                rss.link = link.Attribute("href").Value;
                            var updated = item.Element(XName.Get("updated", atomns));
                            if (updated != null)
                                rss.pubDate = DateTime.Parse(updated.Value);
                            items.Add(rss);
                        }
                    }
                    // RSS 1.0
                    else if (root.Name.LocalName == "RDF")
                    {
                        var rss10ns = "http://purl.org/rss/1.0/";

                        foreach (XElement item in root.Elements(XName.Get("item", rss10ns)))
                        {
                            var rss = new Rss();
                            var title = item.Element(XName.Get("title", rss10ns));
                            if (title != null)
                                rss.title = title.Value;
                            var content = item.Element(XName.Get("description", rss10ns));
                            if (content != null)
                                rss.content = content.Value;
                            var link = item.Element(XName.Get("link", rss10ns));
                            if (link != null)
                                rss.link = link.Value;
                            var date = item.Element(XName.Get("date", dcns));
                            if (date != null)
                                rss.pubDate = DateTime.Parse(date.Value);
                            var encoded = item.Element(XName.Get("encoded", contentns));
                            if (encoded != null)
                            {
                                var imageUrl = GetImageFromHtml(encoded.Value);
                                if (imageUrl != null)
                                    rss.image = imageUrl;
                            }
                            items.Add(rss);
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Log(LogLevel.Error, $"invalid RSS feed '{key}'. Exception: {e}");
                    items = null;
                }
                return items;
            }, true);


            GetAsync["/{uai}/rss"] = async (p, c) =>
            {
                var structure = new Structure { id = (string)p["uai"] };
                using (DB db = await DB.CreateAsync(dbUrl))
                {
                    if (await structure.LoadAsync(db))
                    {
                        await c.EnsureHasRightsOnStructureAsync(structure, true, false, false);
                        await structure.LoadExpandFieldAsync(db, nameof(structure.flux));

                        var infos = new ModelList<Rss>();
                        // parallel loading
                        var tasks = structure.flux.Select((flux) => cache.GetAsync(flux.url));
                        await Task.WhenAll(tasks);
                        tasks.ForEach((t) => { if (t.Result != null) infos.AddRange(t.Result); });

                        c.Response.StatusCode = 200;
                        c.Response.Content = infos.Filter(c);
                    }
                }
            };
        }

        async Task<XDocument> LoadXmlAsync(Uri url, int maxRedirect = 5)
        {
            using (var client = await HttpClient.CreateAsync(url, 5000, 10000))
            {
                var clientRequest = new HttpClientRequest
                {
                    Method = "GET",
                    Path = url.PathAndQuery
                };
                await client.SendRequestAsync(clientRequest);
                var response = await client.GetResponseAsync();
                if ((response.StatusCode == 301 || response.StatusCode == 302) && response.Headers.ContainsKey("location"))
                {
                    if (maxRedirect <= 0)
                    {
                        logger.Log(LogLevel.Error, $"While loading '{url}' got too many HTTP REDIRECT");
                        return null;
                    }
                    var redirectUrl = new Uri(url, response.Headers["location"]);
                    return await LoadXmlAsync(redirectUrl, maxRedirect - 1);
                }
                if (response.StatusCode == 200)
                    return XDocument.Load(response.InputStream);
                logger.Log(LogLevel.Error, $"While loading '{url}' got HTTP {response.StatusCode}");
            }
            return null;
        }

        static string GetTextFromHtml(string html)
        {
            // load the document using sgml reader
            var document = new XmlDocument();
            using (var sgmlReader = new Sgml.SgmlReader())
            {
                sgmlReader.CaseFolding = Sgml.CaseFolding.ToLower;
                sgmlReader.DocType = "HTML";
                sgmlReader.WhitespaceHandling = WhitespaceHandling.None;

                using (var sr = new StringReader(html))
                {
                    sgmlReader.InputStream = sr;
                    document.Load(sgmlReader);
                }
            }
            return document.InnerText;
        }

        static string GetImageFromHtml(string html)
        {
            // load the document using sgml reader
            var document = new XmlDocument();
            using (var sgmlReader = new Sgml.SgmlReader())
            {
                sgmlReader.CaseFolding = Sgml.CaseFolding.ToLower;
                sgmlReader.DocType = "HTML";
                sgmlReader.WhitespaceHandling = WhitespaceHandling.None;

                using (var sr = new StringReader(html))
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
            return imageUrl;
        }
    }
}
