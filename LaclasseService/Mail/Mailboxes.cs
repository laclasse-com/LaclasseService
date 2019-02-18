using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Mail
{
    public class Mailbox : Model
    {
        [ModelField]
        public string id { get { return GetField<string>(nameof(id), null); } set { SetField(nameof(id), value); } }
        [ModelField]
        public DateTime ctime { get { return GetField<DateTime>(nameof(ctime), DateTime.MinValue); } set { SetField(nameof(ctime), value); } }
        [ModelField]
        public DateTime mtime { get { return GetField<DateTime>(nameof(mtime), DateTime.MinValue); } set { SetField(nameof(mtime), value); } }
        [ModelField]
        public string firstname { get { return GetField<string>(nameof(firstname), null); } set { SetField(nameof(firstname), value); } }
        [ModelField]
        public string lastname { get { return GetField<string>(nameof(lastname), null); } set { SetField(nameof(lastname), value); } }
        [ModelField]
        public bool is_deleted { get { return GetField(nameof(is_deleted), false); } set { SetField(nameof(is_deleted), value); } }
        [ModelField]
        public long? size { get { return GetField<long?>(nameof(size), null); } set { SetField(nameof(size), value); } }
    }

    public class Mailboxes : HttpRouting
    {
        public Mailboxes(string dbUrl, string rootPath)
        {
            GetAsync["/"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();
                var expand = false;
                if (c.Request.QueryString.ContainsKey("expand") && bool.Parse(c.Request.QueryString["expand"]))
                    expand = true;

                var mailboxes = new ModelList<Mailbox>();
                // browse mail folder to find users mailboxes
                foreach (var subdir in System.IO.Directory.GetDirectories(rootPath))
                {
                    foreach (var mailboxFolder in System.IO.Directory.GetDirectories(subdir))
                    {
                        var info = new DirectoryInfo(mailboxFolder);
                        var filename = Path.GetFileName(mailboxFolder);
                        if ((filename.Length < 3) || !filename.EndsWith(Path.GetFileName(subdir), StringComparison.InvariantCulture))
                            continue;
                        var mailbox = new Mailbox
                        {
                            id = filename.ToUpper(),
                            ctime = info.CreationTime,
                            mtime = info.LastWriteTime,
                            is_deleted = true
                        };
                        if (expand)
                            mailbox.size = GetDirectorySize(info);
                        mailboxes.Add(mailbox);
                    }
                }

                var userIds = mailboxes.Select(mailbox => mailbox.id);
                var usersDict = new Dictionary<string, Directory.User>();
                using (DB db = await DB.CreateAsync(dbUrl, false))
                {
                    var tmpIds = new List<string>();
                    var idsCount = 0;

                    Func<Task> loadUsers = async delegate
                    {
                        var sql = $"SELECT * FROM `user` WHERE {DB.InFilter(nameof(Directory.User.id), tmpIds)} ORDER BY `{nameof(Directory.User.id)}`";
                        foreach (var user in await db.SelectAsync<Directory.User>(sql))
                            usersDict[user.id] = user;
                    };

                    // batch request by a group of 200 because it is too slowest for bigger id group
                    foreach (var id in userIds)
                    {
                        tmpIds.Add(id);
                        idsCount++;
                        if (idsCount > 200)
                        {
                            await loadUsers();
                            tmpIds.Clear();
                            idsCount = 0;
                        }
                    }
                    if (idsCount > 0)
                        await loadUsers();
                }
                foreach (var mailbox in mailboxes)
                {
                    if (usersDict.ContainsKey(mailbox.id))
                    {
                        var user = usersDict[mailbox.id];
                        mailbox.firstname = user.firstname;
                        mailbox.lastname = user.lastname;
                        mailbox.is_deleted = false;
                    }
                }

                c.Response.StatusCode = 200;
                c.Response.Content = mailboxes.Search(c);
            };

            DeleteAsync["/"] = async (p, c) =>
            {
                await c.EnsureIsSuperAdminAsync();

                var json = await c.Request.ReadAsJsonAsync();
                if (json is JsonArray jsonArray && jsonArray.Count > 0)
                {
                    foreach (var jsonValue in jsonArray)
                    {
                        if (jsonValue.Value is string value && value.All(char.IsLetterOrDigit))
                        {
                            value = value.ToLower();
                            var subdir = value.Substring(value.Length - 3);
                            var parentPath = Path.Combine(rootPath, subdir);
                            var mailPath = Path.Combine(parentPath, value);
                            if (System.IO.Directory.Exists(mailPath))
                            {
                                System.IO.Directory.Delete(mailPath, true);
                                // remove the parent folder if empty
                                if (System.IO.Directory.EnumerateFileSystemEntries(parentPath).Any())
                                    System.IO.Directory.Delete(parentPath, false);
                            }
                        }
                    }
                }
                c.Response.StatusCode = 200;
            };
        }

        public static long GetDirectorySize(DirectoryInfo dir)
        {
            long size = 0;
            dir.EnumerateFiles().ForEach(f => size += f.Length);
            dir.EnumerateDirectories().ForEach(d => size += GetDirectorySize(d));
            return size;
        }
    }
}
