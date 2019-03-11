// Tiles.cs
// 
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
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

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
    public enum TileNewStatus
	{
		NONE,
		DISPLAY,
		AUTO
	}

    public enum TileRightType
    {
        None,
        Read,
        Write,
        Admin
    }

    [Model(Table = "tile", PrimaryKey = nameof(id))]
	public class Tile : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField(Required = false, ForeignModel = typeof(Tile))]
		public int? parent_id { get { return GetField<int?>(nameof(parent_id), null); } set { SetField(nameof(parent_id), value); } }
		[ModelField(Required = false, ForeignModel = typeof(Structure))]
		public string structure_id { get { return GetField<string>(nameof(structure_id), null); } set { SetField(nameof(structure_id), value); } }
		[ModelField(Required = false, ForeignModel = typeof(User))]
		public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
		[ModelField(ForeignModel = typeof(Application))]
		public string application_id { get { return GetField<string>(nameof(application_id), null); } set { SetField(nameof(application_id), value); } }
		[ModelField(ForeignModel = typeof(Resource))]
		public int? resource_id { get { return GetField<int?>(nameof(resource_id), null); } set { SetField(nameof(resource_id), value); } }
		[ModelField(Required = true)]
		public string type { get { return GetField<string>(nameof(type), null); } set { SetField(nameof(type), value); } }
		[ModelField]
		public string name { get { return GetField<string>(nameof(name), null); } set { SetField(nameof(name), value); } }
		[ModelField]
		public string description { get { return GetField<string>(nameof(description), null); } set { SetField(nameof(description), value); } }
		[ModelField]
		public string url { get { return GetField<string>(nameof(url), null); } set { SetField(nameof(url), value); } }
		[ModelField(Required = true)]
		public int index { get { return GetField(nameof(index), 0); } set { SetField(nameof(index), value); } }
		[ModelField]
		public string color { get { return GetField<string>(nameof(color), null); } set { SetField(nameof(color), value); } }
		[ModelField]
        public string icon { get { return GetField<string>(nameof(icon), null); } set { SetField(nameof(icon), value); } }
		[ModelField]
		public TileNewStatus new_status { get { return GetField(nameof(new_status), TileNewStatus.NONE); } set { SetField(nameof(new_status), value); } }
		[ModelField]
        public DateTime ctime { get { return GetField<DateTime>(nameof(ctime), DateTime.Now); } set { SetField(nameof(ctime), value); } }
		[ModelExpandField(Name = nameof(rights), ForeignModel = typeof(TileRight))]
		public ModelList<TileRight> rights { get { return GetField<ModelList<TileRight>>(nameof(rights), null); } set { SetField(nameof(rights), value); } }
      
		public override async Task EnsureRightAsync(HttpContext context, Right right, Model diff)
		{
            if (right == Right.Create)
            {
                var parentRight = await GetParentRightsAsync(context);
                if (parentRight == TileRightType.None || parentRight == TileRightType.Read)
                    throw new WebException(403, "Insufficient rights");
            }
            else if (right == Right.Update || right == Right.Delete)
            {
                var tileRight = await GetRightsAsync(context);
                if (tileRight == TileRightType.None || tileRight == TileRightType.Read)
                    throw new WebException(403, "Insufficient rights");
            }
            else
            {
                var tileRight = await GetRightsAsync(context);
                if (tileRight == TileRightType.None)
                    throw new WebException(403, "Insufficient rights");
            }

//          if (structure_id != null)
//			{
//				var structure = new Structure { id = structure_id };
//				await context.EnsureHasRightsOnStructureAsync(structure, true, right == Right.Update, right == Right.Create || right == Right.Delete);
//			}
		}

        public async Task<List<Tile>> GetParentsAsync(HttpContext context)
        {
            var parents = new List<Tile>();
            var parent = this;
            while (parent != null)
            {
                parents.Add(parent);
                parent = await GetParentAsync(context);
            }
            parents.Reverse();
            return parents;
        }

        public async Task<Tile> GetParentAsync(HttpContext context)
        {
            if (parent_id == null)
                return null;

            var parent = new Tile { id = (int)parent_id };
            using (var db = await DB.CreateAsync(context.GetSetup().database.url))
                if (!await parent.LoadAsync(db, true))
                    parent = null;
            return parent;
        }

        public async Task<TileRightType> GetRightsAsync(HttpContext context)
        {
            var authUser = await context.GetAuthenticatedUserAsync();
            if (authUser.IsSuperAdmin || authUser.IsApplication)
                return TileRightType.Admin;

            var parentRight = await GetParentRightsAsync(context);
            if (parentRight == TileRightType.None)
                return parentRight;

            var right = TileRightType.None;
            rights.ForEach(r =>
            {
                if (r.profile == TileRightProfile.ALL)
                    right = ConvertRight(r.read, r.write, r.admin);
                else if (r.profile == TileRightProfile.ELV && authUser.user.profiles.Exists((obj) => obj.type == "ELV"))
                    right = ConvertRight(r.read, r.write, r.admin);
                else if (r.profile == TileRightProfile.TUT && authUser.user.profiles.Exists((obj) => obj.type == "TUT"))
                    right = ConvertRight(r.read, r.write, r.admin);
                else if (r.profile == TileRightProfile.ENS && authUser.user.profiles.Exists((obj) => obj.type != "TUT" && obj.type != "ELV"))
                    right = ConvertRight(r.read, r.write, r.admin);
            });
            if (authUser.user.profiles.Exists((obj) => obj.type == "ADM" || obj.type == "DIR"))
                right = ConvertRight(true, true, true);

            return right;
        }

        TileRightType ConvertRight(bool read, bool write, bool admin)
        {
            if (admin)
                return TileRightType.Admin;
            if (write)
                return TileRightType.Write;
            if (read)
                return TileRightType.Read;
            return TileRightType.None;
        }

        public async Task<TileRightType> GetParentRightsAsync(HttpContext context)
        {
            var authUser = await context.GetAuthenticatedUserAsync();
            if (authUser.IsSuperAdmin || authUser.IsApplication)
                return TileRightType.Admin;

            var right = TileRightType.None;

            var parent = await GetParentAsync(context);
            if (parent != null)
            {
                right = await parent.GetRightsAsync(context);
            }
            else if (structure_id != null)
            {
                if (authUser.user.profiles.Exists((obj) => (obj.structure_id == structure_id) && (obj.type == "ADM" || obj.type == "DIR")))
                    right = TileRightType.Admin;
                else if (authUser.user.profiles.Exists((obj) => obj.structure_id == structure_id))
                    right = TileRightType.Read;
            }
            return right;
        }
	}

    public class Context
    {
        public AuthenticatedUser user;
        public DB db;
        public Dictionary<int, Tile> tiles = new Dictionary<int, Tile>();

        public async Task<Tile> GetByIdAsync(int id)
        {
            if (tiles.ContainsKey(id))
                return tiles[id];
            var tile = new Tile { id = id };
            if (!await tile.LoadAsync(db, true))
                tile = null;
            if (tile != null)
            {
                tiles[id] = tile;
                return tile;
            }
            return null;
        }
    }

    public class Tiles : ModelService<Tile>
	{
		public Tiles(string dbUrl) : base(dbUrl)
		{
            // API only available to authenticated users
            BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

            GetAsync["/"] = async (p, c) =>
            {
                var res = new ModelList<Tile>();
                var authUser = await c.GetAuthenticatedUserAsync();
                var filterAuth = (new Tile()).FilterAuthUser(authUser);
                using (DB db = await DB.CreateAsync(dbUrl))
                {
                    var result = await Model.SearchAsync<Tile>(db, c, filterAuth);
                    foreach (var tile in result.Data)
                    {
                        if (await tile.GetRightsAsync(c) != TileRightType.None)
                            res.Add(tile);
                    }
                }
                c.Response.StatusCode = 200;
                c.Response.Content = res.ToJson();
            };
		}
	}
}
