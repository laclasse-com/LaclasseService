using System;
using System.Linq;
using System.Net.Mail;
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
		public DateTime date { get { return GetField<DateTime>(nameof(date), DateTime.Now); } set { SetField(nameof(date), value); } }
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

		public override async Task EnsureRightAsync(HttpContext context, Right right)
		{
			if(right == Right.Update)
				throw new WebException(403, "Publipostage update not allowed");
			// TODO: improve this
			await context.EnsureIsAuthenticatedAsync();
		}
	}

	public class Publipostages : ModelService<Publipostage>
	{
		MailSetup mailSetup;

		class RecipientUser
		{
			public Group group;
			public User user;
			public User child;
			public string message;
		}

		public Publipostages(string dbUrl, MailSetup mailSetup) : base(dbUrl)
		{
			this.mailSetup = mailSetup;
		}

		async Task<List<RecipientUser>> GetRecipientsAsync(DB db, Publipostage item)
		{
			var recipients = new List<RecipientUser>();

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
				var users = await db.SelectExpandAsync<User>("SELECT * FROM `user` WHERE " + db.InFilter("id", item.users.Select((arg) => arg.user_id)), new object[] { });
				foreach (var user in users)
					recipients.Add(new RecipientUser { user = user });
			}
			return recipients;
		}

		protected override async Task OnCreatedAsync(DB db, Publipostage item)
		{
			await base.OnCreatedAsync(db, item);

			Console.WriteLine("Publipostages.OnCreatedAsync");
			var recipients = await GetRecipientsAsync(db, item);
			Console.WriteLine(recipients.Dump());
			var compactRecipients = new List<RecipientUser>();
			// generate HTML message for all recipients
			foreach (var recipient in recipients)
				GenerateHtmlMessage(item.message, recipient);
			// if a recipient user will received several times the same message remove the duplicates
			foreach (var recipient in recipients)
			{
				if (!compactRecipients.Any((arg) => (arg.user.id == recipient.user.id) && (arg.message == recipient.message)))
					compactRecipients.Add(recipient);
			}

			foreach (var recipient in compactRecipients)
			{
				if ((item.diffusion_type == "email") && (recipient.user.emails != null) && (recipient.user.emails.Count > 0))
				{
					var toEmail = recipient.user.emails.SingleOrDefault((arg) => arg.primary);
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
			message = message.Replace("[civilite]", (recipient.user.gender == null) ? "M" : ((recipient.user.gender == "F") ? "Mme" : "M."));
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
