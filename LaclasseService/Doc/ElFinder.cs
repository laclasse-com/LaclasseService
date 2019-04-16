// ElFinder.cs
// 
//  Transition API from ElFinder to internal docs
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2019 Metropole de Lyon
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

namespace Laclasse.Doc
{
	public class ElFinder: HttpRouting
	{
        string dbUrl;
        string path;
        string tempDir;
        string directoryDbUrl;
        internal Setup globalSetup;
        internal DocSetup setup;
        internal Blobs blobs;

        public ElFinder(string dbUrl, string path, string tempDir, Blobs blobs, int cacheDuration, string directoryDbUrl, Setup globalSetup, Docs docs)
		{
            this.dbUrl = dbUrl;
            this.path = path;
            this.tempDir = tempDir;
            this.blobs = blobs;
            this.globalSetup = globalSetup;
            this.setup = globalSetup.doc;
            this.directoryDbUrl = directoryDbUrl;

            Get["/"] = (p, c) =>
            {
                c.Response.StatusCode = 302;
                c.Response.Headers["location"] = "/portail/#app.doc";
            };

            GetAsync["/connector"] = async (p, c) =>
            {
                string cmd = null;
                if (c.Request.QueryString.ContainsKey("cmd"))
                    cmd = c.Request.QueryString["cmd"];
                if (cmd == "file")
                {
                    var target = c.Request.QueryString["target"];
                    var id = long.Parse(target.Substring(1));

                    using (DB db = await DB.CreateAsync(dbUrl, true))
                    {
                        var context = new Context { setup = setup, storageDir = path, tempDir = tempDir, docs = docs, blobs = blobs, db = db, user = await c.GetAuthenticatedUserAsync(), directoryDbUrl = directoryDbUrl, httpContext = c };
                        var item = await context.GetByIdAsync(id);
                        if (item != null)
                        {
                            c.Response.StatusCode = 302;
                            var accessUrl = $"/api/docs/{id}/content";
                            if (item is Folder)
                                accessUrl = $"/portail/#app.doc/node/{id}";
                            else if (item is OnlyOffice)
                                accessUrl = $"/api/docs/{id}/onlyoffice";
                            c.Response.Headers["location"] = accessUrl;
                        }
                        await db.CommitAsync();
                    }
                }
            };
		}
	}
}
