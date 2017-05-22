using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using Laclasse.Directory;
using Erasme.Json;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace Laclasse.Aaf
{
	public class Synchronizer
	{
		static string BaseDir = "/home/daniel/Programmation/laclassev4/aaf/20170327";
		readonly string dbUrl;

		/// <summary>
		/// Convert user to user link type from the AAF 2D id to the ENT id
		/// </summary>
		Dictionary<int, string> aafRelationType = new Dictionary<int, string>
		{
			[1] = "PERE",
			[2] = "MERE",
			[3] = "TUTEUR",
			[4] = "A_MMBR",
			[5] = "DDASS",
			[6] = "A_CAS",
			[7] = "ELEVE"
		};

		/// <summary>
		/// Convert an AAF fonction to the ENT profile
		/// </summary>
		Dictionary<string, string> aafFonctionToProfile = new Dictionary<string, string>
		{
			["ENS"] = "ENS",
			["DOC"] = "DOC",
			["DIR"] = "DIR",
			["EDU"] = "EVS",
			["AED"] = "EVS",
			["SUR"] = "EVS",
			["ADF"] = "ETA",
			["LAB"] = "ETA",
			["ALB"] = "ETA",
			["MDS"] = "ETA",
			["OUV"] = "ETA",
			["CTR"] = "ETA",
			["ASE"] = "ETA",
			["ORI"] = "ETA",
			["CFC"] = "ETA",
			["ACP"] = "ETA",
			["AES"] = "ETA",
			["TEC"] = "ETA"
		};

		public Synchronizer(string dbUrl)
		{
			this.dbUrl = dbUrl;
		}

		public static void ConvertToZip(string file, string destination)
		{
			using(var destStream = File.OpenWrite(destination))
			using(var writer = WriterFactory.Open(destStream, ArchiveType.Zip, new WriterOptions(CompressionType.Deflate)))
			using (Stream stream = File.OpenRead(file))
			using (var reader = ReaderFactory.Open(stream))
			{
				while (reader.MoveToNextEntry())
				{
					if (reader.Entry.IsDirectory)
						continue;
					
					using(var readerStream = reader.OpenEntryStream())
						writer.Write(reader.Entry.Key, readerStream);
				}
			}
		}

		public void TestZip()
		{
			var file = "/home/daniel/Programmation/laclassev4/aaf/Complet69-ENT2D.20170327-global.zip";

			//SharpCompress.Readers.GZip.GZipReader.Open(

			using (Stream stream = File.OpenRead(file))
			using (var reader = ReaderFactory.Open(stream))
			{
				while (reader.MoveToNextEntry())
				{
					Console.WriteLine(reader.Entry.Key);
					if (reader.Entry.Key.EndsWith("EtabEducNat_0000.xml", StringComparison.InvariantCulture))
					{
						using (var entryStream = reader.OpenEntryStream())
							using (StreamReader textReader = new StreamReader(entryStream, Encoding.UTF8))
								Console.WriteLine(textReader.ReadToEnd());
					}
					/*if (!reader.Entry.IsDirectory)
					{
						Console.WriteLine(reader.Entry.Key);
						//reader.WriteEntryToDirectory(@"C:\temp", new ExtractionOptions()
						//{
						//	ExtractFullPath = true,
						//	Overwrite = true
						//});
					}*/
				}
			}
		}

		public async Task Synchronize()
		{
			var dir = new DirectoryInfo(BaseDir);
			Console.WriteLine(dir.GetFiles());
			var files = dir.GetFiles("*_MatiereEducNat_*.xml"); //ENT_0690078K_Complet_20170327_MatiereEducNat_0000.xml
			await SyncSubjectsAsync(files);

			files = dir.GetFiles("*_MefEducNat_*.xml"); //ENT_0690078K_Complet_20170327_MefEducNat_0000.xml
			await SyncGradesAsync(files);

			files = dir.GetFiles("*_EtabEducNat_*.xml"); //ENT_0690078K_Complet_20170327_EtabEducNat_0000.xml
			await SyncStructuresAsync(files);

			files = dir.GetFiles("*_PersEducNat_*.xml"); //ENT_0690078K_Complet_20170327_PersEducNat_0000.xml
			await SyncPersEducNatAsync(files);

			//var stopWatch = new Stopwatch();
			//stopWatch.Start();
			//files = dir.GetFiles("*_PersRelEleve_*.xml"); //ENT_0690078K_Complet_20170327_PersRelEleve_0000.xml
			//var aafParents = LoadPersRelEleveEducNatAsync(files);
			//stopWatch.Stop();
			//Console.WriteLine($"LOAD PARENTS ELAPSE TIME: {stopWatch.Elapsed.TotalSeconds} s");

			//files = dir.GetFiles("*_Eleve_0012.xml"); //ENT_0690078K_Complet_20170327_Eleve_0000.xml
			//await SyncEleveAsync(files, aafParents);

			// TODO: remove all parents handled by the AAF no more seen
		}

		public async Task SyncSubjectsAsync(FileInfo[] files)
		{
			Console.WriteLine("SUBJECTS SYNCHRONIZE");

			var nodes = new List<XmlNode>();

			foreach (var file in files)
			{
				var doc = new XmlDocument();
				doc.XmlResolver = null;
				doc.Load(file.ToString());
				nodes.AddRange(doc.SelectNodes("//addRequest").Cast<XmlNode>());
			}
			Console.WriteLine("addRequest count: " + nodes.Count);

			var aafSubjects = new Dictionary<string, Subject>();

			foreach (XmlNode node in nodes)
			{
				var id = node["identifier"]["id"].InnerText;
				string ENTMatJointure = null;
				string ENTLibelleMatiere = null;
				foreach (XmlNode attr in node["attributes"])
				{
					if (attr.Attributes["name"].Value == "ENTMatJointure")
						ENTMatJointure = attr["value"].InnerText;
					else if (attr.Attributes["name"].Value == "ENTLibelleMatiere")
						ENTLibelleMatiere = attr["value"].InnerText;
				}
				if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(ENTMatJointure) &&
				   !string.IsNullOrEmpty(ENTLibelleMatiere) && (ENTMatJointure == id))
					aafSubjects[id] = new Subject { id = id, name = ENTLibelleMatiere };
			}

			var entSubjects = new Dictionary<string, Subject>();
			using (var db = await DB.CreateAsync(dbUrl))
			{
				var items = await db.SelectAsync<Subject>("SELECT * FROM subject");
				foreach (var item in items)
					entSubjects[item.id] = item;

				foreach (var id in aafSubjects.Keys)
				{
					if (entSubjects.ContainsKey(id))
					{
						if (entSubjects[id].name != aafSubjects[id].name)
						{
							Console.WriteLine($"SUBJECT CHANGED {id} {entSubjects[id].name} => {aafSubjects[id].name}");
							await entSubjects[id].DiffWithId(aafSubjects[id]).UpdateAsync(db);
						}
					}
					else
					{
						Console.WriteLine($"SUBJECT NEW {id} {aafSubjects[id].name}");
						await aafSubjects[id].SaveAsync(db);
					}
				}

				foreach (var id in entSubjects.Keys)
				{
					if (!aafSubjects.ContainsKey(id))
					{
						Console.WriteLine($"SUBJECT REMOVE {id} {entSubjects[id]}");
						await entSubjects[id].DeleteAsync(db);
					}
				}
			}
		}

		public async Task SyncGradesAsync(FileInfo[] files)
		{
			Console.WriteLine("GRADES SYNCHRONIZE");

			var nodes = new List<XmlNode>();

			foreach (var file in files)
			{
				var doc = new XmlDocument();
				doc.XmlResolver = null;
				doc.Load(file.ToString());
				nodes.AddRange(doc.SelectNodes("//addRequest").Cast<XmlNode>());
			}
			Console.WriteLine("addRequest count: " + nodes.Count);

			var aafGrades = new Dictionary<string, Grade>();

			foreach (XmlNode node in nodes)
			{
				var id = node["identifier"]["id"].InnerText;
				string ENTMefJointure = null;
				string ENTLibelleMef = null;
				string ENTMEFRattach = null;
				string ENTMEFSTAT11 = null;
				foreach (XmlNode attr in node["attributes"])
				{
					switch (attr.Attributes["name"].Value)
					{
						case "ENTMefJointure":
							ENTMefJointure = attr["value"].InnerText;
							break;
						case "ENTLibelleMef":
							ENTLibelleMef = attr["value"].InnerText;
							break;
						case "ENTMEFRattach":
							ENTMEFRattach = attr["value"].InnerText;
							break;
						case "ENTMEFSTAT11":
							ENTMEFSTAT11 = attr["value"].InnerText;
							break;
					}
				}
				if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(ENTMefJointure) &&
					!string.IsNullOrEmpty(ENTLibelleMef) && !string.IsNullOrEmpty(ENTMEFRattach) &&
					!string.IsNullOrEmpty(ENTMEFSTAT11) && (ENTMefJointure == id))
				{
					aafGrades[id] = new Grade
					{
						id = id,
						name = ENTLibelleMef,
						rattach = ENTMEFRattach,
						stat = ENTMEFSTAT11
					};
				}
			}

			var entGrades = new Dictionary<string, Grade>();
			using (var db = await DB.CreateAsync(dbUrl))
			{
				var items = await db.SelectAsync<Grade>("SELECT * FROM grade");
				foreach (var item in items)
					entGrades[item.id] = item;

				foreach (var id in aafGrades.Keys)
				{
					if (entGrades.ContainsKey(id))
					{
						if (entGrades[id] != aafGrades[id])
						{
							Console.WriteLine($"GRADE CHANGED {id} {entGrades[id].name} => {aafGrades[id].name}");
							await entGrades[id].DiffWithId(aafGrades[id]).UpdateAsync(db);
						}
					}
					else
					{
						Console.WriteLine($"GRADE NEW {id} {aafGrades[id].name}");
						await aafGrades[id].InsertAsync(db);
					}
				}

				foreach (var id in entGrades.Keys)
				{
					if (!aafGrades.ContainsKey(id))
					{
						Console.WriteLine($"GRADE REMOVE {id} {entGrades[id].name}");
						await entGrades[id].DeleteAsync(db);
					}
				}
			}
		}

		public async Task SyncStructuresAsync(FileInfo[] files)
		{
			Console.WriteLine("STRUCTURES SYNCHRONIZE");

			var nodes = new List<XmlNode>();

			foreach (var file in files)
			{
				var doc = new XmlDocument();
				doc.XmlResolver = null;
				doc.Load(file.ToString());
				nodes.AddRange(doc.SelectNodes("//addRequest").Cast<XmlNode>());
			}
			Console.WriteLine("addRequest count: " + nodes.Count);

			var aafEtabs = new Dictionary<string, Structure>();

			foreach (XmlNode node in nodes)
			{
				var id = node["identifier"]["id"].InnerText;
				List<string> ENTStructureClasses = null;
				List<string> ENTStructureGroupes = null;
				var attrs = new Dictionary<string, string>();
				foreach (XmlNode attr in node["attributes"])
				{
					attrs[attr.Attributes["name"].Value] = attr["value"].InnerText;
					if (attr.Attributes["name"].Value == "ENTStructureClasses")
					{
						ENTStructureClasses = new List<string>();
						foreach (XmlNode childNode in attr.ChildNodes)
							ENTStructureClasses.Add(childNode.InnerText);
					}
					else if (attr.Attributes["name"].Value == "ENTStructureGroupes")
					{
						ENTStructureGroupes = new List<string>();
						foreach (XmlNode childNode in attr.ChildNodes)
							ENTStructureGroupes.Add(childNode.InnerText);
					}
				}
				attrs.RequireFields(
					"ENTStructureJointure", "ENTStructureUAI", "ENTEtablissementUAI",
					"ENTStructureSIREN", "ENTStructureNomCourant", "ENTStructureTypeStruct",
					"ENTEtablissementMinistereTutelle", "ENTEtablissementContrat", "postOfficeBox",
					"street", "postalCode", "l", "telephoneNumber", "facsimileTelephoneNumber",
					"ENTEtablissementStructRattachFctl", "ENTEtablissementBassin", "ENTServAcAcademie");

				var ENTStructureNomCourant = attrs["ENTStructureNomCourant"];
				var ENTServAcAcademie = attrs["ENTServAcAcademie"];
				// remove the Academie name at the end if present
				ENTStructureNomCourant = Regex.Replace(ENTStructureNomCourant, $"-ac-{ENTServAcAcademie}$", "");

				aafEtabs[attrs["ENTStructureUAI"]] = new Structure
				{
					id = attrs["ENTStructureUAI"],
					aaf_jointure_id = Convert.ToInt32(attrs["ENTStructureJointure"]),
					siren = attrs["ENTStructureSIREN"],
					name = ENTStructureNomCourant,
					address = attrs["street"],
					zip_code = attrs["postalCode"],
					city = attrs["l"],
					phone = attrs["telephoneNumber"],
					fax = attrs["facsimileTelephoneNumber"]
				};
				aafEtabs[attrs["ENTStructureUAI"]].Fields["ENTStructureClasses"] = ENTStructureClasses;
				aafEtabs[attrs["ENTStructureUAI"]].Fields["ENTStructureGroupes"] = ENTStructureGroupes;
			}
			var entStructs = new Dictionary<string, Structure>();
			using (var db = await DB.CreateAsync(dbUrl))
			{
				var items = await db.SelectAsync<Structure>("SELECT * FROM structure");
				foreach (var item in items)
					entStructs[item.id] = item;
				Console.WriteLine($"ETABS ENT COUNT: {entStructs.Count}");

				foreach (var id in aafEtabs.Keys)
				{
					if (entStructs.ContainsKey(id))
					{
						if (!entStructs[id].EqualsIntersection(aafEtabs[id]))
						{
							Console.WriteLine($"STRUCTURES CHANGED {id} {entStructs[id].name} => {aafEtabs[id].name}");
							var diff = entStructs[id].DiffWithId(aafEtabs[id]);
							diff.aaf_mtime = DateTime.Now;
							await diff.UpdateAsync(db);
						}

						// check structure groups
						var structGroups = await entStructs[id].GetGroupsAsync(db);
						var entClasses = new Dictionary<string, Directory.Group>();
						var entGroupes = new Dictionary<string, Directory.Group>();
						foreach (var group in structGroups)
						{
							// only keep groups generated by the AAF
							if (group.aaf_name == null)
								continue;
							if (group.type == "CLS")
								entClasses[group.aaf_name] = group;
							else if (group.type == "GRP")
								entGroupes[group.aaf_name] = group;
						}

						// handle CLASSES
						var aafClasses = new Dictionary<string, Directory.Group>();
						foreach (var aafClasse in (List<string>)aafEtabs[id].Fields["ENTStructureClasses"])
						{
							var tab = aafClasse.Split('$');
							if (tab.Length < 3)
								throw new Exception($"For structure {id} Invalid classes define {aafClasse}");
							var classe = new Directory.Group
							{
								aaf_name = tab[0],
								structure_id = id,
								name = string.IsNullOrWhiteSpace(tab[1]) ? tab[0] : tab[1]
							};
							classe.Fields["grades"] = tab.Skip(2);
							aafClasses[classe.aaf_name] = classe;
						}

						foreach (var classeId in aafClasses.Keys)
						{
							var aafClasse = aafClasses[classeId];
							Directory.Group entClasse = null;
							if (entClasses.ContainsKey(classeId))
							{
								entClasse = entClasses[classeId];
								if (!entClasse.EqualsIntersection(aafClasse))
								{
									Console.WriteLine($"CLASSE CHANGED {classeId} {entClasse.name} => {aafClasse.name}");
									var diff = entClasse.DiffWithId(aafClasse);
									diff.aaf_mtime = DateTime.Now;
									await diff.UpdateAsync(db);
									await entClasse.LoadAsync(db);
								}
							}
							else
							{
								Console.WriteLine($"CLASSE NEW {classeId} {aafClasse.name}");
								aafClasse.type = "CLS";
								aafClasse.aaf_mtime = DateTime.Now;
								await aafClasse.SaveAsync(db);
								entClasse = aafClasse;
							}

							// synchronize the group's grades
							var classeGrades = await entClasse.GetGradesAsync(db);
							var aafClasseGrades = (IEnumerable<string>)aafClasses[classeId].Fields["grades"];

							foreach (var classeGrade in classeGrades)
							{
								if (!aafClasseGrades.Contains(classeGrade.grade_id))
								{
									Console.WriteLine($"CLASSE {classeId} REMOVE GRADE {classeGrade.grade_id}");
									await classeGrade.DeleteAsync(db);
								}
							}

							foreach (var gradeId in aafClasseGrades)
							{
								var foundGrade = classeGrades.FirstOrDefault((arg) => arg.grade_id == gradeId);
								if (foundGrade == null)
								{
									Console.WriteLine($"CLASSE {classeId} ADD GRADE {gradeId}");
									var newGroupGrade = new GroupGrade
									{
										group_id = entClasse.id,
										grade_id = gradeId
									};
									await newGroupGrade.InsertAsync(db);
								}
							}
						}

						foreach (var classeId in entClasses.Keys)
						{
							if (!aafClasses.ContainsKey(classeId))
							{
								Console.WriteLine($"CLASSE REMOVE {classeId} {entClasses[classeId].name}");
								// TODO: decide what to do. Remove or not
								//await entClasses[classeId].DeleteAsync(db);
							}
						}

						// handle GROUPES ELEVES
						var aafGroupes = new Dictionary<string, Directory.Group>();
						foreach (var aafGroupe in (List<string>)aafEtabs[id].Fields["ENTStructureGroupes"])
						{
							var tab = aafGroupe.Split('$');
							if (tab.Length < 2)
								throw new Exception($"For structure {id} invalid groupe define {aafGroupe}");
							var groupe = new Directory.Group
							{
								aaf_name = tab[0],
								structure_id = id,
								name = string.IsNullOrWhiteSpace(tab[1]) ? tab[0] : tab[1]
							};
							aafGroupes[groupe.aaf_name] = groupe;
						}

						foreach (var groupeId in aafGroupes.Keys)
						{
							if (entGroupes.ContainsKey(groupeId))
							{
								if (!entGroupes[groupeId].EqualsIntersection(aafGroupes[groupeId]))
								{
									Console.WriteLine($"GROUPE CHANGED {groupeId} {entGroupes[groupeId].name} => {aafGroupes[groupeId].name}");
									var diff = entGroupes[groupeId].DiffWithId(aafGroupes[groupeId]);
									diff.aaf_mtime = DateTime.Now;
									await diff.UpdateAsync(db);
								}
							}
							else
							{
								Console.WriteLine($"GROUPE NEW {groupeId} {aafGroupes[groupeId].name}");
								aafGroupes[groupeId].type = "GRP";
								aafGroupes[groupeId].aaf_mtime = DateTime.Now;
								await aafGroupes[groupeId].InsertAsync(db);
							}
						}

						foreach (var groupeId in entGroupes.Keys)
						{
							if (!aafGroupes.ContainsKey(groupeId))
							{
								Console.WriteLine($"GROUPE REMOVE {groupeId} {entGroupes[groupeId].name}");
								await entGroupes[groupeId].DeleteAsync(db);
							}
						}


						//Console.WriteLine(aafEtabs[id].Fields["ENTStructureClasses"].Dump());
					}
				}
			}
		}

		public async Task<Dictionary<int, Structure>> LoadEntStructuresAsync()
		{
			var entStructures = new Dictionary<int, Structure>();

			using (var db = await DB.CreateAsync(dbUrl))
			{
				var items = await db.SelectAsync<Structure>("SELECT * FROM structure WHERE aaf_jointure_id IS NOT NULL");
				foreach (var item in items)
					entStructures[(int)item.aaf_jointure_id] = item;
			}
			return entStructures;
		}

		public async Task<Dictionary<string, ProfileType>> LoadEntProfilesTypesAsync()
		{
			var entProfilesTypes = new Dictionary<string, ProfileType>();

			using (var db = await DB.CreateAsync(dbUrl))
			{
				var items = await db.SelectAsync<ProfileType>("SELECT * FROM profile_type");
				foreach (var item in items)
					entProfilesTypes[item.id] = item;
			}
			return entProfilesTypes;
		}


		public Dictionary<long,User> LoadPersRelEleveEducNatAsync(FileInfo[] files)
		{
			var aafParents = new Dictionary<long, User>();
			var mulFields = new string[] { "mobile", "mail" };

			foreach (var file in files)
			{
				var doc = new XmlDocument();
				doc.XmlResolver = null;
				doc.Load(file.ToString());

				long id;
				Dictionary<string, string> attrs;
				Dictionary<string, List<string>> attrsMul;

				foreach (XmlNode node in doc.SelectNodes("//addRequest"))
				{
					ReadAttributes(node, mulFields, out id, out attrs, out attrsMul);
					var aafParent = AttributesToUser(id, attrs, attrsMul);
					aafParent.Fields["attrs"] = attrs;
					aafParent.Fields["attrsMul"] = attrsMul;
					aafParents[id] = aafParent;
				}
			}
			return aafParents;
		}

		public async Task SyncPersEducNatAsync(FileInfo[] files)
		{
			Console.WriteLine("PERSEDUCNAT SYNCHRONIZE");

			var structures = await LoadEntStructuresAsync();
			var structuresIds = structures.Values.Select((arg) => arg.id);

			var profilesTypes = await LoadEntProfilesTypesAsync();

			var nodes = new List<XmlNode>();

			foreach (var file in files)
			{
				var doc = new XmlDocument();
				doc.XmlResolver = null;
				doc.Load(file.ToString());
				nodes.AddRange(doc.SelectNodes("//addRequest").Cast<XmlNode>());
			}

			Console.WriteLine("addRequest count: " + nodes.Count);

			foreach (XmlNode node in nodes)
			{
				var id = Convert.ToInt64(node["identifier"]["id"].InnerText);

				List<string> ENTAuxEnsClassesMatieres = null;
				List<string> ENTAuxEnsGroupesMatieres = null;
				var ENTPersonFonctions = new Dictionary<int, string>();

				var attrs = new Dictionary<string, string>();
				foreach (XmlNode attr in node["attributes"])
				{
					attrs[attr.Attributes["name"].Value] = attr["value"].InnerText;
					if (attr.Attributes["name"].Value == "ENTAuxEnsClassesMatieres")
					{
						ENTAuxEnsClassesMatieres = new List<string>();
						foreach (XmlNode childNode in attr.ChildNodes)
							ENTAuxEnsClassesMatieres.Add(childNode.InnerText);
					}
					else if (attr.Attributes["name"].Value == "ENTAuxEnsGroupesMatieres")
					{
						ENTAuxEnsGroupesMatieres = new List<string>();
						foreach (XmlNode childNode in attr.ChildNodes)
							ENTAuxEnsGroupesMatieres.Add(childNode.InnerText);
					}
					else if (attr.Attributes["name"].Value == "ENTPersonFonctions")
					{
						foreach (XmlNode childNode in attr.ChildNodes)
						{
							var tab = childNode.InnerText.Split('$');
							// WARNING: profile '-' exists and means nothing
							if (tab.Length >= 3)
							{
								// convert the "fonction" to a profile
								var profileId = "ETA";
								if (aafFonctionToProfile.ContainsKey(tab[1]))
									profileId = aafFonctionToProfile[tab[1]];

								if (profilesTypes.ContainsKey(profileId))
									ENTPersonFonctions[Convert.ToInt32(tab[0])] = profileId;
							}
						}
					}
				}
				attrs.RequireFields(
					"ENTPersonJointure", "ENTPersonDateNaissance", "sn",
					"givenName", "mail", "ENTPersonStructRattach");

				string gender = null;
				if (attrs.ContainsKey("personalTitle"))
					if (attrs["personalTitle"] == "Mme")
						gender = "F";
					else if (attrs["personalTitle"] == "M.")
						gender = "M";

				DateTime dt;
				DateTime? birthdate = null;
				if (DateTime.TryParseExact(
					attrs["ENTPersonDateNaissance"], "dd/MM/yyyy", CultureInfo.InvariantCulture,
					DateTimeStyles.None, out dt))
					birthdate = dt;

				// normalize the firstname. Lower the givenName and capitalize the first letters
				var firstname = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(attrs["givenName"].ToLower());

				var aafUser = new User
				{
					aaf_jointure_id = id,
					firstname = firstname,
					lastname = attrs["sn"],
					gender = gender,
					birthdate = birthdate
				};

				using (var db = await DB.CreateAsync(dbUrl))
				{
					var entUser = (await db.SelectAsync<User>("SELECT * FROM user WHERE aaf_jointure_id=?", id)).SingleOrDefault();

					// if user not found, try to find the user by its email
					if ((entUser == null) && !string.IsNullOrWhiteSpace(attrs["mail"]))
					{
						entUser = (await db.SelectAsync<User>(
							"SELECT * FROM user WHERE aaf_jointure_id IS NULL AND id IN "+
							"(SELECT user_id FROM email WHERE address LIKE ? AND type='Academique')", attrs["mail"])).SingleOrDefault();
					}

					var interStructs = ENTPersonFonctions.Keys.Intersect(structures.Keys);
					var interCount = interStructs.Count();

					if (entUser == null)
					{
						// if the user is in a structure we handle
						if (interCount > 0)
						{
							Console.WriteLine($"EDUCNATPERS NEW {aafUser.firstname} {aafUser.lastname}");
							// create the user
							await aafUser.SaveAsync(db);
							Console.WriteLine($"RES CTIME: {aafUser.ctime}");
							//await aafUser.InsertAsync(db);
							// create the user "Academique" email
							if (!string.IsNullOrWhiteSpace(attrs["mail"]))
							{
								var email = new Email
								{
									user_id = aafUser.id,
									address = attrs["mail"],
									type = "Academique"
								};
								await email.SaveAsync(db);
							}
							// TODO: create a user "Ent" email (the default for teachers)

							// create the user profiles
							foreach (var structId in interStructs)
							{
								var profile = new UserProfile
								{
									user_id = aafUser.id,
									type = ENTPersonFonctions[structId],
									structure_id = structures[structId].id,
									aaf_mtime = DateTime.Now
								};
								await profile.SaveAsync(db);
							}
							// TODO: create the user groups
						}
					}
					else
					{
						if (!entUser.EqualsIntersection(aafUser))
						{
							Console.WriteLine($"EDUCNATPERS CHANGED {id} {entUser.firstname} {entUser.lastname} => {aafUser.firstname} {aafUser.lastname}");
							var diff = entUser.DiffWithId(aafUser);

							Console.WriteLine("entUser aaf_jointure_id: " + entUser.Fields["aaf_jointure_id"].GetType());
							Console.WriteLine("aafUser aaf_jointure_id: " + aafUser.Fields["aaf_jointure_id"].GetType());

							//diff = DateTime.Now;
							await diff.UpdateAsync(db);
						}
						// TODO: synchronize fonctions/profiles
						var aafProfiles = new ModelList<UserProfile>();
						foreach (var structId in interStructs)
						{
							var aafProfile = new UserProfile
							{
								user_id = entUser.id,
								type = ENTPersonFonctions[structId],
								structure_id = structures[structId].id,
								aaf_mtime = DateTime.Now
							};
							aafProfiles.Add(aafProfile);
						}
						await SyncUserProfiles(db, await entUser.GetProfilesAsync(db), aafProfiles, structuresIds);

						// TODO: synchronize "classes" and "groupe eleve"
					}
				}
			}
			// TODO: remove all profiles managed by the AAF but not seen by the synchronization for the AAF structures
			// IDEA: always update aaf_mtime in a synchronisation and use the the aaf_mtime to remove old link

			// TODO: remove all groups managed by the AAF but not seen by the synchronization for the AAF structures
		}

		async Task SyncUserProfiles(DB db, ModelList<UserProfile> entProfiles, ModelList<UserProfile> aafProfiles,
		                            IEnumerable<string> structuresIds)
		{
			foreach (var aafProfile in aafProfiles)
			{
				if (!structuresIds.Contains(aafProfile.structure_id))
					continue;
				var entProfile = entProfiles.SingleOrDefault(p => ((p.structure_id == aafProfile.structure_id) &&
										   (p.user_id == aafProfile.user_id) && (p.type == aafProfile.type)));
				if (entProfile == null)
				{
					Console.WriteLine("ADD AAF PROFILE: " + aafProfile.type);
					await aafProfile.SaveAsync(db);
				}
				else if (entProfile.aaf_mtime == null)
				{
					Console.WriteLine("CONVERT TO AAF PROFILE: " + aafProfile.type);
					entProfile.aaf_mtime = DateTime.Now;
					await entProfile.UpdateAsync(db);
				}
			}

			foreach (var entProfile in entProfiles)
			{
				// only handle profile for structure managed by the AAF
				if (!structuresIds.Contains(entProfile.structure_id))
					continue;
				// only remove profiles created by the AAF (not ADM for example)
				if (entProfile.aaf_mtime == null)
					continue;
				if (!aafProfiles.Any((arg) => (arg.structure_id == entProfile.structure_id) &&
									 (arg.user_id == entProfile.user_id) && (arg.type == entProfile.type)))
				{
					Console.WriteLine("DELETE ENT PROFILE: " + entProfile.type);
					await entProfile.DeleteAsync(db);
				}
			}
		}

		public async Task SyncEleveAsync(FileInfo[] files, Dictionary<long, User> aafParents)
		{
			Console.WriteLine("ELEVE SYNCHRONIZE");

			var mulFields = new string[] {
				"ENTEleveClasses", "ENTEleveGroupes", "ENTEleveCodeEnseignements",
				"ENTEleveEnseignements", "ENTPersonAutresPrenoms", "ENTElevePersRelEleve" };

			var structures = await LoadEntStructuresAsync();
			var structuresIds = structures.Values.Select((arg) => arg.id);

			var nodes = new List<XmlNode>();

			foreach (var file in files)
			{
				var doc = new XmlDocument();
				doc.XmlResolver = null;
				doc.Load(file.ToString());
				nodes.AddRange(doc.SelectNodes("//addRequest").Cast<XmlNode>());
			}
			Console.WriteLine("addRequest count: " + nodes.Count);

			foreach (var node in nodes)
			{
				long id;
				Dictionary<string, string> attrs;
				Dictionary<string, List<string>> attrsMul;
				ReadAttributes(node, mulFields, out id, out attrs, out attrsMul);
				var aafUser = AttributesToUser(id, attrs, attrsMul);
				var ENTPersonStructRattach = int.Parse(attrs["ENTPersonStructRattach"]);

				// if the user is not in a structure we handle
				if (!structures.ContainsKey(ENTPersonStructRattach))
					continue;

				using (var db = await DB.CreateAsync(dbUrl))
				{
					var entUser = (await db.SelectAsync<User>("SELECT * FROM user WHERE aaf_jointure_id=?", id)).SingleOrDefault();

					// if user not found, try to find the user by its aaf_struct_rattach_id
					if ((entUser == null) && (aafUser.aaf_struct_rattach_id != null))
					{
						entUser = (await db.SelectAsync<User>(
							"SELECT * FROM user WHERE aaf_struct_rattach_id=?", aafUser.aaf_struct_rattach_id)).SingleOrDefault();
					}
					if (entUser == null)
					{
						Console.WriteLine($"ELEVE NEW {aafUser.firstname} {aafUser.lastname}");
						// create the user
						await aafUser.SaveAsync(db);
						entUser = aafUser;
						// TODO: create a user "Ent" email (the default for students)

						// create the user profiles
						var profile = new UserProfile
						{
							user_id = aafUser.id,
							type = "ELV",
							structure_id = structures[ENTPersonStructRattach].id,
							aaf_mtime = DateTime.Now
						};
						await profile.SaveAsync(db);

						// TODO: create the user groups
					}
					else
					{
						if (!entUser.EqualsIntersection(aafUser))
						{
							Console.WriteLine($"ELEVE CHANGED {id} {entUser.firstname} {entUser.lastname} => {aafUser.firstname} {aafUser.lastname}");
							var diff = entUser.DiffWithId(aafUser);
							await diff.UpdateAsync(db);
							await entUser.LoadAsync(db);
						}
						// TODO: synchronize fonctions/profiles
						var aafProfiles = new ModelList<UserProfile>();
						aafProfiles.Add(new UserProfile 
						{
							user_id = entUser.id,
							type = "ELV",
							structure_id = structures[ENTPersonStructRattach].id,
							aaf_mtime = DateTime.Now
						});
						await SyncUserProfiles(db, await entUser.GetProfilesAsync(db), aafProfiles, structuresIds);

						// TODO: synchronize "classes" and "groupe eleve"
					}

					// TODO: handle parents
					if (attrsMul.ContainsKey("ENTElevePersRelEleve"))
					{
						var aafUserChilds = new ModelList<UserChild>();

						foreach (var relEleve in attrsMul["ENTElevePersRelEleve"])
						{
							var tab = relEleve.Split('$');
							if (tab.Length == 6)
							{
								var parent_aaf_jointure_id = long.Parse(tab[0]);
								// ERROR: parent not found in the AAF...
								if (!aafParents.ContainsKey(parent_aaf_jointure_id))
								{
									Console.WriteLine($"WARNING: INVALID ENTElevePersRelEleve PARENT WITH aaf_jointure_id {parent_aaf_jointure_id} NOT FOUND");
									continue;
								}

								var aafParent = aafParents[parent_aaf_jointure_id];
								var entParent = (await db.SelectAsync<User>("SELECT * FROM user WHERE aaf_jointure_id=?", parent_aaf_jointure_id)).SingleOrDefault();
								// if parent not found, need to create it
								// TODO: try to find the parent with other attributes
								if (entParent == null)
								{
									await aafParent.SaveAsync(db);
									entParent = aafParent;
								}
								else
								{
									if (!entParent.EqualsIntersection(aafParent))
									{
										Console.WriteLine($"PARENT CHANGED {parent_aaf_jointure_id} {entParent.firstname} {entParent.lastname} => {aafParent.firstname} {aafParent.lastname}");
										var diff = entParent.DiffWithId(aafParent);
										await diff.UpdateAsync(db);
										await entParent.LoadAsync(db);
									}
								}

								var parentAttrs = (Dictionary<string, string>)aafParent.Fields["attrs"];
								var parentAttrsMul = (Dictionary<string, List<string>>)aafParent.Fields["attrsMul"];

								// handle parent phones
								var aafParentPhones = new ModelList<Phone>();
								if (parentAttrs.ContainsKey("telephoneNumber") && !string.IsNullOrWhiteSpace(parentAttrs["telephoneNumber"]))
								{
									aafParentPhones.Add(new Phone
									{
										user_id = entParent.id,
										number = parentAttrs["telephoneNumber"],
										type = "TRAVAIL"
									});
								}
								if (parentAttrs.ContainsKey("homePhone") && !string.IsNullOrWhiteSpace(parentAttrs["homePhone"]))
								{
									aafParentPhones.Add(new Phone
									{
										user_id = entParent.id,
										number = parentAttrs["homePhone"],
										type = "MAISON"
									});
								}
								if (parentAttrsMul.ContainsKey("mobile"))
								{
									foreach (var mobile in parentAttrsMul["mobile"])
									{
										if (!string.IsNullOrWhiteSpace(mobile))
										{
											aafParentPhones.Add(new Phone
											{
												user_id = entParent.id,
												number = mobile,
												type = "PORTABLE"
											});
										}
									}
								}
								await SyncUserPhones(db, entParent, aafParentPhones);

								// handle parent phones
								var aafParentEmails = new ModelList<Email>();
								if (parentAttrsMul.ContainsKey("mail"))
								{
									foreach (var mail in parentAttrsMul["mail"])
									{
										if (string.IsNullOrWhiteSpace(mail))
											continue;
										aafParentEmails.Add(new Email
										{
											address = mail,
											user_id = entParent.id,
											type = "Autre"
										});
									}
								}
								await SyncUserEmails(db, entParent, aafParentEmails);

								aafUserChilds.Add(new UserChild
								{
									type = aafRelationType[int.Parse(tab[1])],
									user_id = entParent.id,
									child_id = entUser.id,
									financial = tab[2] == "1",
									legal = tab[3] == "1",
									contact = tab[4] == "1"
								});
							}
							else
								Console.WriteLine($"WARNING: INVALID ENTElevePersRelEleve FOUND ({relEleve}) FOR USER {aafUser.aaf_jointure_id}"); 
						}
						// handle user_child relation
						await SyncUserChilds(db, entUser, aafUserChilds);
					}
				}
			}
			// TODO: remove all profiles managed by the AAF but not seen by the synchronization for the AAF structures
			// IDEA: always update aaf_mtime in a synchronisation and use the the aaf_mtime to remove old link

			// TODO: remove all groups managed by the AAF but not seen by the synchronization for the AAF structures
		}

		void ReadAttributes(XmlNode node, string[] mulFields, out long id, out Dictionary<string,string> attrs, out Dictionary<string,List<string>> attrsMul)
		{
			id = Convert.ToInt64(node["identifier"]["id"].InnerText);
			attrs = new Dictionary<string, string>();
			attrsMul = new Dictionary<string, List<string>>();

			foreach (XmlNode attr in node["attributes"])
			{
				var name = attr.Attributes["name"].Value;
				if (mulFields.Contains(name))
				{
					attrsMul[name] = new List<string>();
					foreach (XmlNode childNode in attr.ChildNodes)
						attrsMul[name].Add(childNode.InnerText);
				}
				else
					attrs[name] = attr["value"].InnerText;
			}
		}

		User AttributesToUser(long id, Dictionary<string, string> attrs, Dictionary<string, List<string>> attrsMul)
		{
			var user = new User { aaf_jointure_id = id };
			string gender = null;
			if (attrs.ContainsKey("personalTitle"))
				if (attrs["personalTitle"] == "Mme")
					gender = "F";
				else if (attrs["personalTitle"] == "M.")
					gender = "M";
			if (gender != null)
				user.gender = gender;

			if (attrs.ContainsKey("ENTPersonDateNaissance") && !string.IsNullOrWhiteSpace(attrs["ENTPersonDateNaissance"]))
			{
				DateTime dt;
				if (DateTime.TryParseExact(
					attrs["ENTPersonDateNaissance"], "dd/MM/yyyy", CultureInfo.InvariantCulture,
					DateTimeStyles.None, out dt))
					user.birthdate = dt;
			}

			if (attrs.ContainsKey("ENTPersonAdresse") && !string.IsNullOrWhiteSpace(attrs["ENTPersonAdresse"]))
				user.address = string.Join("\n", attrs["ENTPersonAdresse"].Split(new char[] { '$' }, StringSplitOptions.RemoveEmptyEntries));

			if (attrs.ContainsKey("ENTPersonCodePostal") && !string.IsNullOrWhiteSpace(attrs["ENTPersonCodePostal"]))
				user.zip_code = attrs["ENTPersonCodePostal"];

			if (attrs.ContainsKey("ENTPersonVille") && !string.IsNullOrWhiteSpace(attrs["ENTPersonVille"]))
				user.city = attrs["ENTPersonVille"];

			if (attrs.ContainsKey("ENTPersonPays") && !string.IsNullOrWhiteSpace(attrs["ENTPersonPays"]))
				user.country = attrs["ENTPersonPays"];

			if (attrs.ContainsKey("ENTEleveStructRattachId"))
				user.aaf_struct_rattach_id = int.Parse(attrs["ENTEleveStructRattachId"]);

			// normalize the firstname. Lower the givenName and capitalize the first letters
			if (attrs.ContainsKey("givenName"))
				user.firstname = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(attrs["givenName"].ToLower());

			if (attrs.ContainsKey("sn"))
				user.lastname = attrs["sn"];

			return user;
		}

		/// <summary>
		/// Syncs the user childs with the list of children given by the AAF
		/// </summary>
		/// <returns>The user childs.</returns>
		/// <param name="db">Db.</param>
		/// <param name="entUser">Ent user.</param>
		/// <param name="aafChilds">Aaf childs.</param>
		async Task SyncUserChilds(DB db, User entUser, ModelList<UserChild> aafChilds)
		{
			var entChilds = await entUser.GetParentsAsync(db);
	
			foreach (var aafChild in aafChilds)
			{
				var entChild = entChilds.SingleOrDefault(r => ((r.child_id == aafChild.child_id) &&
				                                               (r.user_id == aafChild.user_id)));
				if (entChild == null)
				{
					Console.WriteLine("ADD AAF USER CHILD: " + aafChild.type);
					await aafChild.SaveAsync(db);
				}
				else
				{
					if (!aafChild.EqualsIntersection(entChild))
						await entChild.DiffWithId(aafChild).UpdateAsync(db);
				}
			}

			foreach (var entChild in entChilds)
			{
				if (!aafChilds.Any((arg) => (arg.child_id == entChild.child_id) &&
				                   (arg.user_id == entChild.user_id)))
				{
					Console.WriteLine("DELETE ENT USER CHILD: " + entChild.type);
					await entChild.DeleteAsync(db);
				}
			}
		}

		/// <summary>
		/// Syncs the user phones with the given AAF phones list
		/// </summary>
		/// <returns>The user phones.</returns>
		/// <param name="db">Db.</param>
		/// <param name="entUser">Ent user.</param>
		/// <param name="aafPhones">Aaf phones.</param>
		async Task SyncUserPhones(DB db, User entUser, ModelList<Phone> aafPhones)
		{
			var entPhones = await entUser.GetPhonesAsync(db);

			foreach (var aafPhone in aafPhones)
			{
				var entPhone = entPhones.SingleOrDefault(r => ((r.type == aafPhone.type) && (r.number == aafPhone.number) &&
															   (r.user_id == aafPhone.user_id)));
				if (entPhone == null)
				{
					Console.WriteLine($"ADD AAF USER {entUser.id} PHONE {aafPhone.number}");
					await aafPhone.SaveAsync(db);
				}
				else
				{
					if (!aafPhone.EqualsIntersection(entPhone))
						await entPhone.DiffWithId(aafPhone).UpdateAsync(db);
				}
			}

			foreach (var entPhone in entPhones)
			{
				if (!aafPhones.Any((arg) => (arg.type == entPhone.type) && (arg.number == entPhone.number) &&
								   (arg.user_id == entPhone.user_id)))
				{
					Console.WriteLine($"DELETE ENT USER {entUser.id} PHONE {entPhone.number}");
					await entPhone.DeleteAsync(db);
				}
			}
		}

		/// <summary>
		/// Syncs the user emails with the given list of AAF emails
		/// </summary>
		/// <returns>The user emails.</returns>
		/// <param name="db">Db.</param>
		/// <param name="entUser">Ent user.</param>
		/// <param name="aafEmails">Aaf emails.</param>
		async Task SyncUserEmails(DB db, User entUser, ModelList<Email> aafEmails)
		{
			var entEmails = await entUser.GetEmailsAsync(db);

			foreach (var aafEmail in aafEmails)
			{
				var entEmail = entEmails.SingleOrDefault(r => ((r.type == aafEmail.type) && (r.address == aafEmail.address) &&
															   (r.user_id == aafEmail.user_id)));
				if (entEmail == null)
				{
					Console.WriteLine($"ADD AAF USER {entUser.id} EMAIL {aafEmail.address}");
					await aafEmail.SaveAsync(db);
				}
				else
				{
					if (!aafEmail.EqualsIntersection(entEmail))
						await entEmail.DiffWithId(aafEmail).UpdateAsync(db);
				}
			}

			foreach (var entEmail in entEmails)
			{
				if (!aafEmails.Any((arg) => (arg.type == entEmail.type) && (arg.address == entEmail.address) &&
								   (arg.user_id == entEmail.user_id)))
				{
					Console.WriteLine($"DELETE ENT USER {entUser.id} EMAIL {entEmail.address}");
					await entEmail.DeleteAsync(db);
				}
			}
		}
	}
}
