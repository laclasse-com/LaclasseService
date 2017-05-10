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
		Matieres matieres;
//		Niveaux niveaux;

		public Synchronizer(string dbUrl, Matieres matieres, Niveaux niveaux)
		{
			this.dbUrl = dbUrl;
			this.matieres = matieres;
//			this.niveaux = niveaux;
		}

		public async Task Synchronize()
		{
			var dir = new DirectoryInfo(BaseDir);
			Console.WriteLine(dir.GetFiles());
			var files = dir.GetFiles("*_MatiereEducNat_*.xml"); //ENT_0690078K_Complet_20170327_MatiereEducNat_0000.xml
			foreach (var file in files)
				await SyncMatieres(file);

			files = dir.GetFiles("*_MefEducNat_*.xml"); //ENT_0690078K_Complet_20170327_MefEducNat_0000.xml
			foreach (var file in files)
				await SyncNiveaux(file);

			files = dir.GetFiles("*EtabEducNat_*.xml"); //ENT_0690078K_Complet_20170327_EtabEducNat_0000.xml
			foreach (var file in files)
				await SyncStructures(file);
		}

		public async Task SyncMatieres(FileInfo file)
		{
			Console.WriteLine("MATIERES SYNCHRONIZE");

			var doc = new XmlDocument();
			doc.XmlResolver = null;
			doc.Load(file.ToString());

			var nodes = doc.SelectNodes("//addRequest");
			Console.WriteLine("addRequest count: " + nodes.Count);

			var aafMatieres = new Dictionary<string, string>();

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
					aafMatieres[id] = ENTLibelleMatiere;
			}

			var entMatieres = new Dictionary<string, string>();
			using (var db = await DB.CreateAsync(dbUrl))
			{
				var items = await db.SelectAsync("SELECT * FROM matiere");
				foreach (var item in items)
				{
					entMatieres[(string)item["id"]] = (string)item["name"];
				}

				foreach (var id in aafMatieres.Keys)
				{
					if (entMatieres.ContainsKey(id))
					{
						if (entMatieres[id] != aafMatieres[id])
						{
							Console.WriteLine($"MATIERE CHANGED {id} {entMatieres[id]} => {aafMatieres[id]}");
							await matieres.ModifyMatiereAsync(db, id, new JsonObject
							{
								["name"] = aafMatieres[id]
							});
						}
					}
					else
					{
						Console.WriteLine($"MATIERE NEW {id} {aafMatieres[id]}");
						await matieres.CreateMatiereAsync(db, new JsonObject
						{
							["id"] = id,
							["name"] = aafMatieres[id]
						});
					}
				}

				foreach (var id in entMatieres.Keys)
				{
					if (!aafMatieres.ContainsKey(id))
					{
						Console.WriteLine($"MATIERE REMOVE {id} {entMatieres[id]}");
						await matieres.DeleteMatiereAsync(id);
					}
				}
			}
		}

		public async Task SyncNiveaux(FileInfo file)
		{
			Console.WriteLine("NIVEAUX SYNCHRONIZE");
			var doc = new XmlDocument();
			doc.XmlResolver = null;
			doc.Load(file.ToString());

			var nodes = doc.SelectNodes("//addRequest");
			Console.WriteLine("addRequest count: " + nodes.Count);

			var aafNiveaux = new Dictionary<string, Niveau>();

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
					aafNiveaux[id] = new Niveau
					{
						id = id,
						name = ENTLibelleMef,
						rattach = ENTMEFRattach,
						stat = ENTMEFSTAT11
					};
				}
			}

			var entNiveaux = new Dictionary<string, Niveau>();
			using (var db = await DB.CreateAsync(dbUrl))
			{
				var items = await db.SelectAsync<Niveau>("SELECT * FROM niveau");
				foreach (var item in items)
					entNiveaux[item.id] = item;

				foreach (var id in aafNiveaux.Keys)
				{
					if (entNiveaux.ContainsKey(id))
					{
						if (entNiveaux[id] != aafNiveaux[id])
						{
							Console.WriteLine($"NIVEAU CHANGED {id} {entNiveaux[id].name} => {aafNiveaux[id].name}");
							await entNiveaux[id].DiffWithId(aafNiveaux[id]).UpdateAsync(db);
						}
					}
					else
					{
						Console.WriteLine($"NIVEAU NEW {id} {aafNiveaux[id].name}");
						await aafNiveaux[id].InsertAsync(db);
					}
				}

				foreach (var id in entNiveaux.Keys)
				{
					if (!aafNiveaux.ContainsKey(id))
					{
						Console.WriteLine($"NIVEAU REMOVE {id} {entNiveaux[id].name}");
						await entNiveaux[id].DeleteAsync(db);
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
