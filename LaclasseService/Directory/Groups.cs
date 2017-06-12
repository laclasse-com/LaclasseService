// Groupe.cs
// 
//  Handle groupes API. 
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

using System;
using System.Threading.Tasks;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "group", PrimaryKey = nameof(id))]
	public class Group : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField]
		public string name { get { return GetField<string>(nameof(name), null); } set { SetField(nameof(name), value); } }
		[ModelField]
		public string description { get { return GetField<string>(nameof(description), null); } set { SetField(nameof(description), value); } }
		[ModelField]
		public DateTime? aaf_mtime { get { return GetField<DateTime?>(nameof(aaf_mtime), null); } set { SetField(nameof(aaf_mtime), value); } }
		[ModelField]
		public string aaf_name { get { return GetField<string>(nameof(aaf_name), null); } set { SetField(nameof(aaf_name), value); } }
		[ModelField(Required = true)]
		public string type { get { return GetField<string>(nameof(type), null); } set { SetField(nameof(type), value); } }
		[ModelField(ForeignModel = typeof(Structure))]
		public string structure_id { get { return GetField<string>(nameof(structure_id), null); } set { SetField(nameof(structure_id), value); } }
		[ModelField]
		public DateTime? ctime { get { return GetField<DateTime?>(nameof(ctime), null); } set { SetField(nameof(ctime), value); } }

		[ModelExpandField(Name = nameof(grades), ForeignModel = typeof(GroupGrade))]
		public ModelList<GroupGrade> grades { get { return GetField<ModelList<GroupGrade>>(nameof(grades), null); } set { SetField(nameof(grades), value); } }

		[ModelExpandField(Name = nameof(users), ForeignModel = typeof(GroupUser))]
		public ModelList<GroupUser> users { get { return GetField<ModelList<GroupUser>>(nameof(users), null); } set { SetField(nameof(users), value); } }
	}

	public class Groups : ModelService<Group>
	{
		public Groups(string dbUrl) : base(dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();
		}
	}
}
