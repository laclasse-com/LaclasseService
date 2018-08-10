// Structures.cs
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

using System;
using System.Linq;
using System.Threading.Tasks;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "structure", PrimaryKey = nameof(id))]
	public class Structure : Model
	{
		// should be "^[0-9]{7,7}[A-Z]$"

		[ModelField(Required = true, RegexMatch = "^[0-9A-Z]+$")]
		public string id { get { return GetField<string>(nameof(id), null); } set { SetField(nameof(id), value); } }
		[ModelField]
		public string name { get { return GetField<string>(nameof(name), null); } set { SetField(nameof(name), value); } }
		[ModelField]
		public string siren { get { return GetField<string>(nameof(siren), null); } set { SetField(nameof(siren), value); } }
		[ModelField]
		public string address { get { return GetField<string>(nameof(address), null); } set { SetField(nameof(address), value); } }
		[ModelField]
		public string zip_code { get { return GetField<string>(nameof(zip_code), null); } set { SetField(nameof(zip_code), value); } }
		[ModelField]
		public string city { get { return GetField<string>(nameof(city), null); } set { SetField(nameof(city), value); } }
		[ModelField]
		public string phone { get { return GetField<string>(nameof(phone), null); } set { SetField(nameof(phone), value); } }
		[ModelField]
		public string fax { get { return GetField<string>(nameof(fax), null); } set { SetField(nameof(fax), value); } }
		[ModelField]
		public double? longitude { get { return GetField<double?>(nameof(longitude), null); } set { SetField(nameof(longitude), value); } }
		[ModelField]
		public double? latitude { get { return GetField<double?>(nameof(latitude), null); } set { SetField(nameof(latitude), value); } }
		[ModelField]
		public DateTime? aaf_mtime { get { return GetField<DateTime?>(nameof(aaf_mtime), null); } set { SetField(nameof(aaf_mtime), value); } }
		[ModelField]
		public string domain { get { return GetField<string>(nameof(domain), null); } set { SetField(nameof(domain), value); } }
		[ModelField]
		public string public_ip { get { return GetField<string>(nameof(public_ip), null); } set { SetField(nameof(public_ip), value); } }
		[ModelField(Required = true)]
		public int type { get { return GetField(nameof(type), 0); } set { SetField(nameof(type), value); } }
		[ModelField]
		public bool aaf_sync_activated { get { return GetField(nameof(aaf_sync_activated), false); } set { SetField(nameof(aaf_sync_activated), value); } }
		[ModelField]
		public string private_ip { get { return GetField<string>(nameof(private_ip), null); } set { SetField(nameof(private_ip), value); } }
		[ModelField]
		public string educnat_marking_id { get { return GetField<string>(nameof(educnat_marking_id), null); } set { SetField(nameof(educnat_marking_id), value); } }
		[ModelField]
		public int? aaf_jointure_id { get { return GetField<int?>(nameof(aaf_jointure_id), null); } set { SetField(nameof(aaf_jointure_id), value); } }

		[ModelExpandField(Name = nameof(groups), ForeignModel = typeof(Group))]
		public ModelList<Group> groups { get { return GetField<ModelList<Group>>(nameof(groups), null); } set { SetField(nameof(groups), value); } }

		public async Task<ModelList<Group>> GetGroupsAsync(DB db)
		{
			await LoadExpandFieldAsync(db, nameof(groups));
			return groups;
		}

		[ModelExpandField(Name = nameof(resources), ForeignModel = typeof(StructureResource))]
		public ModelList<StructureResource> resources { get { return GetField<ModelList<StructureResource>>(nameof(resources), null); } set { SetField(nameof(resources), value); } }

		[ModelExpandField(Name = nameof(profiles), ForeignModel = typeof(UserProfile))]
		public ModelList<UserProfile> profiles { get { return GetField<ModelList<UserProfile>>(nameof(profiles), null); } set { SetField(nameof(profiles), value); } }

		[ModelExpandField(Name = nameof(tiles), ForeignModel = typeof(Tile), Visible = false)]
		public ModelList<Tile> tiles { get { return GetField<ModelList<Tile>>(nameof(tiles), null); } set { SetField(nameof(tiles), value); } }

		[ModelExpandField(Name = nameof(flux), ForeignModel = typeof(FluxPortail), Visible = false)]
		public ModelList<FluxPortail> flux { get { return GetField<ModelList<FluxPortail>>(nameof(flux), null); } set { SetField(nameof(flux), value); } }

        public override SqlFilter FilterAuthUser (AuthenticatedUser user)
        {
            if (user.IsSuperAdmin || user.IsApplication)
                return new SqlFilter();

			// read right on all structures for all user
            // that are not just only ELV (student) or TUT (parent)
			if (user.user.profiles.Exists((p) => (p.type != "ELV") && (p.type != "TUT")))
				return new SqlFilter();

            var structuresIds = user.user.profiles.Select ((arg) => arg.structure_id).Distinct ();
			return new SqlFilter() { Where = DB.InFilter("id", structuresIds) };
        }

		public override async Task EnsureRightAsync(HttpContext context, Right right, Model diff)
		{
			if (right == Right.Create)
				await context.EnsureIsSuperAdminAsync();
			else
				await context.EnsureHasRightsOnStructureAsync(
					this, true, (right == Right.Update), (right == Right.Delete));
		}
	}

	public class Structures : ModelService<Structure>
	{
		public Structures(string dbUrl) : base(dbUrl)
		{
			GetAsync["/{id}/subjects"] = async (p, c) =>
			{
				c.Response.StatusCode = 200;
				using (DB db = await DB.CreateAsync(dbUrl))
					c.Response.Content = await db.SelectAsync<Grade>("SELECT * FROM subject WHERE id IN (SELECT `subject_id` FROM `group_user` WHERE `group_id` IN (SELECT id FROM `group` WHERE `structure_id`=?))", (string)p["id"]);
			};
		}
	}
}
