// Ent.cs
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
	[Model(Table = "ent", PrimaryKey = nameof(id))]
	public class Ent : Model
	{
		[ModelField]
		public string id { get { return GetField<string>(nameof(id), null); } set { SetField(nameof(id), value); } }
		[ModelField]
		public string mail_domaine { get { return GetField<string>(nameof(mail_domaine), null); } set { SetField(nameof(mail_domaine), value); } }
		[ModelField]
		public long last_id_ent_counter { get { return GetField<long>(nameof(last_id_ent_counter), 0); } set { SetField(nameof(last_id_ent_counter), value); } }
		[ModelField]
		public string ent_letter { get { return GetField<string>(nameof(ent_letter), null); } set { SetField(nameof(ent_letter), value); } }
		[ModelField]
		public int ent_digit { get { return GetField(nameof(ent_digit), 0); } set { SetField(nameof(ent_digit), value); } }

		public override async Task EnsureRightAsync(HttpContext context, Right right)
		{
			if (right != Right.Read)
				await context.EnsureIsSuperAdminAsync();
		}
	}

	public class Ents : ModelService<Ent>
	{
		public Ents(string dbUrl): base(dbUrl)
		{
		}
	}
}
