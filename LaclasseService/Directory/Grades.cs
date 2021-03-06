﻿// Grades.cs
// 
//  Handle school grades API. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017 Daniel LACROIX
// Copyright (c) 2017-2018 Metropole de Lyon
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
	[Model(Table = "grade", PrimaryKey = nameof(id))]
	public class Grade : Model
	{
		[ModelField(Required = true)]
		public string id { get { return GetField<string>(nameof(id), null); } set { SetField(nameof(id), value); } }
		[ModelField]
		public string name { get { return GetField<string>(nameof(name), null); } set { SetField(nameof(name), value); } }
		[ModelField]
		public string rattach { get { return GetField<string>(nameof(rattach), null); } set { SetField(nameof(rattach), value); } }
		[ModelField]
		public string stat { get { return GetField<string>(nameof(stat), null); } set { SetField(nameof(stat), value); } }
        
		public override async Task EnsureRightAsync(HttpContext context, Right right, Model diff)
		{
			if (right != Right.Read)
				await context.EnsureIsSuperAdminAsync();
		}
	}

	public class Grades : ModelService<Grade>
	{
		public Grades(string dbUrl) : base(dbUrl)
		{
			GetAsync["/used"] = async (p, c) => {
				var sql = $"SELECT * FROM `grade` INNER JOIN (SELECT DISTINCT(`{nameof(User.student_grade_id)}`) AS `allow_id` FROM `user` WHERE `{nameof(User.student_grade_id)}` IS NOT NULL) AS `allow` ON (`id` = `allow_id`) ORDER BY `id` ASC";
				if (c.Request.QueryStringArray.ContainsKey("structure_id")) {
					sql = $"SELECT * FROM `grade` INNER JOIN(SELECT DISTINCT(`{nameof(User.student_grade_id)}`) AS `allow_id` FROM `user` INNER JOIN(SELECT DISTINCT(`{nameof(UserProfile.user_id)}`) AS `allow_user_id` FROM `user_profile` WHERE  {DB.InFilter(nameof(UserProfile.structure_id), c.Request.QueryStringArray["structure_id"])}) AS `allow_user` ON(`id` = `allow_user_id`) WHERE `{nameof(User.student_grade_id)}` IS NOT NULL) AS `allow` ON(`id` = `allow_id`) ORDER BY `id` ASC";
				}
				using (DB db = await DB.CreateAsync(dbUrl)) {
					c.Response.StatusCode = 200;
					c.Response.Content = await db.SelectAsync<Grade>(sql);
				}
			};
		}
	}
}
