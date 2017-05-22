// ProfilesTypes.cs
// 
//  Handle profiles types API. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017 Metropole de Lyon
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
using Erasme.Http;
using Erasme.Json;

namespace Laclasse.Directory
{
	[Model(Table = "profile_type", PrimaryKey = "id")]
	public class ProfileType : Model
	{
		[ModelField]
		public string id { get { return GetField<string>("id", null); } set { SetField("id", value); } }
		[ModelField]
		public string name { get { return GetField<string>("name", null); } set { SetField("name", value); } }
		[ModelField]
		public string code_national { get { return GetField<string>("code_national", null); } set { SetField("code_national", value); } }
	}

	public class ProfilesTypes : HttpRouting
	{
		public ProfilesTypes(string dbUrl)
		{
			GetAsync["/"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
					c.Response.Content = await db.SelectAsync<ProfileType>("SELECT * FROM profile_type");
				c.Response.StatusCode = 200;
			};

			GetAsync["/{id}"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					var item = await db.SelectRowAsync<ProfileType>((string)p["id"]);
					if (item != null)
					{
						c.Response.StatusCode = 200;
						c.Response.Content = item;
					}
				}
			};
		}
	}
}
