using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erasme.Http;
using Laclasse.Authentication;
using Erasme.Json;

namespace Laclasse.Textbook
{
    public class EduLibService : HttpRouting
    {
        public EduLibService(EduLibSetup setup, string dbUrl)
        {
            GetAsync["/structures/{structure_id}/books"] = async (p, c) =>
            {
                var authUser = await c.EnsureIsAuthenticatedAsync();
                var uai = p["structure_id"] as string;

                var url = new Uri(setup.url);

                using (HttpClient client = await HttpClient.CreateAsync(url))
                {
                    HttpClientRequest request = new HttpClientRequest
                    {
                        Method = "GET",
                        Path = "/api/v1/catalog/laclasse",
                        QueryString = { { "uai", uai }, { "apiKey", setup.apiKey } }
                    };
                    await client.SendRequestAsync(request);
                    HttpClientResponse response = await client.GetResponseAsync();
                    c.Response.StatusCode = response.StatusCode;
                    if (response.StatusCode == 200)
                    {
                        var json = response.ReadAsJson(); // await response.ReadAsJsonAsync caused NullReference errors 
                        c.Response.Content = json.ToString();
                    }
                }
            };
        }

    }
}
