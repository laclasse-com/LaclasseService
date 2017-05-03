// Cas.cs
// 
//  API for the CAS server SSO
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

using System;
using System.Xml;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Directory;

namespace Laclasse.Authentication
{
	public partial class CasView
	{
		public string service = "/";
		public string error;
		public string title;
		public string message;
	}

	public class Cas : HttpRouting
	{
		readonly string dbUrl;
		readonly Tickets tickets;
		readonly Sessions sessions;
		readonly Users users;
		readonly Etablissements etablissements;
		readonly string cookieName;

		public Cas(string dbUrl, Sessions sessions, Users users, Etablissements etablissements,
		           string cookieName, double ticketTimeout, JsonValue aafSsoSetup)
		{
			this.dbUrl = dbUrl;
			this.sessions = sessions;
			this.users = users;
			this.etablissements = etablissements;
			this.cookieName = cookieName;
			tickets = new Tickets(dbUrl, ticketTimeout);

			var agentCert = new X509Certificate2(Convert.FromBase64String(aafSsoSetup["agents"]["cert"]));
			var parentCert = new X509Certificate2(Convert.FromBase64String(aafSsoSetup["parents"]["cert"]));

			GetAsync["/login"] = async (p, c) =>
			{
				var user = await sessions.GetAuthenticatedUserAsync(c);
				if (user != null)
				{
					c.Response.StatusCode = 302;
					c.Response.Headers["content-type"] = "text/plain; charset=utf-8";

					if (c.Request.QueryString.ContainsKey("service"))
					{
						string service = c.Request.QueryString["service"];
						var ticket = await tickets.CreateAsync(c.Request.Cookies[cookieName]);
						if (service.IndexOf('?') > 0)
							service += "&ticket=" + ticket;
						else
							service += "?ticket=" + ticket;
						c.Response.Headers["location"] = service;
					}
					else
					{
						c.Response.StatusCode = 200;
						c.Response.Headers["content-type"] = "text/html; charset=utf-8";
						c.Response.Content = (new CasView {
							title = "Connexion réussie",
							message = @"
							<p>Vous vous &ecirc;tes authentifi&eacute;(e) aupr&egrave;s du Service Central d'Authentification</p>
							<p>Pour des raisons de s&eacute;curit&eacute;, veuillez vous d&eacute;connecter et fermer
							votre navigateur lorsque vous avez fini d'acc&eacute;der aux services authentifi&eacute;s</p>"
						}).TransformText();
					}
				}
				else
				{
					string service = "?";
					if (c.Request.QueryString.ContainsKey("service"))
						service = c.Request.QueryString["service"];

					c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "text/html; charset=utf-8";
					c.Response.Content = (new CasView { service = service }).TransformText();
				}
			};

			PostAsync["/login"] = async (p, c) =>
			{
				var formUrl = await c.Request.ReadAsStringAsync();
				Dictionary<string, string> formFields;
				Dictionary<string, List<string>> formArrayFields;
				HttpUtility.ParseFormUrlEncoded(formUrl, out formFields, out formArrayFields);
				formFields.RequireFields("username", "password");

				var uid = await users.CheckPasswordAsync(formFields["username"], formFields["password"]);
				if (uid == null)
				{
					c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "text/html; charset=utf-8";
					c.Response.Content = (new CasView {
						service = formFields.ContainsKey("service") ? formFields["service"] : "/",
						error = "Les informations transmises n'ont pas permis de vous authentifier."
					}).TransformText();
				}
				else
					// init the session and redirect to service
					await CasLoginAsync(c, uid, formFields.ContainsKey("service") ? formFields["service"] : null);
			};

			GetAsync["/logout"] = async (p, c) =>
			{
				string destination = c.Request.QueryString.ContainsKey("destination") ?
									  c.Request.QueryString["destination"] : "login";
				// delete the session
				if (c.Request.Cookies.ContainsKey(cookieName))
					await sessions.DeleteSessionAsync(c.Request.Cookies[cookieName]);

				// TODO: clean all cookies
				/*foreach (var cookie in c.Request.Cookies.Keys)
				{
					
				}*/

				c.Response.StatusCode = 302;
				c.Response.Headers["content-type"] = "text/plain; charset=utf-8";
				c.Response.Headers["set-cookie"] = cookieName + "=; Expires= " + (DateTime.Now - TimeSpan.FromDays(365)).ToString("R") + "; Path=/";
				c.Response.Headers["location"] = destination;
			};

			GetAsync["/serviceValidate"] = async (p, c) =>
			{
				c.Request.QueryString.RequireFields("ticket");
				if (!c.Request.QueryString.ContainsKey("ticket") && !c.Request.QueryString.ContainsKey("service"))
				{
					c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "text/xml; charset=\"UTF-8\"";
					c.Response.Content = ServiceResponseFailure(
						"INVALID_REQUEST", 
						$"serviceValidate require at least two parameters : ticket and service.");
					return;
				}

				var ticketId = c.Request.QueryString["ticket"];
				var session = await tickets.GetAsync(ticketId);
				if (session == null)
				{
					c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "text/xml; charset=\"UTF-8\"";
					c.Response.Content = ServiceResponseFailure(
						"INVALID_TICKET",
						$"Ticket {ticketId} is not recognized.");
				}
				else
				{
					await tickets.DeleteAsync(ticketId);
					var user = await sessions.GetSessionAsync(session);
					if (user == null)
					{
						c.Response.StatusCode = 200;
						c.Response.Headers["content-type"] = "text/xml; charset=\"UTF-8\"";
						c.Response.Content = ServiceResponseFailure(
							"INVALID_SESSION",
							$"Ticket {ticketId} has a timed out session.");
					}
					else
					{
						c.Response.StatusCode = 200;
						c.Response.Headers["content-type"] = "text/xml; charset=\"UTF-8\"";
						c.Response.Content = ServiceResponseSuccess(await GetUserSsoAttributesAsync(user));
					}
				}
			};

			PostAsync["/samlValidate"] = async (p, c) =>
			{
				if (!c.Request.QueryString.ContainsKey("TARGET"))
				{
					c.Response.StatusCode = 200;
					c.Response.Content = new XmlContent(SoapSamlResponseError(null, "unknown"));
					return;
				}
				var service = c.Request.QueryString["TARGET"];

				var samlRequest = await c.Request.ReadAsStringAsync();

				var doc = new XmlDocument();
				doc.PreserveWhitespace = true;
				doc.LoadXml(samlRequest);

				// find the ticket (Artifact)
				var ns = new XmlNamespaceManager(doc.NameTable);
				ns.AddNamespace("samlp", "urn:oasis:names:tc:SAML:1.0:protocol");
				var nodes = doc.DocumentElement.SelectNodes("//samlp:AssertionArtifact", ns);

				// if no artifact or multiples artifacts found or not SAML 1.0 protocol, stop here
				if (nodes.Count != 1) {
					Console.WriteLine("samlValidate AssertionArtifact not found");
					c.Response.StatusCode = 200;
					c.Response.Content = new XmlContent(SoapSamlResponseError(doc, service));
					return;
				}
				var ticketId = nodes[0].InnerText;
				Console.WriteLine($"Extracted Ticket: {ticketId}");

				var session = await tickets.GetAsync(ticketId);
				if (session == null)
				{
					Console.WriteLine($"samlValidate Ticket {ticketId} not found.");
					c.Response.StatusCode = 200;
					c.Response.Content = new XmlContent(SoapSamlResponseError(doc, service));
				}
				else
				{
					await tickets.DeleteAsync(ticketId);
					var user = await sessions.GetSessionAsync(session);
					if (user == null)
					{
						Console.WriteLine($"samlValidate Ticket {ticketId} has a timed out session.");
						c.Response.StatusCode = 200;
						c.Response.Content = new XmlContent(SoapSamlResponseError(doc, service));
					}
					else
					{
						// TODO: need the check if the service is accepted.
						// TODO: need to filter the user's attributes and the nameIdentifier

						// send SAML response
						c.Response.StatusCode = 200;
						c.Response.Content = new XmlContent(SoapSamlResponse(
							c.SelfURL(), doc, await GetUserSsoAttributesAsync(user), "user", service));
					}
				}
			};

			Get["/parentPortalIdp"] = (p, c) =>
			{
				var url = (string)aafSsoSetup["parents"]["url"];
				var uri = new Uri(new Uri(c.SelfURL()), new Uri("login", UriKind.Relative));
				var service = uri.AbsoluteUri;
				if (c.Request.QueryString.ContainsKey("service"))
					service = c.Request.QueryString["service"];
				var xml = SamlAuthnRequest(aafSsoSetup["parents"], service, c.SelfURL());

				string SAMLRequest;
				using (var memStream = new MemoryStream())
				{
					using (var compressionStream = new DeflateStream(memStream, CompressionMode.Compress))
					{
						var bytes = Encoding.UTF8.GetBytes(xml);
						compressionStream.Write(bytes, 0, bytes.Length);
					}
					SAMLRequest = Convert.ToBase64String(memStream.ToArray());
				}
				c.Response.StatusCode = 302;
				c.Response.Headers["location"] = url + ((url.IndexOf('?') > 0) ? "&" : "?") + "SAMLRequest=" +
					HttpUtility.UrlEncode(SAMLRequest) + "&RelayState=" + HttpUtility.UrlEncode(c.SelfURL());
			};

			Get["/agentPortalIdp"] = (p, c) =>
			{
				var url = (string)aafSsoSetup["agents"]["url"];
				var uri = new Uri(new Uri(c.SelfURL()), new Uri("login", UriKind.Relative));
				var service = uri.AbsoluteUri;
				if (c.Request.QueryString.ContainsKey("service"))
					service = c.Request.QueryString["service"];
				var xml = SamlAuthnRequest(aafSsoSetup["agents"], service, c.SelfURL());

				string SAMLRequest;
				using (var memStream = new MemoryStream())
				{
					using (var compressionStream = new DeflateStream(memStream, CompressionMode.Compress))
					{
						var bytes = Encoding.UTF8.GetBytes(xml);
						compressionStream.Write(bytes, 0, bytes.Length);
					}
					SAMLRequest = Convert.ToBase64String(memStream.ToArray());
				}
				c.Response.StatusCode = 302;
				c.Response.Headers["location"] = url + ((url.IndexOf('?') > 0) ? "&" : "?") + "SAMLRequest=" +
					HttpUtility.UrlEncode(SAMLRequest) + "&RelayState=" + HttpUtility.UrlEncode(c.SelfURL());
			};

			PostAsync["/agentPortalIdp"] = async (p, c) =>
			{
				var formUrl = await c.Request.ReadAsStringAsync();
				Dictionary<string, string> formFields;
				Dictionary<string, List<string>> formArrayFields;
				HttpUtility.ParseFormUrlEncoded(formUrl, out formFields, out formArrayFields);

				var SAMLResponse = formFields["SAMLResponse"];
				SAMLResponse = Encoding.UTF8.GetString(Convert.FromBase64String(SAMLResponse));

				var dom = new XmlDocument();
				dom.PreserveWhitespace = true;
				dom.LoadXml(SAMLResponse);

				var id = dom.DocumentElement.GetAttribute("InResponseTo");
				var decoded = Hex2String(id.Substring(1));
				var pos = decoded.IndexOf(':');
				var service = (pos > 0) ? decoded.Substring(pos + 1) : null;

				// verify the Digital Signature
				var refDom = VerifySignedXml(dom, agentCert);
				if (refDom == null) {
					c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "text/html; charset=utf-8";
					c.Response.Content = (new CasView
					{
						error = @"
							Les données d'authentification de l'Académie ne sont pas valides et ne nous permettent pas
							de vous identifier. Rééssayez plus tard ou contacter votre administrateur."
					}).TransformText();
					return;
				}

				// search for the ctemail attribut
				var ns = new XmlNamespaceManager(refDom.NameTable);
				ns.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
				var node = refDom.DocumentElement.SelectSingleNode("//saml:Assertion/saml:AttributeStatement/saml:Attribute[@Name=\"ctemail\"]/saml:AttributeValue", ns);
				if (node == null)
				{
					c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "text/html; charset=utf-8";
					c.Response.Content = (new CasView
					{
						error = @"
							Les données d'authentification de l'Académie ne nous permettent pas de vous identifier. 
							Rééssayez plus tard ou contacter votre administrateur."
					}).TransformText();
					return;
				}
				var ctemail = node.InnerText;
				Console.WriteLine($"agentPortalIdp ctemail: {ctemail}, node: {node}");

				// find the corresponding user
				var queryFields = new Dictionary<string,List<string>>();
				queryFields["emails.type"] = new List<string>(new string[] { "Academique" });
				queryFields["emails.adresse"] = new List<string>(new string[] { ctemail });
				var userResult = (await users.SearchUserAsync(queryFields)).Data.SingleOrDefault();
				//Console.WriteLine($"Found user: {userResult}");

				if (userResult == null)
				{
					c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "text/html; charset=utf-8";
					c.Response.Content = (new CasView
					{
						error = @"
							Compte utilisateur non trouvé dans laclasse.com. Votre compte doit être provisionné
							dans laclasse.com avant de pouvoir vous connecter en utilisant votre compte Académique."
					}).TransformText();
					return;
				}

				// init the session and redirect to service
				await CasLoginAsync(c, userResult["id_ent"], service);
			};

			PostAsync["/parentPortalIdp"] = async (p, c) =>
			{
				var formUrl = await c.Request.ReadAsStringAsync();
				Dictionary<string, string> formFields;
				Dictionary<string, List<string>> formArrayFields;
				HttpUtility.ParseFormUrlEncoded(formUrl, out formFields, out formArrayFields);

				var SAMLResponse = formFields["SAMLResponse"];
				SAMLResponse = Encoding.UTF8.GetString(Convert.FromBase64String(SAMLResponse));

				var dom = new XmlDocument();
				dom.PreserveWhitespace = true;
				dom.LoadXml(SAMLResponse);

				var id = dom.DocumentElement.GetAttribute("InResponseTo");
				var decoded = Hex2String(id.Substring(1));
				var pos = decoded.IndexOf(':');
				var service = (pos > 0) ? decoded.Substring(pos + 1) : null;

				// verify the Digital Signature
				var refDom = VerifySignedXml(dom, parentCert);
				if (refDom == null)
				{
					c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "text/html; charset=utf-8";
					c.Response.Content = (new CasView
					{
						error = @"
							Les données d'authentification de l'Académie ne sont pas valides et ne nous permettent pas
							de vous identifier. Rééssayez plus tard ou contacter votre administrateur."
					}).TransformText();
					return;
				}

				// search for the FrEduVecteur attribut
				var ns = new XmlNamespaceManager(refDom.NameTable);
				ns.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
				var nodes = refDom.DocumentElement.SelectNodes("//saml:Attribute[@Name=\"FrEduVecteur\"]/saml:AttributeValue", ns);
				if ((nodes == null) || (nodes.Count < 1))
				{
					c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "text/html; charset=utf-8";
					c.Response.Content = (new CasView
					{
						error = @"
							Les données d'authentification de l'Académie ne nous permettent pas de vous identifier. 
							Rééssayez plus tard ou contacter votre administrateur."
					}).TransformText();
					return;
				}

				JsonValue userResult = null;
				foreach (XmlNode node in nodes)
				{
					userResult = await SearchFrEduVecteurAsync(node.InnerText);
					if (userResult != null)
						break;
				}

				if (userResult == null)
				{
					c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "text/html; charset=utf-8";
					c.Response.Content = (new CasView
					{
						error = @"
							Compte utilisateur non trouvé dans laclasse.com. Votre compte doit être provisionné
							dans laclasse.com avant de pouvoir vous connecter en utilisant votre compte Académique."
					}).TransformText();
					return;
				}

				// init the session and redirect to service
				await CasLoginAsync(c, userResult["id_ent"], service);
			};
		}

		static Dictionary<string, string> ProfilIdToSdet3 = new Dictionary<string, string>
		{
			["CPE"] = "National_5",
			["AED"] = "National_5",
			["EVS"] = "National_5",
			["ENS"] = "National_3",
			["ELV"] = "National_1",
			["ETA"] = "National_6",
			["ACA"] = "National_7",
			["DIR"] = "National_4",
			["TUT"] = "National_2",
			["COL"] = "National_4",
			["DOC"] = "National_3"
		};

		Dictionary<string, string> UserToSsoAttributes(JsonValue user)
		{
			// TODO: add ENTEleveClasses and ENTEleveNivFormation

			string ENTPersonStructRattachRNE = null;
			string ENTPersonProfils = null;
			string categories = null;
			foreach (var p in (JsonArray)user["profils"])
			{
				if ((bool)p["actif"])
				{
					ENTPersonStructRattachRNE = p["etablissement_code_uai"];
					if (ProfilIdToSdet3.ContainsKey(p["profil_id"]))
						categories = ProfilIdToSdet3[p["profil_id"]];
					if (p["profil_id"] == "ELV")
					{
					}
				}

				if (ENTPersonProfils == null)
					ENTPersonProfils = "";
				else
					ENTPersonProfils += ",";
				ENTPersonProfils += p["profil_id"] + ":" + p["etablissement_code_uai"];
			}

			var ENTPersonRoles = "";
			foreach (var r in (JsonArray)user["roles"])
			{
				if (ENTPersonRoles != "")
					ENTPersonRoles += ",";
				ENTPersonRoles += r["role_id"] + ":" + r["etablissement_code_uai"] +
					":" + r["priority"] + ":" + r["libelle"] + ":" +
					r["etablissement_nom"];
			}

			return new Dictionary<string, string>
			{
				["uid"] = user["id_ent"],
				["user"] = user["id_ent"],
				["login"] = user["login"],
				["nom"] = user["nom"],
				["prenom"] = user["prenom"],
				["dateNaissance"] = (user["date_naissance"] == null) ? null : DateTime.Parse(user["date_naissance"]).ToString("yyyy-MM-dd"),
				["codePostal"] = (user["code_postal"] == null) ? null : (string)user["code_postal"],
				["ENTPersonProfils"] = ENTPersonProfils,
				["ENTPersonStructRattach"] = ENTPersonStructRattachRNE,
				["ENTPersonStructRattachRNE"] = ENTPersonStructRattachRNE,
				["ENTPersonRoles"] = ENTPersonRoles,
				["categories"] = categories,
				["LaclasseNom"] = user["nom"],
				["LaclassePrenom"] = user["prenom"]
			};
		}

		async Task CasLoginAsync(HttpContext c, string uid, string service)
		{
			var sessionId = await sessions.CreateSessionAsync(uid);

			c.Response.StatusCode = 302;
			c.Response.Headers["content-type"] = "text/plain; charset=utf-8";
			c.Response.Headers["set-cookie"] = cookieName + "=" + sessionId + "; Path=/";

			if (service != null)
			{
				var ticket = await tickets.CreateAsync(sessionId);
				if (service.IndexOf('?') > 0)
					service += "&ticket=" + ticket;
				else
					service += "?ticket=" + ticket;
				Console.WriteLine($"Location: '{service}'");
				c.Response.Headers["location"] = service;
			}
			else
				// redirect to /sso/login
				c.Response.Headers["location"] = "login";
		}

		public async Task<Dictionary<string,string>> GetUserSsoAttributesAsync(string uid)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
			{
				return UserToSsoAttributes(await users.GetUserAsync(uid));
			}
		}

		public string ServiceResponseFailure(string code, string message)
		{
			var dom = new XmlDocument();
			var cas = "http://www.yale.edu/tp/cas";

			var ns = new XmlNamespaceManager(dom.NameTable);
			ns.AddNamespace("cas", cas);

			var serviceResponse = dom.CreateElement("cas:serviceResponse", cas);
			serviceResponse.SetAttribute("xmlns:cas", cas);
			dom.AppendChild(serviceResponse);

			var authenticationFailure = dom.CreateElement("cas:authenticationFailure", cas);
			authenticationFailure.SetAttribute("code", code);
			authenticationFailure.InnerText = message;
			serviceResponse.AppendChild(authenticationFailure);

			using (var stringWriter = new StringWriter())
			{
				var settings = new XmlWriterSettings();
				settings.OmitXmlDeclaration = true;
				settings.Encoding = Encoding.UTF8;
				settings.Indent = true;
				using (var xmlTextWriter = XmlWriter.Create(stringWriter, settings))
				{
					dom.Save(xmlTextWriter);
					return stringWriter.ToString();
				}
			}
		}

		public string ServiceResponseSuccess(Dictionary<string, string> attributes)
		{
			var dom = new XmlDocument();
			var cas = "http://www.yale.edu/tp/cas";

			var ns = new XmlNamespaceManager(dom.NameTable);
			ns.AddNamespace("cas", cas);

			var serviceResponse = dom.CreateElement("cas:serviceResponse", cas);
			serviceResponse.SetAttribute("xmlns:cas", cas);
			dom.AppendChild(serviceResponse);

			var authenticationSuccess = dom.CreateElement("cas:authenticationSuccess", cas);
			serviceResponse.AppendChild(authenticationSuccess);

			var casUser = dom.CreateElement("cas:user", cas);
			casUser.InnerText = attributes["user"];
			authenticationSuccess.AppendChild(casUser);

			var casAttributes = dom.CreateElement("cas:attributes", cas);
			authenticationSuccess.AppendChild(casAttributes);

			foreach (var attribute in attributes.Keys)
			{
				var casAttribute = dom.CreateElement("cas:"+attribute, cas);
				casAttribute.InnerText = attributes[attribute];
				casAttributes.AppendChild(casAttribute);
				authenticationSuccess.AppendChild(casAttribute);
			}

			using (var stringWriter = new StringWriter())
			{
				var settings = new XmlWriterSettings();
				settings.OmitXmlDeclaration = true;
				settings.Encoding = Encoding.UTF8;
				settings.Indent = true;
				using (var xmlTextWriter = XmlWriter.Create(stringWriter, settings))
				{
					dom.Save(xmlTextWriter);
					return stringWriter.ToString();
				}
			}
		}

		string String2Hex(string str)
		{
			var sb = new StringBuilder();
			foreach (var b in Encoding.UTF8.GetBytes(str))
			{
				sb.Append(b.ToString("X"));
			}
			return sb.ToString();
		}

		string Hex2String(string str)
		{
			string res;
			using (var memStream = new MemoryStream())
			{
				while (str.Length > 0)
				{
					memStream.WriteByte(byte.Parse(str.Substring(0, 2), System.Globalization.NumberStyles.HexNumber));
					str = str.Substring(2);
				}
				res = Encoding.UTF8.GetString(memStream.ToArray());
			}
			return res;
		}

		string SamlAuthnRequest(JsonValue setup, string service, string assertionConsumerServiceURL)
		{
			var dom = new XmlDocument();
			var samlp = "urn:oasis:names:tc:SAML:2.0:protocol";
			var saml = "urn:oasis:names:tc:SAML:2.0:assertion";

			var id = "_" + String2Hex(StringExt.RandomString(10) + ":" + service);

			var ns = new XmlNamespaceManager(dom.NameTable);
			ns.AddNamespace("samlp", samlp);
			ns.AddNamespace("saml", saml);

			var AuthnRequest = dom.CreateElement("cas:AuthnRequest", samlp);
			AuthnRequest.SetAttribute("xmlns:samlp", samlp);
			AuthnRequest.SetAttribute("xmlns:saml", saml);
			AuthnRequest.SetAttribute("ID", id);
			AuthnRequest.SetAttribute("Version", "2.0");
			AuthnRequest.SetAttribute("IssueInstant", DateTime.UtcNow.ToString("s")+"Z");
			AuthnRequest.SetAttribute("Destination", setup["url"]);
			AuthnRequest.SetAttribute("AssertionConsumerServiceURL", assertionConsumerServiceURL);
			AuthnRequest.SetAttribute("ProtocolBinding", "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST");
			dom.AppendChild(AuthnRequest);

			var Issuer = dom.CreateElement("saml:Issuer", saml);
			Issuer.InnerText = setup["issuer"];
			AuthnRequest.AppendChild(Issuer);

			var NameIDPolicy = dom.CreateElement("samlp:NameIDPolicy", samlp);
			NameIDPolicy.SetAttribute("Format", "urn:oasis:names:tc:SAML:2.0:nameid-format:transient");
			NameIDPolicy.SetAttribute("AllowCreate", "true");
			AuthnRequest.AppendChild(NameIDPolicy);

			string res;
			using (var stringWriter = new StringWriter())
			{
				var settings = new XmlWriterSettings();
				settings.OmitXmlDeclaration = true;
				settings.Encoding = Encoding.UTF8;
				settings.Indent = true;
				using (var xmlTextWriter = XmlWriter.Create(stringWriter, settings))
				{
					dom.Save(xmlTextWriter);
					res = stringWriter.ToString();
				}
			}
			return res;
		}


		XmlDocument SamlResponseError1(string inResponseTo, string recipient, string errorCode = "samlp:Responder")
		{
			var samlp = "urn:oasis:names:tc:SAML:1.0:protocol";
			var saml = "urn:oasis:names:tc:SAML:1.0:assertion";

			var issueInstant = DateTime.UtcNow.ToString("s") + "Z";

			var doc = new XmlDocument();

			var ns = new XmlNamespaceManager(doc.NameTable);
			ns.AddNamespace("samlp", samlp);
			ns.AddNamespace("saml", saml);

			var response = doc.CreateElement("samlp:Response", samlp);
			response.SetAttribute("xmlns:samlp", samlp);
			response.SetAttribute("xmlns:saml", saml);
			response.SetAttribute("MajorVersion", "1");
			response.SetAttribute("MinorVersion", "1");
			response.SetAttribute("ResponseID", StringExt.RandomString(16));
			response.SetAttribute("InResponseTo", inResponseTo);
			response.SetAttribute("IssueInstant", issueInstant);
			response.SetAttribute("Recipient", recipient);
			doc.AppendChild(response);

			var status = doc.CreateElement("samlp:Status", samlp);
			response.AppendChild(status);

			var statusCode = doc.CreateElement("samlp:StatusCode", samlp);
			statusCode.SetAttribute("Value", errorCode);
			status.AppendChild(statusCode);

			return doc;
		}

		XmlDocument SamlResponse1(Dictionary<string,string> attributes, string inResponseTo, string issuer,
		                          string nameIdentifier, string recipient)
		{
			var samlp = "urn:oasis:names:tc:SAML:1.0:protocol";
			var saml = "urn:oasis:names:tc:SAML:1.0:assertion";
			var xs = "http://www.w3.org/2001/XMLSchema";
			var xsi = "http://www.w3.org/2001/XMLSchema-instance";

			var issueInstant = DateTime.UtcNow.ToString("s") + "Z";


			var notBefore = (DateTime.UtcNow - TimeSpan.FromHours(1)).ToString("s") + "Z";
			var notOnOrAfter = (DateTime.UtcNow + TimeSpan.FromHours(1)).ToString("s") + "Z";

			var doc = new XmlDocument();

			var ns = new XmlNamespaceManager(doc.NameTable);
			ns.AddNamespace("samlp", samlp);
			ns.AddNamespace("saml", saml);
			ns.AddNamespace("xs", xs);
			ns.AddNamespace("xsi", xsi);

			var response = doc.CreateElement("samlp:Response", samlp);
			response.SetAttribute("xmlns:samlp", samlp);
			response.SetAttribute("xmlns:saml", saml);
			response.SetAttribute("MajorVersion", "1");
			response.SetAttribute("MinorVersion", "1");
			response.SetAttribute("ResponseID", StringExt.RandomString(16));
			response.SetAttribute("InResponseTo", inResponseTo);
			response.SetAttribute("IssueInstant", issueInstant);
			response.SetAttribute("Recipient", recipient);
			doc.AppendChild(response);

			var status = doc.CreateElement("samlp:Status", samlp);
			response.AppendChild(status);

			var statusCode = doc.CreateElement("samlp:StatusCode", samlp);
			statusCode.SetAttribute("Value", "samlp: Success");
			status.AppendChild(statusCode);

			var assertion = doc.CreateElement("saml:Assertion", saml);
			assertion.SetAttribute("MajorVersion", "1");
			assertion.SetAttribute("MinorVersion", "1");
			assertion.SetAttribute("AssertionID", StringExt.RandomString(16));
			assertion.SetAttribute("Issuer", issuer);
			assertion.SetAttribute("IssueInstant", issueInstant);
			response.AppendChild(assertion);

			var conditions = doc.CreateElement("saml:Conditions", saml);
			conditions.SetAttribute("NotBefore", notBefore);
			conditions.SetAttribute("NotOnOrAfter", notOnOrAfter);
			assertion.AppendChild(conditions);

			var audienceRestrictionCondition = doc.CreateElement("AudienceRestrictionCondition", saml);
			conditions.AppendChild(audienceRestrictionCondition);

			var audience = doc.CreateElement("Audience", saml);
			audience.InnerText = recipient;
			audienceRestrictionCondition.AppendChild(audience);

			var attributeStatement = doc.CreateElement("AttributeStatement", saml);
			assertion.AppendChild(attributeStatement);

			var subject = doc.CreateElement("Subject", saml);
			attributeStatement.AppendChild(subject);	
			var nameIdentifierNode = doc.CreateElement("NameIdentifier", saml);
			nameIdentifierNode.InnerText = nameIdentifier;
			subject.AppendChild(nameIdentifierNode);
			var subjectConfirmation = doc.CreateElement("SubjectConfirmation", saml);
			subject.AppendChild(subjectConfirmation);
			var confirmationMethod = doc.CreateElement("ConfirmationMethod", saml);
			confirmationMethod.InnerText = "urn:oasis:names:tc:SAML:1.0:cm:artifact";
			subjectConfirmation.AppendChild(confirmationMethod);

			foreach (KeyValuePair<string, string> keyValue in attributes)
			{
				var attribute = doc.CreateElement("Attribute", saml);
				attributeStatement.AppendChild(attribute);

				attribute.SetAttribute("AttributeName", keyValue.Key);
				attribute.SetAttribute("AttributeNamespace", issuer);
				attributeStatement.AppendChild(attribute);

				var attributeValue = doc.CreateElement("AttributeValue", saml);
				attributeValue.InnerText = keyValue.Value;
				attribute.AppendChild(attributeValue);
			}

			var authenticationStatement = doc.CreateElement("AuthenticationStatement", saml);
			// should be the time where the user really login
			authenticationStatement.SetAttribute("AuthenticationInstant", issueInstant);
			// should change is the user use something else like AAF-SSO
			authenticationStatement.SetAttribute("AuthenticationMethod", "urn:oasis:names:tc:SAML:1.0:am:password");
			assertion.AppendChild(authenticationStatement);

			subject = doc.CreateElement("Subject", saml);
			authenticationStatement.AppendChild(subject);
			nameIdentifierNode = doc.CreateElement("NameIdentifier", saml);
			nameIdentifierNode.InnerText = nameIdentifier;
			subject.AppendChild(nameIdentifierNode);
			subjectConfirmation = doc.CreateElement("SubjectConfirmation", saml);
			subject.AppendChild(subjectConfirmation);
			confirmationMethod = doc.CreateElement("ConfirmationMethod", saml);
			confirmationMethod.InnerText = "urn:oasis:names:tc:SAML:1.0:cm:artifact";
			subjectConfirmation.AppendChild(confirmationMethod);

			return doc;
		}

		XmlDocument GenerateSoapEnvelope(XmlDocument bodyContent)
		{
			var SOAPENV = "http://schemas.xmlsoap.org/soap/envelope/";

			var doc = new XmlDocument();

			var ns = new XmlNamespaceManager(doc.NameTable);
			ns.AddNamespace("SOAP-ENV", SOAPENV);

			var envelope = doc.CreateElement("SOAP-ENV:Envelope", SOAPENV);
			//envelope.SetAttribute("xmlns:SOAP-ENV", "http://www.w3.org/2000/xmlns/", SOAPENV);
			doc.AppendChild(envelope);

			var header = doc.CreateElement("SOAP-ENV:Header", SOAPENV);
			envelope.AppendChild(header);

			var body = doc.CreateElement("SOAP-ENV:Body", SOAPENV);
			envelope.AppendChild(body);

			body.AppendChild(doc.ImportNode(bodyContent.DocumentElement, true));
			return doc;
		}

		XmlDocument SoapSamlResponse(string selfUrl, XmlDocument request, Dictionary<string, string> attributes,
		                             string nameIdentifier, string recipient)
		{
			var SOAPENV = "http://schemas.xmlsoap.org/soap/envelope/";
			var ns = new XmlNamespaceManager(request.NameTable);
			ns.AddNamespace("SOAP-ENV", SOAPENV);

			string requestID = request.SelectSingleNode("//SOAP-ENV:Body/*", ns).Attributes["RequestID"].Value;

			var samlResponse = SamlResponse1(attributes, requestID, selfUrl, nameIdentifier, recipient);
			var soapSamlResponse = GenerateSoapEnvelope(samlResponse);

			return soapSamlResponse;
		}

		XmlDocument SoapSamlResponseError(XmlDocument request, string recipient, string errorCode = "samlp:Responder")
		{
			var SOAPENV = "http://schemas.xmlsoap.org/soap/envelope/";
			var ns = new XmlNamespaceManager(request.NameTable);
			ns.AddNamespace("SOAP-ENV", SOAPENV);

			var requestID = "unknown";
			if (request != null)
				requestID = request.SelectSingleNode("//SOAP-ENV:Body/*", ns).Attributes["RequestID"].Value;
			var samlResponse = SamlResponseError1(requestID, recipient, errorCode);
			return GenerateSoapEnvelope(samlResponse);
		}

		XmlDocument VerifySignedXml(XmlDocument doc, X509Certificate2 cert)
		{
			var signedInfoDoc = VerifySignature(doc, cert);
			if (signedInfoDoc == null)
				return null;
			return VerifyDigest(doc, signedInfoDoc);
		}

		/// <summary>
		/// Verifies the digest of an XML Document. If the digest
		/// </summary>
		/// <returns>
		/// An XML Document which correspond to the digested part of the given document.
		/// Or null if the digest is not present or not valid
		/// </returns>
		/// <param name="dom">The XML document</param>
		/// <param name="signedInfoDoc">The XML document which contains the Digest</param>
		XmlDocument VerifyDigest(XmlDocument dom, XmlDocument signedInfoDoc)
		{
			var ds = "http://www.w3.org/2000/09/xmldsig#";

			// get the digest value for the signature
			var nodeList = signedInfoDoc.DocumentElement.GetElementsByTagName("DigestValue", ds);
			if (nodeList.Count != 1)
				return null;
			var digestValue = nodeList[0].InnerText;

			// find the reference part ID which is used by the digest
			nodeList = signedInfoDoc.DocumentElement.GetElementsByTagName("Reference", ds);
			if (nodeList.Count != 1)
				return null;	
			var referenceId = nodeList[0].Attributes["URI"].Value;
			// remove the #
			referenceId = referenceId.Substring(1);

			// get the reference node
			var refNode = dom.DocumentElement.SelectSingleNode($"//*[@ID='{referenceId}']");
			if (refNode == null)
				return null;

			// create a document only with the reference part
			var refNodeDoc = new XmlDocument();
			refNodeDoc.PreserveWhitespace = true;
			refNodeDoc.LoadXml(refNode.OuterXml);

			var ns = new XmlNamespaceManager(refNodeDoc.NameTable);
			ns.AddNamespace("ds", ds);

			// remove signature
			var signature = refNodeDoc.DocumentElement.SelectSingleNode("//ds:Signature", ns);
			if (signature != null)
				signature.ParentNode.RemoveChild(signature);

			// generate the SHA1 signature
			string localDigestValue;
			using (var memStream = new MemoryStream())
			{
				var settings = new XmlWriterSettings();
				settings.OmitXmlDeclaration = true;
				// use UTF-8 but without the BOM (3 bytes at the beginning which give the byte order)
				settings.Encoding = new UTF8Encoding(false);
				using (var xmlTextWriter = XmlWriter.Create(memStream, settings))
				{
					refNodeDoc.Save(xmlTextWriter);
				}
				memStream.Seek(0, SeekOrigin.Begin);
				var sha1 = SHA1.Create();
				localDigestValue = Convert.ToBase64String(sha1.ComputeHash(memStream));
			}
			return (localDigestValue == digestValue) ? refNodeDoc : null;
		}

		/// <summary>
		/// Verifies the digital signature of the XML Document.
		/// </summary>
		/// <returns><c>true</c>, if signature was verifyed, <c>false</c> otherwise.</returns>
		/// <param name="doc">Document.</param>
		/// <param name="cert">Cert.</param>
		XmlDocument VerifySignature(XmlDocument doc, X509Certificate2 cert)
		{
			var ns = new XmlNamespaceManager(doc.NameTable);
			ns.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

			// find the signature
			var signature = doc.DocumentElement.SelectSingleNode("//ds:Signature", ns);
			// if no signature or multiples signatures found, stop here
			if (signature == null)
				return null;

			// create a document only with the signature part
			var signatureDoc = new XmlDocument();
			signatureDoc.PreserveWhitespace = true;
			signatureDoc.LoadXml(signature.OuterXml);

			// get the signature value
			var node = signatureDoc.DocumentElement.SelectSingleNode("//ds:SignatureValue", ns);
			if (node == null)
				return null;
			var signatureValue = Convert.FromBase64String(node.InnerText);

			// get the signedInfo part
			var signedInfo = signatureDoc.DocumentElement.SelectSingleNode("//ds:SignedInfo", ns);
			// if no signedInfo or multiples signedInfo found, stop here
			if (signedInfo == null)
				return null;

			// create a document only with the signedInfo part
			var signedInfoDoc = new XmlDocument();
			signedInfoDoc.PreserveWhitespace = true;
			signedInfoDoc.LoadXml(signedInfo.OuterXml);

			byte[] signedDoc;
			using (var memStream = new MemoryStream())
			{
				var settings = new XmlWriterSettings();
				settings.OmitXmlDeclaration = true;
				// use UTF-8 but without the BOM (3 bytes at the beginning which give the byte order)
				settings.Encoding = new UTF8Encoding(false);
				using (var xmlTextWriter = XmlWriter.Create(memStream, settings))
				{
					signedInfoDoc.Save(xmlTextWriter);
				}
				signedDoc = memStream.ToArray();
			}

			// check the signedInfo part signature using RSA key and SHA1
			var rsa = (RSACryptoServiceProvider)cert.PublicKey.Key;
			if (rsa.VerifyData(signedDoc, CryptoConfig.MapNameToOID("SHA1"), signatureValue))
				return signedInfoDoc;
			else
				return null;
		}

		async Task<JsonValue> SearchFrEduVecteurAsync(string FrEduVecteur)
		{
			Console.WriteLine($"SearchFrEduVecteurAsync({FrEduVecteur})");
			var tab = FrEduVecteur.Split('|');
			if (tab.Length != 5)
				return null;
			
			var type = tab[0];
			var lastname = tab[1];
			var firstname = tab[2];
			var id_sconet = tab[3];
			var uai = tab[4];

			// if parents
			if ((type == "1") || (type == "2")) 
			{
				var etabId = await etablissements.GetEtablissementIdAsync(uai);
				if (etabId == -1)
					return null;

				// search by 'nom', 'prenom' and etablissement 'uai'
				var queryFields = new Dictionary<string, List<string>>();
				queryFields["nom"] = new List<string>(new string[] { lastname });
				queryFields["prenom"] = new List<string>(new string[] { firstname });
				queryFields["profils.etablissement_id"] = new List<string>(new string[] { etabId.ToString() });
				queryFields["profils.profil_id"] = new List<string>(new string[] { "TUT" });
				var usersResult = (await users.SearchUserAsync(queryFields)).Data;
				if (usersResult.Count == 1)
					return usersResult[0];

				// seach find the corresponding student with the 'id_sconet'
				foreach (var user in usersResult)
				{
					foreach (var child in (JsonArray)user["enfants"])
					{
						if (child["id_sconet"] == int.Parse(id_sconet))
							return user;
					}
				}
			}
			// if student
			else
			{
				// seach find the corresponding user
				var queryFields = new Dictionary<string, List<string>>();
				queryFields["id_sconet"] = new List<string>(new string[] { id_sconet });
				var usersResult = (await users.SearchUserAsync(queryFields)).Data;
				if (usersResult.Count == 1)
					return usersResult[0];

				var etabId = await etablissements.GetEtablissementIdAsync(uai);
				if (etabId == -1)
					return null;

				queryFields = new Dictionary<string, List<string>>();
				queryFields["nom"] = new List<string>(new string[] { lastname });
				queryFields["prenom"] = new List<string>(new string[] { firstname });
				queryFields["profils.etablissement_id"] = new List<string>(new string[] { etabId.ToString() });
				queryFields["profils.profil_id"] = new List<string>(new string[] { "ELV" });
				usersResult = (await users.SearchUserAsync(queryFields)).Data;
				if (usersResult.Count == 1)
					return usersResult[0];
			}
			return null;
		}
	}
}
