// UsersExtended.cs
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

using System;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "user_extended", PrimaryKey = nameof(id))]
    public class UserExtended : User
    {
		[ModelField]
		public string all_group_name_concat { get { return GetField<string>(nameof(all_group_name_concat), null); } set { SetField(nameof(all_group_name_concat), value); } }
	}

	public class UsersExtended: HttpRouting
    {      
        public UsersExtended(string dbUrl)
        {
            // API only available to authenticated users
			BeforeAsync = async (p, c) => {
				if (c.Request.Method != "GET")
					throw new WebException(400, "Only GET is allowed");
				await c.EnsureIsAuthenticatedAsync();
			};

			// search API
			GetAsync["/"] = async (p, c) => {
                await RunBeforeAsync(null, c);
                var authUser = await c.GetAuthenticatedUserAsync();
				var filterAuth = (new UserExtended()).FilterAuthUser(authUser);
                using (DB db = await DB.CreateAsync(dbUrl))
                {
                    var result = await Model.SearchAsync<UserExtended>(db, c, filterAuth);
                    foreach (var item in result.Data)
                        await item.EnsureRightAsync(c, Right.Read, null);
                    c.Response.Content = result.ToJson(c);
                }
                c.Response.StatusCode = 200;
			};
		}
	}
}
