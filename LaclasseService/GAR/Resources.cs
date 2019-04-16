// GAR.cs
// 
//  Handle GAR media center resources listing API. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2019 Metropole de Lyon
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
using System.Linq;
using System.Xml.Linq;
using Net = System.Net;
using System.Net.Security;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.GAR
{
    public class Resources: HttpRouting
    {
        GARSetup garSetup;

        public Resources(GARSetup garSetup, Logger logger)
        {
            this.garSetup = garSetup;

            X509Certificate2 listeRessourcesCert = null;
            // the certificate MUST contains the RSA private key
            if (garSetup.listeRessourcesCert != null)
                listeRessourcesCert = new X509Certificate2(Convert.FromBase64String(garSetup.listeRessourcesCert));
                
            BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

            GetAsync["/structures/{id}/resources"] = async (p, c) =>
            {
                XElement doc = null;
                Net.HttpWebRequest request = (Net.HttpWebRequest)Net.WebRequest.Create(garSetup.listeRessourcesUrl);
                request.PreAuthenticate = true;
                request.AllowAutoRedirect = true;
                // if localhost, allow invalid certificate
                if (request.Host == "localhost")
                    request.ServerCertificateValidationCallback = (obj, certificate, chain, errors) => true;
                if (listeRessourcesCert != null)
                    request.ClientCertificates.Add(listeRessourcesCert);

                try
                {
                    using (var response = (Net.HttpWebResponse)await request.GetResponseAsync())
                    {
                        using (Stream dataStream = response.GetResponseStream())
                        {
                            doc = XElement.Load(dataStream);
                        }
                    }
                }
                catch (Net.WebException e)
                {
                    if (e.InnerException is System.Security.Authentication.AuthenticationException)
                        logger.Log(LogLevel.Error, "Authentication failed for GAR listeRessources API");
                    else
                        logger.Log(LogLevel.Error, "GAR listsRessouces API fails: " + e);
                }
                if (doc != null)
                {
                    // convert to JSON
                    var resources = new JsonArray();
                    foreach (XElement element in (from node in doc.Elements() where node.Name == "ressource" select node))
                    {
                        var resource = new JsonObject();
                        resources.Add(resource);
                        ConvertNodes(resource, element, "idRessource", "idType", "nomRessource",
                            "idEditeur", "nomEditeur", "urlVignette", "urlAccesRessource",
                            "nomSourceEtiquetteGar", "distributeurTech", "validateurTech");
                        ConvertMultiNodes(resource, element, "typePresentation", "typologieDocument",
                            "niveauEducatif", "domaineEnseignement");
                    }
                    c.Response.StatusCode = 200;
                    c.Response.Content = resources;
                }
            };
        }

        static void ConvertNodes(JsonObject json, XElement node, params string[] names)
        {
            foreach (var name in names)
            {
                var element = node.Descendants().SingleOrDefault((arg) => arg.Name == name);
                if (element != null)
                    json[name] = element.Value;
            }
        }


        static void ConvertMultiNodes(JsonObject json, XElement node, params string[] names)
        {
            foreach (var name in names)
            {
                var nodes = node.Elements().Where((arg) => arg.Name == name);
                if (nodes.Any())
                {
                    var jsonArray = new JsonArray();
                    json[name] = jsonArray;
                    foreach (XElement childNode in nodes)
                    {
                        var jsonObject = new JsonObject();
                        jsonArray.Add(jsonObject);
                        foreach (XElement childSubNode in childNode.Elements())
                        {
                            jsonObject[childSubNode.Name.LocalName] = childSubNode.Value;
                        }
                    }
                }
            }
        }
    }
}
