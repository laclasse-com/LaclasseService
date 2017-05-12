using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using Laclasse.Directory;
using Erasme.Json;

namespace Laclasse.Aaf
{
	public class Synchronizer
	{
		static string BaseDir = "/home/daniel/Programmation/laclassev4/aaf/20170327";
		readonly string dbUrl;
		Subjects subjects;
//		Grades grades;

		public Synchronizer(string dbUrl, Subjects subjects, Grades grades)
		{
			this.dbUrl = dbUrl;
			this.subjects = subjects;
//			this.grades = grades;
		}

		public async Task Synchronize()
		{
			var dir = new DirectoryInfo(BaseDir);
			Console.WriteLine(dir.GetFiles());
			var files = dir.GetFiles("*_MatiereEducNat_*.xml"); //ENT_0690078K_Complet_20170327_MatiereEducNat_0000.xml
			foreach (var file in files)
				await SyncSubjects(file);

			files = dir.GetFiles("*_MefEducNat_*.xml"); //ENT_0690078K_Complet_20170327_MefEducNat_0000.xml
			foreach (var file in files)
				await SyncGrades(file);

			files = dir.GetFiles("*EtabEducNat_*.xml"); //ENT_0690078K_Complet_20170327_EtabEducNat_0000.xml
			foreach (var file in files)
				await SyncStructures(file);
		}

		public async Task SyncSubjects(FileInfo file)
		{
			Console.WriteLine("SUBJECTS SYNCHRONIZE");

			var doc = new XmlDocument();
			doc.XmlResolver = null;
			doc.Load(file.ToString());

			var nodes = doc.SelectNodes("//addRequest");
			Console.WriteLine("addRequest count: " + nodes.Count);

			var aafSubjects = new Dictionary<string, string>();

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
					aafSubjects[id] = ENTLibelleMatiere;
			}

			var entSubjects = new Dictionary<string, string>();
			using (var db = await DB.CreateAsync(dbUrl))
			{
				var items = await db.SelectAsync("SELECT * FROM subject");
				foreach (var item in items)
				{
					entSubjects[(string)item["id"]] = (string)item["name"];
				}

				foreach (var id in aafSubjects.Keys)
				{
					if (entSubjects.ContainsKey(id))
					{
						if (entSubjects[id] != aafSubjects[id])
						{
							Console.WriteLine($"SUBJECT CHANGED {id} {entSubjects[id]} => {aafSubjects[id]}");
							await subjects.ModifySubjectAsync(db, id, new JsonObject
							{
								["name"] = aafSubjects[id]
							});
						}
					}
					else
					{
						Console.WriteLine($"SUBJECT NEW {id} {aafSubjects[id]}");
						await subjects.CreateSubjectAsync(db, new JsonObject
						{
							["id"] = id,
							["name"] = aafSubjects[id]
						});
					}
				}

				foreach (var id in entSubjects.Keys)
				{
					if (!aafSubjects.ContainsKey(id))
					{
						Console.WriteLine($"SUBJECT REMOVE {id} {entSubjects[id]}");
						await subjects.DeleteSubjectAsync(id);
					}
				}
			}
		}

		public async Task SyncGrades(FileInfo file)
		{
			Console.WriteLine("GRADES SYNCHRONIZE");
			var doc = new XmlDocument();
			doc.XmlResolver = null;
			doc.Load(file.ToString());

			var nodes = doc.SelectNodes("//addRequest");
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

		public async Task SyncStructures(FileInfo file)
		{
			Console.WriteLine("STRUCTURES SYNCHRONIZE");

			var doc = new XmlDocument();
			doc.XmlResolver = null;
			doc.Load(file.ToString());

			var nodes = doc.SelectNodes("//addRequest");
			Console.WriteLine("addRequest count: " + nodes.Count);

			var aafEtabs = new Dictionary<string, Structure>();

			foreach (XmlNode node in nodes)
			{
				var id = node["identifier"]["id"].InnerText;
				List<string> ENTStructureClasses = null;
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
					siren = attrs["ENTStructureSIREN"],
					name = ENTStructureNomCourant,
					address = attrs["street"],
					zip_code = attrs["postalCode"],
					city = attrs["l"],
					phone = attrs["telephoneNumber"],
					fax = attrs["facsimileTelephoneNumber"]
				};
				aafEtabs[attrs["ENTStructureUAI"]].Fields["ENTStructureClasses"] = ENTStructureClasses;
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
							await entStructs[id].DiffWithId(aafEtabs[id]).UpdateAsync(db);
						}

						Console.WriteLine(aafEtabs[id].Fields["ENTStructureClasses"].Dump());
					}
				}

			}

			await Task.Delay(0);
		}
	}
}
