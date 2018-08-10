// GroupsGrades.cs
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017 Metropole de Lyon
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
	[Model(Table = "group_grade", PrimaryKey = nameof(id))]
	public class GroupGrade : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField(Required = true, ForeignModel = typeof(Group))]
		public int group_id { get { return GetField(nameof(group_id), 0); } set { SetField(nameof(group_id), value); } }
		[ModelField(Required = true, ForeignModel = typeof(Grade))]
		public string grade_id { get { return GetField<string>(nameof(grade_id), null); } set { SetField(nameof(grade_id), value); } }

		public override async Task EnsureRightAsync(HttpContext context, Right right, Model diff)
		{
			var group = new Group { id = group_id };
			using (var db = await DB.CreateAsync(context.GetSetup().database.url))
				await group.LoadAsync(db, true);

			await context.EnsureHasRightsOnGroupAsync(group, true, right == Right.Update, right == Right.Create || right == Right.Delete);
		}
	}

	public class GroupsGrades : ModelService<GroupGrade>
	{
		public GroupsGrades(string dbUrl) : base(dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();
		}
	}
}
