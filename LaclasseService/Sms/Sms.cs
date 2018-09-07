// Sms.cs
// 
//  Handle SMS API. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2018 Metropole de Lyon
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

using System.Threading.Tasks;
using System;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;
using Laclasse.Directory;

namespace Laclasse.Sms
{
	[Model(Table = "sms_user", PrimaryKey = nameof(id))]
    public class SmsUser : Model
    {
        [ModelField]
        public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
        [ModelField]
        public string number { get { return GetField<string>(nameof(number), null); } set { SetField(nameof(number), value); } }
		[ModelField]
		public string user_firstname { get { return GetField<string>(nameof(user_firstname), null); } set { SetField(nameof(user_firstname), value); } }
		[ModelField]
		public string user_lastname { get { return GetField<string>(nameof(user_lastname), null); } set { SetField(nameof(user_lastname), value); } }
		[ModelField(ForeignModel = typeof(Sms))]
		public int sms_id { get { return GetField<int>(nameof(sms_id), 0); } set { SetField(nameof(sms_id), value); } }
        [ModelField(ForeignModel = typeof(User))]
        public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
    }

	[Model(Table = "sms", PrimaryKey = nameof(id))]
    public class Sms : Model
    {
        [ModelField]
        public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField]
        public DateTime ctime { get { return GetField(nameof(ctime), DateTime.Now); } set { SetField(nameof(ctime), value); } }
		[ModelField]
		public string content { get { return GetField<string>(nameof(content), null); } set { SetField(nameof(content), value); } }      
		[ModelField(ForeignModel = typeof(User))]
        public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
		[ModelExpandField(Name = nameof(targets), ForeignModel = typeof(SmsUser))]
		public ModelList<SmsUser> targets { get { return GetField<ModelList<SmsUser>>(nameof(targets), null); } set { SetField(nameof(targets), value); } }
    }

	public class SmsService : ModelService<Sms>
    {
		SmsSetup smsSetup;

		public SmsService(string dbUrl, SmsSetup smsSetup): base(dbUrl)
        {
			this.smsSetup = smsSetup;

            // API only available to admin users
			BeforeAsync = async (p, c) => await c.EnsureIsNotRestrictedUserAsync();

			PostAsync["/"] = async (p, c) =>
			{
				await RunBeforeAsync(null, c);

				var json = await c.Request.ReadAsJsonAsync();

				var sms = new Sms();
                sms.FromJson((JsonObject)json, null, c);
                

				using (var db = await DB.CreateAsync(dbUrl, true))
                {
					JsonArray phones = new JsonArray();

					// resolv targets
					foreach (var target in sms.targets)
                    {
						var user = new User { id = target.user_id };
						await user.LoadAsync(db, true);
						target.user_firstname = user.firstname;
						target.user_lastname = user.lastname;
						var phone = user.phones.Find((ph) => ph.type == "PORTABLE");
                        if (phone != null)
						{
							target.number = phone.number;
							phones.Add(phone.number);
						}                  
					}
                    // save in the DB
					await sms.SaveAsync(db, true);

					// send the SMS
					SendSms(phones, sms.content);

                    // commit
					db.Commit();
                }
				c.Response.StatusCode = 200;
				c.Response.Content = sms;
			};
        }

		void SendSms(JsonArray phones, string message)
		{
			if (phones.Count <= 5)
				SendSmsMax5(phones, message);
			else
			{
				var pos = 0;
				while (pos < phones.Count)
				{
					JsonArray limitedPhones = new JsonArray();
					for (var i = 0; i < 5 && (pos < phones.Count); i++, pos++)
						limitedPhones.Add(phones[pos]);
					SendSmsMax5(phones, message);
				}
			}         
		}

        void SendSmsMax5(JsonArray phones, string message)
		{
			// send the SMS
            var uri = new Uri(smsSetup.url);
            using (var client = HttpClient.Create(uri))
            {
                var clientRequest = new HttpClientRequest();
                clientRequest.Method = "POST";
                clientRequest.Path = uri.PathAndQuery;
                clientRequest.Headers["authorization"] = "Bearer " + smsSetup.token;
                clientRequest.Headers["content-type"] = "application/json";
                var jsonData = new JsonObject
                {
                    ["content"] = message,
                    ["receiver"] = phones
                };
                clientRequest.Content = jsonData.ToString();
                client.SendRequest(clientRequest);
                var response = client.GetResponse();
            }
		}
    }
}
