// ContextExtensions.cs
// 
//  Inject object in an HttpContext. 
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
using System.Text;
using System.Threading.Tasks;
using Erasme.Http;
using Laclasse.Directory;

namespace Laclasse.Authentication
{
	public class AuthenticatedUser
	{
		public User user;
		public Application application;

		public string Name
		{
			get {
				return (user != null) ? user.id : ((application != null) ? application.id : null);
			}
		}

		public bool IsSuperAdmin
		{
			get {
				return (application != null) || ((user != null) && user.super_admin);
			}
		}

		public bool IsUser
		{
			get {
				return user != null;
			}
		}

		public bool IsApplication
		{
			get {
				return application != null;
			}
		}

        public bool IsRestrictedUser
		{
			get {
				if (IsSuperAdmin)
					return false;
				if (user.profiles.Count == 0)
					return true;
				return !user.profiles.Exists((obj) => (obj.type != "ELV") && (obj.type != "TUT"));
			}
		}      
              
		bool IsProfPrincipal(User user)
		{
			return this.user.groups.Any((g) => g.type == "PRI" && user.groups.Any((ug) => ug.group_id == g.group_id && ug.type == "ELV"));
		}

		public bool HasRightsOnUser(User user, bool read, bool write, bool admin)
		{
			if (IsSuperAdmin)
				return true;
			// only super admin have admin and write rights to other super admins
			if ((admin || write) && user.super_admin)
				return false;
			// admins in structure the user is have admin right
			if (admin)
				return user.profiles.Exists((obj) => HasRightsOnStructure(new Structure { id = obj.structure_id }, false, false, true)) || IsProfPrincipal(user);
			// user himself and admins in structure the user is have write right
			if (write)
				return (this.user.id == user.id) || user.profiles.Exists((obj) => HasRightsOnStructure(new Structure { id = obj.structure_id }, false, false, true)) || IsProfPrincipal(user);
			if (this.user.id == user.id)
				return true;
			// all users except parents and students and user without any profiles have read access on other users
			if (!IsRestrictedUser)
				return true;
			// parents have read right on their children
			if (this.user.children.Exists((obj) => obj.child_id == user.id))
				return true;
			// if the users are in common group, allow read access
			if (this.user.groups.Any((arg) => user.groups.Any((arg2) => arg.group_id == arg2.group_id)))
				return true;
			// all user with a read right on a structure have read right on the user
			return user.profiles.Exists((obj) => HasRightsOnStructure(new Structure { id = obj.structure_id }, true, false, false));
		}

		public bool HasRightsOnGroup(Group group, bool read, bool write, bool admin)
		{
			if (IsSuperAdmin)
				return true;
			if (admin)
			{
				if ((group.structure_id != null) && HasRightsOnStructure(new Structure { id = group.structure_id }, false, false, true))
					return true;
				return user.groups.Exists((obj) => (obj.group_id == group.id) && ((obj.type == "PRI") || (obj.type == "ADM")));
			}
			if (write)
				return user.groups.Exists((obj) => (obj.group_id == group.id));
			if ((group.structure_id != null) && HasRightsOnStructure(new Structure { id = group.structure_id }, true, false, false))
				return true;
			if (group.visibility == GroupVisibility.PUBLIC)
				return true;
			if (user.groups.Exists((obj) => (obj.group_id == group.id)))
				return true;
            if (user.children_groups.Exists ((obj) => (obj.group_id == group.id)))
                return true;
			return false;
		}

		public bool HasRightsOnStructure(Structure structure, bool read, bool write, bool admin)
		{
			return HasRightsOnStructure(structure.id, read, write, admin);
		}

		public bool HasRightsOnStructure(string structure_id, bool read, bool write, bool admin)
		{
			if (IsSuperAdmin)
				return true;
			if (admin || write)
				return user.profiles.Exists((obj) => (obj.structure_id == structure_id) && (obj.type == "ADM" || obj.type == "DIR"));
			// read right for all user with a profile in the structure or all user
			// that are not just only ELV (student) or TUT (parent)
			return user.profiles.Exists((obj) => obj.structure_id == structure_id || ((obj.type != "ELV") && (obj.type != "TUT")));
		}
	}

	public static class ContextExtensions
	{
		static readonly object AuthUserKey = new object();

		public async static Task<AuthenticatedUser> EnsureIsAuthenticatedAsync(this HttpContext context)
		{
			var user = await context.GetAuthenticatedUserAsync();
			if (user == null)
				throw new WebException(401, "Authentication is needed");
			return user;
		}

		public async static Task<AuthenticatedUser> EnsureIsSuperAdminAsync(this HttpContext context)
		{
			var user = await EnsureIsAuthenticatedAsync(context);
			if (!user.IsSuperAdmin)
				throw new WebException(403, "Insufficient authorization");
			return user;
		}

		public async static Task<AuthenticatedUser> EnsureIsNotRestrictedUserAsync(this HttpContext context)
        {
            var user = await EnsureIsAuthenticatedAsync(context);
			if (user.IsRestrictedUser)
                throw new WebException(403, "Insufficient authorization");
            return user;
        }

		// Ensure we have at least admin right on a structure
		public async static Task<AuthenticatedUser> EnsureIsStructureAdminAsync(this HttpContext context)
		{
			var user = await EnsureIsAuthenticatedAsync(context);
			if (user.IsSuperAdmin)
				return user;
			if (user.IsUser && user.user.profiles != null)
			{
				foreach (var profile in user.user.profiles)
				{
					if (user.HasRightsOnStructure(profile.structure_id, false, false, true))
						return user;
				}
			}
			throw new WebException(403, "Insufficient authorization");
		}

		public async static Task<AuthenticatedUser> EnsureHasRightsOnUserAsync(this HttpContext context, User user, bool read, bool write, bool admin)
		{
			var authUser = await EnsureIsAuthenticatedAsync(context);
			if (!authUser.HasRightsOnUser(user, read, write, admin))
			    throw new WebException(403, "Insufficient authorization");
			return authUser;
		}

		public async static Task<AuthenticatedUser> EnsureHasRightsOnGroupAsync(this HttpContext context , Group group, bool read, bool write, bool admin)
		{
			var authUser = await EnsureIsAuthenticatedAsync(context);
			if (!authUser.HasRightsOnGroup(group, read, write, admin))
				throw new WebException(403, "Insufficient authorization");
			return authUser;
		}

		public async static Task<AuthenticatedUser> EnsureHasRightsOnStructureAsync(this HttpContext context, Structure structure, bool read, bool write, bool admin)
		{
			var authUser = await EnsureIsAuthenticatedAsync(context);
			if (!authUser.HasRightsOnStructure(structure, read, write, admin))
				throw new WebException(403, "Insufficient authorization");
			return authUser;
		}

		public async static Task<AuthenticatedUser> GetAuthenticatedUserAsync(this HttpContext context)
		{
			if (context.Data.ContainsKey(AuthUserKey))
				return context.Data[AuthUserKey] as AuthenticatedUser;

			AuthenticatedUser authUser = null;

			// check in the sessions
			var userId = await ((Sessions)context.Data["sessions"]).GetAuthenticatedUserAsync(context);
			if (userId != null)
			{
				var user = await ((Directory.Users)context.Data["users"]).GetUserAsync(userId);
				if (user != null)
					authUser = new AuthenticatedUser { user = user };
			}

			// check for HTTP Basic authorization
			if ((authUser == null) && (context.Request.Headers.ContainsKey("authorization")))
			{
				var parts = context.Request.Headers["authorization"].Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
				if (parts[0].ToLowerInvariant() == "basic")
				{
					var authorization = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
					var pos = authorization.IndexOf(':');
					if (pos != -1)
					{
						var login = authorization.Substring(0, pos);
						var password = authorization.Substring(pos + 1);
						// check in the users
						userId = await ((Users)context.Data["users"]).CheckPasswordAsync(login, password);
						if (userId != null)
						{
							var user = await ((Users)context.Data["users"]).GetUserAsync(userId);
							if (user != null)
								authUser = new AuthenticatedUser { user = user };
						}

						// check in the applications
						if (authUser == null)
						{
							var app = await ((Applications)context.Data["applications"]).CheckPasswordAsync(login, password);
							if (app != null)
								authUser = new AuthenticatedUser { application = app };
						}
					}
				}
			}

			// check for Query string authentication
			if ((authUser == null) && context.Request.QueryString.ContainsKey("app_id") &&
				context.Request.QueryString.ContainsKey("app_key"))
			{
				var login = context.Request.QueryString["app_id"];
				var password = context.Request.QueryString["app_key"];
				// check in the applications
				var app = await ((Applications)context.Data["applications"]).CheckPasswordAsync(login, password);
				if (app != null)
					authUser = new AuthenticatedUser { application = app };
			}

			context.User = (authUser != null) ? authUser.Name : null;
			context.Data[AuthUserKey] = authUser;
			return authUser;
		}

		public async static Task<AuthenticatedUser> SetAuthenticatedUserAsync(this HttpContext context, string userId)
        {
            AuthenticatedUser authUser = null;

            // check in the sessions
            if (userId != null)
            {
                var user = await ((Directory.Users)context.Data["users"]).GetUserAsync(userId);
                if (user != null)
                    authUser = new AuthenticatedUser { user = user };
            }
            context.User = (authUser != null) ? authUser.Name : null;
            context.Data[AuthUserKey] = authUser;
            return authUser;
        }

	}

}
