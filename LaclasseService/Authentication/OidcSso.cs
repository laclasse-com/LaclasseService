/*using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Directory;

namespace Laclasse.Authentication
{
	public class OidcSso: HttpRouting
	{
		public OidcSso(OIDCSSOSetup[] setup, Users users, Cas cas)
		{
			Get["/login"] = (p, c) =>
			{
				if (c.Request.QueryString.ContainsKey("sso"))
				{
					var name = c.Request.QueryString["sso"];
					var sso = setup.FirstOrDefault((arg) => arg.name == name);
					if (sso != null)
					{
						var redirect_uri = new Uri(new Uri(c.SelfURL()), new Uri("./", UriKind.Relative));
						c.Response.Headers["location"] = sso.authorizeUrl + "?" +
							HttpUtility.QueryStringToString(new Dictionary<string, string>
							{
								["response_type"] = "code",
								["scope"] = "openid email profile crown",
								["client_id"] = sso.clientId,
								["state"] = "12345",
								["redirect_uri"] = redirect_uri + "?sso=" + HttpUtility.UrlEncode(sso.name)
							});

						c.Response.StatusCode = 302;
						c.Response.Content = "";
					}
				}
			};

			GetAsync["/"] = async (p, c) =>
			{
				Console.WriteLine($"GOT /");

				if (c.Request.QueryString.ContainsKey("sso") &&
				    c.Request.QueryString.ContainsKey("code"))
				{
					var name = c.Request.QueryString["sso"];
					var code = c.Request.QueryString["code"];
					var sso = setup.FirstOrDefault((arg) => arg.name == name);
					if (sso != null)
					{
						JsonValue token;
						var tokenUri = new Uri(sso.tokenUrl);
						using (var client = await HttpClient.CreateAsync(tokenUri))
						{
							var request = new HttpClientRequest();
							request.Method = "POST";
							request.Path = tokenUri.PathAndQuery;
							request.Headers["content-type"] = "application/x-www-form-urlencoded";
							request.Headers["authorization"] = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sso.clientId + ":" + sso.password));

							request.Content = HttpUtility.QueryStringToString(new Dictionary<string, string> {
								["grant_type"] = "authorization_code",
								["code"] = code,
								["redirect_uri"] = c.SelfURL() +"?sso=" + HttpUtility.UrlEncode(sso.name)
							});

							await client.SendRequestAsync(request);
							var response = await client.GetResponseAsync();

							token = response.ReadAsJson();
							// { "access_token": "...", "token_type": "Bearer", "expires_in": 28800, "id_token": "..."}
						}
						// id_token is [base64 Meta].[base64 Token].[base64 Signature]
						var tab = ((string)token["id_token"]).Split('.');
						var userInfoBase64 = tab[1];
						// base64 need to be 4 char padded
						if (userInfoBase64.Length % 4 != 0)
						{
							var add = 4 - (userInfoBase64.Length % 4);
							for (int i = 0; i < add; i++)
								userInfoBase64 += "=";
						}

						var userInfo = JsonValue.Parse(Encoding.ASCII.GetString(Convert.FromBase64String(userInfoBase64)));

						// sub field is the user unique id
						Console.WriteLine("User SUB: " + userInfo["sub"]);

						var user = await users.GetUserByOidcIdAsync(userInfo["sub"]);
						if (user != null)
							await cas.CasLoginAsync(c, user.id, "", Idp.ENT, false);
					}
				}
			};
		}
	}
}
*/