// SsoClients.cs
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
    [Model (Table = "sso_client", PrimaryKey = nameof (id))]
    public class SsoClient : Model
    {
        [ModelField]
        public int id { get { return GetField (nameof (id), 0); } set { SetField (nameof (id), value); } }
        [ModelField]
        public string name { get { return GetField<string> (nameof (name), null); } set { SetField (nameof (name), value); } }
        [ModelField]
        public string identity_attribute { get { return GetField<string> (nameof (identity_attribute), null); } set { SetField (nameof (identity_attribute), value); } }
        [ModelField]
        public bool cas_attributes { get { return GetField (nameof (cas_attributes), false); } set { SetField (nameof (cas_attributes), value); } }
        [ModelField (Required = false, ForeignModel = typeof (Resource))]
        public int? resource_id { get { return GetField<int?> (nameof (resource_id), null); } set { SetField (nameof (resource_id), value); } }

        [ModelExpandField (Name = nameof (urls), ForeignModel = typeof (SsoClientUrl))]
        public ModelList<SsoClientUrl> urls { get { return GetField<ModelList<SsoClientUrl>> (nameof (urls), null); } set { SetField (nameof (urls), value); } }

        [ModelExpandField (Name = nameof (attributes), ForeignModel = typeof (SsoClientAttribute))]
        public ModelList<SsoClientAttribute> attributes { get { return GetField<ModelList<SsoClientAttribute>> (nameof (attributes), null); } set { SetField (nameof (attributes), value); } }

        public override async Task EnsureRightAsync (HttpContext context, Right right)
        {
            await context.EnsureIsSuperAdminAsync ();
        }
    }

    public class SsoClients : ModelService<SsoClient>
    {
        public SsoClients (string dbUrl) : base (dbUrl)
        {
        }
    }
}
