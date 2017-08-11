// GroupsUsers.cs
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017 Daniel LACROIX
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
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "group_user", PrimaryKey = nameof(id))]
	public class GroupUser : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField]
		public string type { get { return GetField<string>(nameof(type), null); } set { SetField(nameof(type), value); } }
		[ModelField(Required = true, ForeignModel = typeof(Group))]
		public int group_id { get { return GetField(nameof(group_id), 0); } set { SetField(nameof(group_id), value); } }
		[ModelField(Required = true, ForeignModel = typeof(User))]
		public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
		[ModelField]
		public string subject_id { get { return GetField<string>(nameof(subject_id), null); } set { SetField(nameof(subject_id), value); } }
		[ModelField]
		public DateTime ctime { get { return GetField(nameof(ctime), DateTime.Now); } set { SetField(nameof(ctime), value); } }
		[ModelField]
		public DateTime? aaf_mtime { get { return GetField<DateTime?>(nameof(aaf_mtime), null); } set { SetField(nameof(aaf_mtime), value); } }
		[ModelField]
		public bool pending_validation { get { return GetField(nameof(pending_validation), false); } set { SetField(nameof(pending_validation), value); } }

		public override async Task EnsureRightAsync(HttpContext context, Right right)
		{
			var authUser = await context.EnsureIsAuthenticatedAsync();

			var group = new Group { id = group_id };
			using (var db = await DB.CreateAsync(context.GetSetup().database.url))
				await group.LoadAsync(db, true);

			// ok if we have rights on the group
			if (authUser.HasRightsOnGroup(group, true, right == Right.Update, right == Right.Create || right == Right.Delete))
				return;

			// load the target user
			if (user_id != null)
			{
				// a user can ask to enter in a group or remove himself from the group
				if (authUser.IsUser && authUser.user.id == user_id && ((right == Right.Delete) || ((right == Right.Create) && (pending_validation == true))))
					return;

				var user = new User { id = user_id };
				using (var db = await DB.CreateAsync(context.GetSetup().database.url))
				{
					if (!await user.LoadAsync(db, true))
						throw new WebException(403, "Can check the user");
				}

				// a user with admin rights on the group's user can ask for a pending validation
				if ((right == Right.Create) && (pending_validation == true) && authUser.HasRightsOnUser(user, false, false, true))
					return;
			}

			throw new WebException(403, "Insufficient authorization");
		}
	}

	public class GroupsUsers : ModelService<GroupUser>
	{
		public GroupsUsers(string dbUrl) : base(dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();
		}
	}
}
