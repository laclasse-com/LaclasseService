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
using System.Linq;
using System.Threading.Tasks;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{   
	public enum GroupType
	{
		GPL,
		GRP,
		CLS
	}

	public enum GroupVisibility
	{
		PRIVATE,
		PUBLIC
	}

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
		public GroupType type { get { return GetField(nameof(type), GroupType.GPL); } set { SetField(nameof(type), value); } }
		[ModelField(ForeignModel = typeof(Structure))]
		public string structure_id { get { return GetField<string>(nameof(structure_id), null); } set { SetField(nameof(structure_id), value); } }
		[ModelField]
		public DateTime? ctime { get { return GetField<DateTime?>(nameof(ctime), null); } set { SetField(nameof(ctime), value); } }
		[ModelField]
		public GroupVisibility visibility { get { return GetField<GroupVisibility>(nameof(visibility), GroupVisibility.PRIVATE); } set { SetField(nameof(visibility), value); } }
              
		[ModelExpandField(Name = nameof(grades), ForeignModel = typeof(GroupGrade))]
		public ModelList<GroupGrade> grades { get { return GetField<ModelList<GroupGrade>>(nameof(grades), null); } set { SetField(nameof(grades), value); } }

		[ModelExpandField(Name = nameof(users), ForeignModel = typeof(GroupUser))]
		public ModelList<GroupUser> users { get { return GetField<ModelList<GroupUser>>(nameof(users), null); } set { SetField(nameof(users), value); } }

		public override SqlFilter FilterAuthUser (AuthenticatedUser user)
        {
            if (user.IsSuperAdmin || user.IsApplication)
				return new SqlFilter();

			var groupsIds = user.user.groups.Select((arg) => arg.group_id);
            groupsIds = groupsIds.Concat(user.user.children_groups.Select((arg) => arg.group_id));
            groupsIds = groupsIds.Distinct();

			// users that are not only just only ELV (student) or TUT (parent)
			// can see all GPL groups and all groups in the structures they
			// belongs to
			if (user.user.profiles.Exists((p) => (p.type != "ELV") && (p.type != "TUT")))
			{
				var structuresIds = user.user.profiles.Select((arg) => arg.structure_id).Distinct();
				return new SqlFilter() { Where = $"(`visibility`='PUBLIC' OR {DB.InFilter("structure_id", structuresIds)} OR {DB.InFilter("id", groupsIds)})" };
			}
                     
			// ELV (student) and TUT (parent) only sees group they belongs to
            // or their childre belongs to
			return new SqlFilter() { Where = $"{DB.InFilter("id", groupsIds)}" };
        }

		public override async Task EnsureRightAsync(HttpContext context, Right right, Model diff)
		{
			var user = await context.GetAuthenticatedUserAsync();
			if (user == null)
				throw new WebException(401, "Authentication needed");
			if (user.IsSuperAdmin)
				   return;
			if ((right == Right.Create) && (type == GroupType.GPL))
			{
				// allow all profiles except ELV and TUT to group "GPL" group in their structure
				if (structure_id != null) {
					if (user.user.profiles.Any((arg) => arg.structure_id == structure_id && arg.type != "ELV" && arg.type != "TUT"))
						return;
				}
				// allow all profiles except ELV and TUT to create group out of any structure
				else if (user.user.profiles.Any((arg) => arg.type != "ELV" && arg.type != "TUT"))
					return;
			}
			await context.EnsureHasRightsOnGroupAsync(this, true, false, (right == Right.Create) || (right == Right.Delete) || (right == Right.Update));
		}
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
