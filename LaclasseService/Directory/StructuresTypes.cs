﻿// StructuresTypes.cs
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

using Erasme.Http;

namespace Laclasse.Directory
{
	[Model(Table = "structure_type", PrimaryKey = "id")]
	public class StructureType : Model
	{
		[ModelField]
		public int id { get { return GetField("id", 0); } set { SetField("id", value); } }
		[ModelField]
		public string name { get { return GetField<string>("name", null); } set { SetField("name", value); } }
		[ModelField]
		public string contrat_type { get { return GetField<string>("contrat_type", null); } set { SetField("contrat_type", value); } }
		[ModelField]
		public string aaf_type { get { return GetField<string>("aaf_type", null); } set { SetField("aaf_type", value); } }
	}

	public class StructuresTypes: HttpRouting
	{
		public StructuresTypes(string dbUrl)
		{
			GetAsync["/"] = async (p, c) =>
			{
				using (DB db = await DB.CreateAsync(dbUrl))
					c.Response.Content = await db.SelectAsync<StructureType>("SELECT * FROM structure_type");
				c.Response.StatusCode = 200;
			};

			GetAsync["/{id:int}"] = async (p, c) =>
			{
				StructureType item;
				using (DB db = await DB.CreateAsync(dbUrl))
					item = await db.SelectRowAsync<StructureType>((int)p["id"]);
				if (item != null)
				{
					c.Response.StatusCode = 200;
					c.Response.Content = item;
				}
			};
		}
	}
}
