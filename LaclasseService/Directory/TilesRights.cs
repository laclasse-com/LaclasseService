// TilesRights.cs
// 
//  Handle tiles rights API. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2018 Metropole de Lyon
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
	public enum TileRightProfile
	{
		ALL,
		ENS,
		ELV,
		TUT
	}

	[Model(Table = "tile_right", PrimaryKey = nameof(id))]
	public class TileRight : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField(ForeignModel = typeof(Tile))]
		public int tile_id { get { return GetField(nameof(tile_id), 0); } set { SetField(nameof(tile_id), value); } }
		[ModelField(ForeignModel = typeof(Structure))]
		public string structure_id { get { return GetField<string>(nameof(structure_id), null); } set { SetField(nameof(structure_id), value); } }
		[ModelField(ForeignModel = typeof(Group))]
		public int? group_id { get { return GetField<int?>(nameof(group_id), 0); } set { SetField(nameof(group_id), value); } }
		[ModelField(ForeignModel = typeof(User))]
		public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
		[ModelField]
		public TileRightProfile? profile { get { return GetField<TileRightProfile?>(nameof(profile), null); } set { SetField(nameof(profile), value); } }
		[ModelField]
		public bool admin { get { return GetField(nameof(admin), false); } set { SetField(nameof(admin), value); } }
		[ModelField]
		public bool write { get { return GetField(nameof(write), false); } set { SetField(nameof(write), value); } }
		[ModelField]
		public bool read { get { return GetField(nameof(read), false); } set { SetField(nameof(read), value); } }
	}

	public class TilesRights : ModelService<TileRight>
	{
		public TilesRights(string dbUrl) : base(dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();
		}
	}
}
