using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Net.Mail;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
    [Model(Table = "publipostage_group", PrimaryKey = nameof(id))]
    public class PublipostageGroup : Model
    {
        [ModelField]
        public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
        [ModelField(ForeignModel = typeof(Publipostage))]
        public int publipostage_id { get { return GetField(nameof(publipostage_id), 0); } set { SetField(nameof(publipostage_id), value); } }
        [ModelField(ForeignModel = typeof(Group))]
        public int group_id { get { return GetField(nameof(group_id), 0); } set { SetField(nameof(group_id), value); } }
    }

    [Model(Table = "publipostage_user", PrimaryKey = nameof(id))]
    public class PublipostageUser : Model
    {
        [ModelField]
        public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
        [ModelField(ForeignModel = typeof(Publipostage))]
        public int publipostage_id { get { return GetField(nameof(publipostage_id), 0); } set { SetField(nameof(publipostage_id), value); } }
        [ModelField(ForeignModel = typeof(User))]
        public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
    }

    [Model(Table = "publipostage", PrimaryKey = nameof(id))]
    public class Publipostage : Model
    {
        [ModelField]
        public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
        [ModelField]
        public DateTime date { get { return GetField(nameof(date), DateTime.Now); } set { SetField(nameof(date), value); } }
        [ModelField]
        public string message { get { return GetField<string>(nameof(message), null); } set { SetField(nameof(message), value); } }
        [ModelField]
        public string profils { get { return GetField<string>(nameof(profils), null); } set { SetField(nameof(profils), value); } }
        [ModelField]
        public string message_type { get { return GetField<string>(nameof(message_type), null); } set { SetField(nameof(message_type), value); } }
        [ModelField]
        public string descriptif { get { return GetField<string>(nameof(descriptif), null); } set { SetField(nameof(descriptif), value); } }
        [ModelField]
        public string personnels { get { return GetField<string>(nameof(personnels), null); } set { SetField(nameof(personnels), value); } }
        [ModelField]
        public string diffusion_type { get { return GetField<string>(nameof(diffusion_type), null); } set { SetField(nameof(diffusion_type), value); } }
        [ModelField]
        public string destinataires_libelle { get { return GetField<string>(nameof(destinataires_libelle), null); } set { SetField(nameof(destinataires_libelle), value); } }
        [ModelField(ForeignModel = typeof(User))]
        public string user_id { get { return GetField<string>(nameof(user_id), null); } set { SetField(nameof(user_id), value); } }
        [ModelField(ForeignModel = typeof(Subject))]
        public string subject_id { get { return GetField<string>(nameof(subject_id), null); } set { SetField(nameof(subject_id), value); } }

        [ModelExpandField(Name = nameof(groups), ForeignModel = typeof(PublipostageGroup))]
        public ModelList<PublipostageGroup> groups { get { return GetField<ModelList<PublipostageGroup>>(nameof(groups), null); } set { SetField(nameof(groups), value); } }

        [ModelExpandField(Name = nameof(users), ForeignModel = typeof(PublipostageUser))]
        public ModelList<PublipostageUser> users { get { return GetField<ModelList<PublipostageUser>>(nameof(users), null); } set { SetField(nameof(users), value); } }

        public override void FromJson(JsonObject json, string[] filterFields = null, HttpContext context = null)
        {
            base.FromJson(json, filterFields, context);
            // cleanup HTML
            if (message != null)
                message = Utils.Html.RemoveScriptFromHtml(message);
        }

        public override SqlFilter FilterAuthUser(AuthenticatedUser user)
        {
            if (user.IsSuperAdmin || user.IsApplication)
                return new SqlFilter();
            // allow group or children groups 
            var groupIds = user.user.groups.Select((g) => g.group_id).Union(
                user.user.children_groups.Select((g) => g.group_id));

            var filter = $"INNER JOIN(" +
                    $"SELECT `{nameof(Publipostage.id)}` AS `allow_id` FROM `publipostage` WHERE `user_id`= '{DB.EscapeString(user.user.id)}' " +
                    $" UNION SELECT `{nameof(PublipostageUser.publipostage_id)}` AS `allow_id` FROM `publipostage_user` WHERE `{nameof(PublipostageUser.user_id)}`='{DB.EscapeString(user.user.id)}' ";
            if (groupIds.Count() > 0)
                filter += $" UNION SELECT `{nameof(PublipostageGroup.publipostage_id)}` AS `allow_id` FROM `publipostage_group` WHERE {DB.InFilter(nameof(PublipostageGroup.group_id), groupIds)}";
            filter += ") `allow` ON (`id` = `allow_id`)";

            return new SqlFilter { Inner = filter };
        }

        public override async Task EnsureRightAsync(HttpContext context, Right right, Model diff)
        {
            await context.EnsureIsAuthenticatedAsync();
            var authUser = await context.GetAuthenticatedUserAsync();
            if (authUser.IsApplication || authUser.IsSuperAdmin)
                return;

            if ((right == Right.Create || right == Right.Delete) && user_id != authUser.user.id)
                throw new WebException(403, "Not allowed publipostage user_id");
            if (right == Right.Update)
                throw new WebException(403, "Publipostage update not allowed");
        }
    }

    public class Publipostages : ModelService<Publipostage>
    {
        MailSetup mailSetup;

        class RecipientUser : Model
        {
            [ModelField]
            public Group group { get { return GetField<Group>(nameof(group), null); } set { SetField(nameof(group), value); } }

            [ModelField]
            public User user { get { return GetField<User>(nameof(user), null); } set { SetField(nameof(user), value); } }

            [ModelField]
            public User child { get { return GetField<User>(nameof(child), null); } set { SetField(nameof(child), value); } }

            [ModelField]
            public string message { get { return GetField<string>(nameof(message), null); } set { SetField(nameof(message), value); } }
        }

        const string renderToPdfScript = @"
try {
	var fs = require('fs'),
		args = require('system').args,
		page = require('webpage').create();

	page.content = content;
	page.viewportSize = {width: 600, height: 600};
	page.paperSize = {
	    format: args[2],
	    orientation: 'portrait',
    	margin: '1cm',
	};

	page.render(args[1], { format: 'pdf' });
	phantom.exit();
}
catch(e) {
	console.log(e);
	phantom.exit(1);
}";

        public Publipostages(string dbUrl, MailSetup mailSetup) : base(dbUrl)
        {
            this.mailSetup = mailSetup;

            // API only available to authenticated users
            BeforeAsync = async (p, c) => await c.EnsureIsAuthenticatedAsync();

            GetAsync["/{id:int}/pdf"] = async (p, c) =>
            {
                var publi = new Publipostage { id = (int)p["id"] };
                using (DB db = await DB.CreateAsync(dbUrl))
                {
                    if (await publi.LoadAsync(db, true))
                    {
                        // only the admin of the publipostage have right to see the PDF
                        await publi.EnsureRightAsync(c, Right.Create, null);
                        c.Response.StatusCode = 302;
                        c.Response.Headers["location"] = "pdf/" + HttpUtility.UrlEncode(publi.descriptif) + ".pdf?" +
                            HttpUtility.QueryStringToString(c.Request.QueryString);
                    }
                }
            };

            GetAsync["/{id:int}/pdf/{filename}"] = async (p, c) =>
            {
                var pageSize = PageSize.A4;
                if (c.Request.QueryString.ContainsKey("pageSize"))
                    Enum.TryParse(c.Request.QueryString["pageSize"], out pageSize);

                var html = new StringBuilder();
                var publi = new Publipostage { id = (int)p["id"] };
                using (DB db = await DB.CreateAsync(dbUrl))
                {
                    if (await publi.LoadAsync(db, true))
                    {
                        // only the admin of the publipostage have right to see the PDF
                        await publi.EnsureRightAsync(c, Right.Create, null);

                        html.Append("<!DOCTYPE html>\n<html>\n<head>\n");
                        html.Append("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"/>\n");
                        html.Append("<style>* { font-family: sans-serif; }</style>");
                        html.Append("</head>\n");

                        var recipients = await GetRecipientsMessagesAsync(db, publi);
                        bool first = true;
                        foreach (var recipient in recipients)
                        {
                            if (first)
                                html.Append("<div>");
                            else
                                html.Append("<div style=\"page-break-before: always;\">");
                            html.Append(recipient.message);
                            html.Append("</div>\n");
                            first = false;
                        }
                        html.Append("</html>\n");

                        c.Response.StatusCode = 200;
                        c.Response.Headers["content-type"] = "application/pdf";
                        c.Response.Content = HtmlToPdf(html.ToString(), pageSize);
                    }
                }
            };

            PostAsync["/preview"] = async (p, c) =>
            {
                var publi = Model.CreateFromJson<Publipostage>(await c.Request.ReadAsJsonAsync());
                using (DB db = await DB.CreateAsync(dbUrl))
                {
                    await publi.EnsureRightAsync(c, Right.Read, null as Publipostage);
                    var recipientsMessages = await GetRecipientsMessagesAsync(db, publi);
                    foreach (var recipient in recipientsMessages)
                    {
                        if (recipient.user != null)
                            await recipient.user.EnsureRightAsync(c, Right.Read, null as User);
                        if (recipient.group != null)
                            await recipient.group.EnsureRightAsync(c, Right.Read, null as User);
                        if (recipient.child != null)
                            await recipient.child.EnsureRightAsync(c, Right.Read, null as User);
                    }
                    c.Response.StatusCode = 200;
                    c.Response.Content = recipientsMessages;
                }
            };
        }

        public enum PageSize
        {
            A1,
            A2,
            A3,
            A4,
            A5,
            A6,
            A7,
            A8
        }

        static string BuildArguments(string[] args)
        {
            string res = "";
            foreach (string arg in args)
            {
                var tmp = (string)arg.Clone();
                tmp = tmp.Replace("'", "\\'");
                if (res != "")
                    res += " ";
                res += "'" + tmp + "'";
            }
            return res;
        }

        public static Stream HtmlToPdf(string html, PageSize pageSize = PageSize.A4)
        {
            var startInfo = new ProcessStartInfo("/usr/bin/phantomjs", BuildArguments(new string[] {
                "/dev/stdin", "/dev/stdout", pageSize.ToString() }));
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = true;
            startInfo.UseShellExecute = false;
            int exitCode;
            var memStream = new MemoryStream();

            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                // write the JS script to stdin
                process.StandardInput.Write("var content = ");
                process.StandardInput.Write((new JsonPrimitive(html)).ToString());
                process.StandardInput.WriteLine(";");
                process.StandardInput.WriteLine(renderToPdfScript);
                process.StandardInput.Flush();
                process.StandardInput.Close();
                process.StandardOutput.BaseStream.CopyTo(memStream);
                process.WaitForExit();
                memStream.Seek(0, SeekOrigin.Begin);
                exitCode = process.ExitCode;
                if (exitCode != 0)
                {
                    Console.WriteLine($"ERROR: phantomjs EXIT CODE: {exitCode}");
                    Console.WriteLine(process.StandardError.ReadToEnd());
                    throw new WebException(500, "Error while generating the PDF");
                }
            }
            return memStream;
        }

        async Task<ModelList<RecipientUser>> GetRecipientsAsync(DB db, Publipostage item)
        {
            var recipients = new ModelList<RecipientUser>();

            if ((item.groups != null) && (item.groups.Count > 0))
            {
                var profiles = (JsonValue.Parse(item.profils) as JsonArray).Select((arg) => (string)arg);
                foreach (var publiGroup in item.groups)
                {
                    var group = new Group { id = publiGroup.group_id };
                    await group.LoadAsync(db, true);
                    // get all teachers
                    var teachers = new List<User>();
                    foreach (var userId in group.users.FindAll((obj) => obj.type == "ENS").Select((arg) => arg.user_id))
                    {
                        var user = new User { id = userId };
                        await user.LoadAsync(db, true);
                        teachers.Add(user);
                    }
                    // get all students
                    var students = new List<User>();
                    foreach (var userId in group.users.FindAll((obj) => obj.type == "ELV").Select((arg) => arg.user_id))
                    {
                        var user = new User { id = userId };
                        await user.LoadAsync(db, true);
                        students.Add(user);
                    }

                    if (profiles.Contains("eleves"))
                    {
                        foreach (var student in students)
                            recipients.Add(new RecipientUser { group = group, user = student });
                    }
                    if (profiles.Contains("profs"))
                    {
                        foreach (var teacher in teachers)
                            recipients.Add(new RecipientUser { group = group, user = teacher });
                    }
                    if (profiles.Contains("parents"))
                    {
                        foreach (var student in students)
                        {
                            foreach (var userParent in student.parents)
                            {
                                var parent = new User { id = userParent.parent_id };
                                await parent.LoadAsync(db, true);
                                recipients.Add(new RecipientUser { group = group, user = parent, child = student });
                            }
                        }
                    }
                }
            }
            else if (item.users != null)
            {
                var users = await db.SelectExpandAsync<User>("SELECT * FROM `user` WHERE " + DB.InFilter("id", item.users.Select((arg) => arg.user_id)), new object[] { });
                foreach (var user in users)
                    recipients.Add(new RecipientUser { user = user });
            }
            return recipients;
        }

        async Task<ModelList<RecipientUser>> GetRecipientsMessagesAsync(DB db, Publipostage item)
        {
            var recipients = await GetRecipientsAsync(db, item);
            var compactRecipients = new ModelList<RecipientUser>();
            // generate HTML message for all recipients
            foreach (var recipient in recipients)
                GenerateHtmlMessage(item.message, recipient);
            // if a recipient user will received several times the same message remove the duplicates
            foreach (var recipient in recipients)
            {
                if (!compactRecipients.Any((arg) => (arg.user.id == recipient.user.id) && (arg.message == recipient.message)))
                    compactRecipients.Add(recipient);
            }
            return compactRecipients;
        }

        protected override async Task OnCreatedAsync(DB db, Publipostage item)
        {
            await base.OnCreatedAsync(db, item);

            var recipients = await GetRecipientsMessagesAsync(db, item);

            foreach (var recipient in recipients)
            {
                if ((item.diffusion_type == "email") && (recipient.user.emails != null) && (recipient.user.emails.Count > 0))
                {
                    var toEmail = recipient.user.emails.FirstOrDefault((arg) => arg.primary);
                    if (toEmail == null)
                        toEmail = recipient.user.emails[0];
                    Console.WriteLine($"Send FROM: {mailSetup.from}, TO: {toEmail.address}");

                    using (var smtpClient = new SmtpClient(mailSetup.server.host, mailSetup.server.port))
                    {
                        var mailMessage = new MailMessage(mailSetup.from, toEmail.address, "[Laclasse] " + item.descriptif, recipient.message);
                        mailMessage.IsBodyHtml = true;
                        await smtpClient.SendMailAsync(mailMessage);
                    }
                }
                if (item.diffusion_type == "news")
                {
                    // generate news
                    var news = new News
                    {
                        guid = Guid.NewGuid().ToString(),
                        title = item.descriptif,
                        description = recipient.message,
                        pubDate = DateTime.Now,
                        user_id = recipient.user.id,
                        publipostage_id = item.id
                    };
                    await news.SaveAsync(db, true);
                }
            }
        }

        string GenerateHtmlMessage(string message, RecipientUser recipient)
        {
            message = message.Replace("[civilite]", (recipient.user.gender == null) ? "M" : ((recipient.user.gender == Gender.F) ? "Mme" : "M."));
            message = message.Replace("[nom]", recipient.user.lastname);
            message = message.Replace("[prenom]", recipient.user.firstname);
            message = message.Replace("[adresse]", (recipient.user.address ?? "") + " " + (recipient.user.zip_code ?? "") + " " + (recipient.user.city ?? ""));
            message = message.Replace("[date]", DateTime.Now.ToString("d"));
            message = message.Replace("[loginLaClasse]", recipient.user.login);
            message = message.Replace(
                "[pwdLaClasse]", (recipient.user.password == null) ?
                "<em>(non défini)</em>" :
                (recipient.user.password.StartsWith("clear:", StringComparison.InvariantCulture) ?
                 recipient.user.password.Substring(6) :
                 "<em>(vous avez déjà changé votre mot de passe)</em>"));

            if (recipient.group != null)
                message = message.Replace("[classe]", recipient.group.name);
            if (recipient.child != null)
            {
                message = message.Replace("[nomEleve]", recipient.child.lastname);
                message = message.Replace("[prenomEleve]", recipient.child.firstname);
            }
            // TODO: handle [matiere]

            recipient.message = message;
            return message;
        }
    }
}
