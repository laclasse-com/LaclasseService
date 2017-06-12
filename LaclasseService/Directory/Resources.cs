// Resources.cs
// 
//  Handle resources API. 
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
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	[Model(Table = "resource", PrimaryKey = nameof(id))]
	public class Resource : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField]
		public string name { get { return GetField<string>(nameof(name), null); } set { SetField(nameof(name), value); } }
		[ModelField]
		public string url { get { return GetField<string>(nameof(url), null); } set { SetField(nameof(url), value); } }
		[ModelField]
		public string site_web { get { return GetField<string>(nameof(site_web), null); } set { SetField(nameof(site_web), value); } }
		[ModelField]
		public DateTime? mtime { get { return GetField<DateTime?>(nameof(mtime), null); } set { SetField(nameof(mtime), value); } }
		[ModelField]
		public string type { get { return GetField<string>(nameof(type), null); } set { SetField(nameof(type), value); } }

		[ModelExpandField(Name = nameof(structures), ForeignModel = typeof(StructureResource), Visible = false)]
		public ModelList<StructureResource> structures {
			get { return GetField<ModelList<StructureResource>>(nameof(structures), null); }
			set { SetField(nameof(structures), value); } }
	}

	public class Resources: ModelService<Resource>
	{
		public Resources(string dbUrl) : base(dbUrl)
		{
			// API only available to authenticated users
			BeforeAsync = async (p, c) =>
			{
				if (c.Request.Method != "GET")
					await c.EnsureIsAuthenticatedAsync();
			};
		}
	}
}
