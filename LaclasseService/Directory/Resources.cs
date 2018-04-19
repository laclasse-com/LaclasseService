// Resources.cs
// 
//  Handle resources API. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017-2018 Metropole de Lyon
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
using System.Threading.Tasks;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public enum ResourceUrlMode
	{
		GLOBAL,
		USERDEFINED
	}

	public enum ResourceEmbedMode
	{
		IFRAME,
		EXTERNAL,
		PORTAL,
		REPLACE
	}

    public enum ResourceCost
    {
        FREE,
        SUBSCRIPTION
    }

    public enum ResourceTargetUser
    {
        ENS,
        ELV,
        TUT,
        EDU,
        ETA,
        OTHER
    }

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
		[ModelField]
		public string icon { get { return GetField<string>(nameof(icon), null); } set { SetField(nameof(icon), value); } }
		[ModelField]
		public string color { get { return GetField<string>(nameof(color), null); } set { SetField(nameof(color), value); } }
		[ModelField]
		public string description { get { return GetField<string>(nameof(description), null); } set { SetField(nameof(description), value); } }
		[ModelField]
		public string editor { get { return GetField<string>(nameof(editor), null); } set { SetField(nameof(editor), value); } }
		[ModelField]
		public ResourceUrlMode url_mode { get { return GetField(nameof(url_mode), ResourceUrlMode.GLOBAL); } set { SetField(nameof(url_mode), value); } }
		[ModelField]
		public ResourceEmbedMode embed { get { return GetField(nameof(embed), ResourceEmbedMode.EXTERNAL); } set { SetField(nameof(embed), value); } }
        [ModelField]
        public ResourceCost cost { get { return GetField(nameof (cost), ResourceCost.FREE); } set { SetField (nameof (cost), value); } }
        [ModelField]
        public ResourceTargetUser? target_user { get { return GetField<ResourceTargetUser?> (nameof (target_user), null); } set { SetField (nameof (target_user), value); } }

		[ModelExpandField(Name = nameof(structures), ForeignModel = typeof(StructureResource), Visible = false)]
		public ModelList<StructureResource> structures {
			get { return GetField<ModelList<StructureResource>>(nameof(structures), null); }
			set { SetField(nameof(structures), value); } }

		public override async Task EnsureRightAsync(HttpContext context, Right right)
		{
			if (right != Right.Read)
				await context.EnsureIsSuperAdminAsync();
			else
				await context.EnsureIsAuthenticatedAsync();
		}
	}

	public class Resources: ModelService<Resource>
	{
		public Resources(string dbUrl) : base(dbUrl)
		{
		}
	}
}
