// TypesEtablissements.cs
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
using Erasme.Http;
using Erasme.Json;

namespace Laclasse.Directory
{
	public class TypesEtablissements: HttpRouting
	{
		readonly string dbUrl;

		public TypesEtablissements(string dbUrl)
		{
			GetAsync["/"] = async (p, c) =>
			{
				var json = new JsonArray();
				using (DB db = await DB.CreateAsync(dbUrl))
				{
					foreach (var item in await db.SelectAsync("SELECT * FROM type_etablissement"))
					{
						json.Add(new JsonObject
						{
							["id"] = (int)item["id"],
							["nom"] = (string)item["nom"],
							["type_contrat"] = (string)item["type_contrat"],
							["libelle"] = (string)item["libelle"],
							["type_struct_aaf"] = (string)item["type_struct_aaf"]
						});
					}
				}
				c.Response.StatusCode = 200;
				c.Response.Content = json;
			};
		}
	}
}
