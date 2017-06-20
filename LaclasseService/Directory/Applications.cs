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
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "application", PrimaryKey = nameof(id))]
	public class Application : Model
	{
		[ModelField(Required = true)]
		public string id { get { return GetField<string>(nameof(id), null); } set { SetField(nameof(id), value); } }
		[ModelField]
		public string name { get { return GetField<string>(nameof(name), null); } set { SetField(nameof(name), value); } }
		[ModelField(Required = true)]
		public string url { get { return GetField<string>(nameof(url), null); } set { SetField(nameof(url), value); } }
		[ModelField]
		public string password { get { return GetField<string>(nameof(password), null); } set { SetField(nameof(password), value); } }

		public override async Task EnsureRightAsync(HttpContext context, Right right)
		{
			if (right != Right.Read)
				await context.EnsureIsSuperAdminAsync();
		}
	}

	public class Applications: ModelService<Application>
	{
		readonly string dbUrl;

		public Applications(string dbUrl) : base(dbUrl)
		{
			this.dbUrl = dbUrl;
		}

		public async Task<Application> CheckPasswordAsync(string login, string password)
		{
			using (DB db = await DB.CreateAsync(dbUrl))
				return await CheckPasswordAsync(db, login, password);
		}

		public async Task<Application> CheckPasswordAsync(DB db, string login, string password)
		{
			Application app = null;
			var item = await db.SelectRowAsync<Application>(login);
			if ((item != null) && (item.password != null) && (password == item.password))
				app = item;
			return app;
		}
	}
}
