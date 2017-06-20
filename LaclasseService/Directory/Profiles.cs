// Profiles.cs
// 
//  Handle profiles API. 
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
using System.Linq;
using System.Threading.Tasks;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "user_profile", PrimaryKey = nameof(id))]
	public class UserProfile : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField]
		public string type { get { return GetField<string>(nameof(type), null); } set { SetField(nameof(type), value); } }
		[ModelField(Required = true, ForeignModel = typeof(Structure))]
		public string structure_id { get { return GetField<string>(nameof(structure_id), null); } set { SetField(nameof(structure_id), value); } }
		[ModelField(Required = true, ForeignModel = typeof(User))]
		public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
		[ModelField]
		public bool active { get { return GetField(nameof(active), false); } set { SetField(nameof(active), value); } }
		[ModelField]
		public DateTime? aaf_mtime { get { return GetField<DateTime?>(nameof(aaf_mtime), null); } set { SetField(nameof(aaf_mtime), value); } }

	
		public async override Task<bool> InsertAsync(DB db)
		{
			var userProfiles = (ModelList<UserProfile>)await LoadExpandFieldAsync<User>(db, nameof(User.profiles), user_id);
			var activeProfiles = userProfiles.FindAll((obj) => obj.active);
			// ensure only 1 active profile per user
			if (activeProfiles.Count == 0)
				active = true;
			bool done = await base.InsertAsync(db);
			if (IsSet(nameof(active)) && active && (activeProfiles.Count > 0))
			{
				foreach (var profile in activeProfiles)
					await profile.DiffWithId(new UserProfile { active = false }).UpdateAsync(db);
			}
			return done;
		}

		public async override Task<bool> UpdateAsync(DB db)
		{
			var oldProfile = this;
			// if the user is not known, load the profile data
			if (!IsSet(nameof(user_id)))
				oldProfile = await db.SelectRowAsync<UserProfile>(id);

			var userProfiles = (ModelList<UserProfile>)await LoadExpandFieldAsync<User>(db, nameof(User.profiles), oldProfile.user_id);
			var activeProfiles = userProfiles.FindAll((obj) => obj.active && (obj.id != id));

			if (activeProfiles.Count == 0)
				active = true;

			var done = await base.UpdateAsync(db);

			if (IsSet(nameof(active)) && active)
			{
				foreach (var profile in activeProfiles)
					await profile.DiffWithId(new UserProfile { active = false }).UpdateAsync(db);
			}
			return done;
		}

		public async override Task<bool> DeleteAsync(DB db)
		{
			var oldProfile = this;
			// if the user is not known, load the profile data
			if (!IsSet(nameof(user_id)))
				oldProfile = await db.SelectRowAsync<UserProfile>(id);

			var userProfiles = (ModelList<UserProfile>)await LoadExpandFieldAsync<User>(db, nameof(User.profiles), oldProfile.user_id);
			var activeProfiles = userProfiles.FindAll((obj) => obj.active && (obj.id != id));

			var res = await base.DeleteAsync(db);

			if (activeProfiles.Count == 0)
			{
				var profile = userProfiles.FirstOrDefault((arg) => arg.id != id);
				if (profile != null)
					await profile.DiffWithId(new UserProfile { active = true }).UpdateAsync(db);
			}
			return res;
		}

		public override async Task EnsureRightAsync(HttpContext context, Right right)
		{
			var user = new User { id = user_id };
			using (var db = await DB.CreateAsync(context.GetSetup().database.url))
				await user.LoadAsync(db, true);

			await context.EnsureHasRightsOnUserAsync(user, true, right == Right.Update, right == Right.Create || right == Right.Delete);
		}
	}

	public class Profiles : ModelService<UserProfile>
	{
		public Profiles(string dbUrl) : base(dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();
		}
	}
}
