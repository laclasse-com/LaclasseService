// UnusedEmailCheck.cs
//
//  API to check unused mailboxes from user that were deleted
//
// Author(s):
//  Nelson Gonçalves <ngoncalves@erasme.org>
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
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Erasme.Http;
using Laclasse.Authentication;
using Erasme.Json;

namespace Laclasse.Mail
{
    public class UnusedEmailModel
    {
        public string user;
        public DirectoryInfo info;
         
        public UnusedEmailModel(string rootPath, string user) 
        {
            this.user = user;
            var subdir = user.Substring(user.Length - 3);
            var path = Path.Combine(rootPath, subdir, user);
            info = new DirectoryInfo(path);
        }

        public JsonObject ToJson()
        {
            return new JsonObject
            {
                ["user"] = user.ToUpper(),
                ["ctime"] = info.CreationTime,
                ["atime"] = info.LastAccessTime,
                ["wtime"] = info.LastWriteTime
            };
        }
    }

    public class UnusedEmailCheck : HttpRouting
    {
        public UnusedEmailCheck(string dbUrl, string rootPath)
        {
            GetAsync["/cleanup"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                try
                {
                    // Browser mail folder to find users mailboxes
                    var mailUsers = new HashSet<string>();
                    var totalMailboxes = 0;
                    System.IO.Directory.GetDirectories(rootPath).ForEach(subdir =>
                    {
                        System.IO.Directory.GetDirectories(subdir).ForEach(mailbox =>
                        {
                            mailUsers.Add(Path.GetFileName(mailbox).ToUpper());
                            totalMailboxes++;
                        });
                    });


                    using (var db = await DB.CreateAsync(dbUrl, true))
                    {
                        // Get users in database using the found users
                        var res = await db.SelectAsync($"SELECT `{nameof(Directory.User.id)}` FROM `user` WHERE " + DB.InFilter(nameof(Directory.User.id), mailUsers));
                        // Compare them to extract the users with a mailbox that aren't a part of the database anymore
                        res.ForEach((line) =>
                        {
                            string userId = line[nameof(Directory.User.id)] as string;
                            if (mailUsers.Contains(userId))
                            {
                                mailUsers.Remove(userId);
                            }
                        });

                        //Send report
                        var data = new JsonArray();
                        mailUsers.ForEach(user => {
                            var model = new UnusedEmailModel(rootPath, user.ToLower());
                            data.Add(model.ToJson());
                        });

                        c.Response.StatusCode = 200;
                        var responseContent = new JsonObject
                        {
                            { "total", totalMailboxes },
                            { "count", mailUsers.Count },
                            { "data", data }
                        };
                        c.Response.Content = responseContent;
                    }
                }
                catch (UnauthorizedAccessException exception)
                {
                    c.Response.StatusCode = 500;
                    c.Response.Content = exception.Message;
                }
            };

            DeleteAsync["/cleanup"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();

                var json = await c.Request.ReadAsJsonAsync();
                if (json is JsonArray jsonArray && jsonArray.Count > 0)
                {
                    foreach(var jsonValue in jsonArray)
                    {
                        if (jsonValue.Value is string value && value.All(char.IsLetterOrDigit))
                        {

                            var subdir = value.Substring(value.Length - 3);
                            string mailPath = $"{rootPath}/{subdir}/{value}".ToLower();
                            if (System.IO.Directory.Exists(mailPath))
                            {
                                System.IO.Directory.Delete(mailPath,true);
                            }
                        }
                    }
                }
                c.Response.StatusCode = 200;
            };
        }
    }
}