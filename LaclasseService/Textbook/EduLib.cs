using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erasme.Http;
using Laclasse.Authentication;
using Erasme.Json;
using Laclasse.Directory;

namespace Laclasse.Textbook
{
    [Model(Table = "book_allocation", PrimaryKey = nameof(id))]
    public class BookAllocation : Model
    {
        [ModelField]
        public int id { get => GetField(nameof(id), 0); set => SetField(nameof(id), value); }
        [ModelField(Required = true)]
        public string article_id { get => GetField<string>(nameof(article_id), null); set => SetField(nameof(article_id), value); }
        [ModelField(ForeignModel = typeof(Structure))]
        public string structure_id { get => GetField<string>(nameof(structure_id), null); set => SetField(nameof(structure_id), value); }
        [ModelField(ForeignModel = typeof(User))]
        public string user_id { get => GetField<string>(nameof(user_id), null); set => SetField(nameof(user_id), value); }
        [ModelField(ForeignModel = typeof(Group))]
        public int? group_id { get => GetField<int?>(nameof(group_id), null); set => SetField(nameof(group_id), value); }
    }

    public class BookAllocations : ModelService<BookAllocation>
    {
        public BookAllocations(string dbUrl) : base(dbUrl)
        {

        }
    }
    public enum LicenseType
    {

    }
    public class EduLibService : HttpRouting
    {
        public EduLibService(EduLibSetup setup, string dbUrl)
        {
            GetAsync["/structures/{structure_id}/books"] = async (p, c) =>
            {
                /*
                 * Check if user is authenticated and has rights on structure
                 */
                var authUser = await c.EnsureIsAuthenticatedAsync();
                var uai = p["structure_id"] as string;
                await c.EnsureHasRightsOnStructureAsync(new Structure { id = uai }, true, false, false);
                bool isAdmin = authUser.HasRightsOnStructure(uai, false, false, true);

                /*
                 * Fetch result from Edulib API
                 */
                var url = new Uri(setup.url);
                using (var client = await HttpClient.CreateAsync(url))
                {
                    var request = new HttpClientRequest
                    {
                        Method = "GET",
                        Path = "/api/v1/catalog/laclasse",
                        QueryString = { { "uai", uai }, { "apiKey", setup.apiKey } }
                    };
                    await client.SendRequestAsync(request);
                    var response = await client.GetResponseAsync();
                    c.Response.StatusCode = response.StatusCode;
                    if (response.StatusCode == 200)
                    {
                        /*
                         * If there are results, check if authUser can access it
                         */
                        using (var db = await DB.CreateAsync(dbUrl, false))
                        {
                            var json = await response.ReadAsJsonAsync();
                            if(isAdmin)
                            {
                                c.Response.Content = json;
                                return;
                            }

                            /*
                             * Fetch data needed to filter books
                             */
                            var allowedProfiles = new string[] { "ELV", "ENS", "DOC" };
                            var highestProfileInStructure = authUser.user.profiles
                                .Where((profile) => profile.structure_id == uai)
                                .OrderByDescending((profile) => Array.IndexOf(allowedProfiles, profile.type))
                                .First();
                            var licenseType = highestProfileInStructure.type == "ELV" ? "student" : "teacher";
                            var grade = await db.SelectRowAsync<Grade>(authUser.user.student_grade_id);

                            /*
                             * Books are filtered based on their licensing, and rights
                             */ 
                            var filteredJson = new JsonArray();
                            foreach (var jsonValue in json as JsonArray)
                            {
                                if(jsonValue["license_type"].Value as string != licenseType) { continue; }
                                //TODO Move this out of the loop to only do it once
                                var result = await db.SelectAsync<BookAllocation>("SELECT * FROM `book_allocation` WHERE `article_id` = ? AND `structure_id` = ?", jsonValue["article_id"].Value, uai);
                                if (result.Count > 0 && result.Any((bookUser) => bookUser.user_id == authUser.user.id))
                                    filteredJson.Add(jsonValue);
                                else if (grade != null && licenseType == "student" && (jsonValue["classrooms"] as JsonArray).Any((value) => grade.name.Contains((value.Value as string).ToUpper())))
                                    filteredJson.Add(jsonValue);
                                else if (licenseType == "teacher")
                                    filteredJson.Add(jsonValue);
                            }
                            c.Response.Content = filteredJson;
                        }
                    }
                }
            };
        }

    }
}
