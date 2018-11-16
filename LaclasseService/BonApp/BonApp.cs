using System;
using System.Threading.Tasks;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.BonApp
{
    public class BonAppService : IHttpHandler
    {
        BonAppSetup setup;

        public BonAppService(BonAppSetup setup)
        {
            this.setup = setup;
        }

        public async Task ProcessRequestAsync(HttpContext context)
        {
            if (context.Request.Method != "GET")
                return;

            await context.EnsureIsAuthenticatedAsync();
            var url = new Uri(setup.url);
            using (HttpClient client = await HttpClient.CreateAsync(url))
            {
                HttpClientRequest request = new HttpClientRequest();
                request.Method = "GET";
                request.Path = context.Request.Path;
                request.Headers["menuapi-key"] = setup.apiKey;
                await client.SendRequestAsync(request);
                HttpClientResponse response = await client.GetResponseAsync();
                if (response.StatusCode == 200)
                {
                    var json = await response.ReadAsJsonAsync();
                    context.Response.StatusCode = 200;
                    context.Response.Content = json;
                }
                else
                {
                    context.Response.StatusCode = 500;
                    Console.WriteLine(await response.ReadAsStringAsync());
                }
            }
        }
    }
}
