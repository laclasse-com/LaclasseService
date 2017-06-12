// Applications.cs
// 
//  Handle applications API. 
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

using System.Threading.Tasks;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "application", PrimaryKey = "id")]
	public class Application : Model
	{
		[ModelField(Required = true)]
		public string id { get { return GetField<string>("id", null); } set { SetField("id", value); } }
		[ModelField]
		public string name { get { return GetField<string>("name", null); } set { SetField("name", value); } }
		[ModelField(Required = true)]
		public string url { get { return GetField<string>("url", null); } set { SetField("url", value); } }
		[ModelField]
		public string password { get { return GetField<string>("password", null); } set { SetField("password", value); } }
	}

	public class Applications: ModelService<Application> //HttpRouting
	{
		readonly string dbUrl;

		public Applications(string dbUrl) : base(dbUrl)
		{
			this.dbUrl = dbUrl;

			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();
		}

		public async Task<string> CheckPasswordAsync(string login, string password)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await CheckPasswordAsync(db, login, password);
		}

		public async Task<string> CheckPasswordAsync(DB db, string login, string password)
		{
			string user = null;
			var item = await db.SelectRowAsync<Application>(login);
			if ((item != null) && (item.password != null) && (password == item.password))
				user = item.id;
			return user;
		}
	}
}
