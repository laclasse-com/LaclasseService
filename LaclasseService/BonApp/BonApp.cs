using System;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.BonApp
{
    public class BonAppService : IHttpHandler
    {
        BonAppSetup setup;
        Utils.Cache<JsonValue> cache;

        public BonAppService(BonAppSetup setup)
        {
            this.setup = setup;
            // allow data caching to 20 min
            cache = new Utils.Cache<JsonValue>(200, TimeSpan.FromMinutes(20), async (string key) =>
            {
                JsonValue res = null;
                var url = new Uri(setup.url);
                using (HttpClient client = await HttpClient.CreateAsync(url))
                {
                    HttpClientRequest request = new HttpClientRequest();
                    request.Method = "GET";
                    request.Path = key;
                    request.Headers["menuapi-key"] = setup.apiKey;
                    await client.SendRequestAsync(request);
                    HttpClientResponse response = await client.GetResponseAsync();
                    if (response.StatusCode == 200)
                        res = await response.ReadAsJsonAsync();
                    else
                        Console.WriteLine(await response.ReadAsStringAsync());
                }
                return res;
            });
        }

        public async Task ProcessRequestAsync(HttpContext context)
        {
            if (context.Request.Method != "GET")
                return;

            await context.EnsureIsAuthenticatedAsync();

            var json = await cache.GetAsync(context.Request.Path);
            if (json != null)
            {
                context.Response.StatusCode = 200;
                context.Response.Content = json;
            }
            else
                context.Response.StatusCode = 500;
        }
    }
}
