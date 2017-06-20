// Tiles.cs
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

using System.Threading.Tasks;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "tile", PrimaryKey = nameof(id))]
	public class Tile : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField(Required = true, ForeignModel = typeof(Structure))]
		public string structure_id { get { return GetField<string>(nameof(structure_id), null); } set { SetField(nameof(structure_id), value); } }
		[ModelField(ForeignModel = typeof(Application))]
		public string application_id { get { return GetField<string>(nameof(application_id), null); } set { SetField(nameof(application_id), value); } }
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

		public override async Task EnsureRightAsync(HttpContext context, Right right)
		{
			var structure = new Structure { id = structure_id };
			await context.EnsureHasRightsOnStructureAsync(structure, true, right == Right.Update, right == Right.Create || right == Right.Delete);
		}
	}

	public class Tiles : ModelService<Tile>
	{
		public Tiles(string dbUrl) : base(dbUrl)
		{
		}
	}
}
