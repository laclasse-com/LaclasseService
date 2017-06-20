// UserLinks.cs
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
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "user_child", PrimaryKey = nameof(id))]
	public class UserChild : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField(Required = true)]
		public string type { get { return GetField<string>(nameof(type), null); } set { SetField(nameof(type), value); } }
		[ModelField(Required = true, ForeignModel = typeof(User))]
		public string parent_id { get { return GetField<string>(nameof(parent_id), null); } set { SetField(nameof(parent_id), value); } }
		[ModelField(Required = true, ForeignModel = typeof(User))]
		public string child_id { get { return GetField<string>(nameof(child_id), null); } set { SetField(nameof(child_id), value); } }
		[ModelField]
		public bool financial { get { return GetField(nameof(financial), false); } set { SetField(nameof(financial), value); } }
		[ModelField]
		public bool legal { get { return GetField(nameof(legal), false); } set { SetField(nameof(legal), value); } }
		[ModelField]
		public bool contact { get { return GetField(nameof(contact), false); } set { SetField(nameof(contact), value); } }

		public override async Task EnsureRightAsync(HttpContext context, Right right)
		{
			var parent = new User { id = parent_id };
			var child = new User { id = child_id };
			using (var db = await DB.CreateAsync(context.GetSetup().database.url))
			{
				await parent.LoadAsync(db, true);
				await child.LoadAsync(db, true);
			}
			await context.EnsureHasRightsOnUserAsync(parent, true, right == Right.Update, right == Right.Create || right == Right.Delete);
			await context.EnsureHasRightsOnUserAsync(child, true, right == Right.Update, right == Right.Create || right == Right.Delete);
		}
	}

	public class UserLinks : ModelService<UserChild>
	{
		public UserLinks(string dbUrl) : base(dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();
		}
	}
}
