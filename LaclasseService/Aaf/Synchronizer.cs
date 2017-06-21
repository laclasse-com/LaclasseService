// Synchronizer.cs
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017 Daniel LACROIX
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
using System.Xml;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
using Laclasse.Directory;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace Laclasse.Aaf
{
	public class Synchronizer
	{
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

		Dictionary<int, Structure> structures;
		Dictionary<string, ProfileType> profilesTypes;
		Dictionary<string, Dictionary<string, Group>> structuresGroupsClasses;
		Dictionary<string, Dictionary<string, Group>> structuresGroupsGroupes;
		Dictionary<int, Group> groups;
		Dictionary<string, Subject> subjects;

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

		List<XmlNode> LoadNodes(string zipFile, string regex)
		{
			var nodes = new List<XmlNode>();
			using (var archive = SharpCompress.Archives.ArchiveFactory.Open(zipFile))
			{
				var list = from entry in archive.Entries where System.Text.RegularExpressions.Regex.IsMatch(entry.Key, regex) select entry;
				foreach (var entry in list)
				{
					using (var entryStream = entry.OpenEntryStream())
					{
						var settings = new XmlReaderSettings();
						settings.IgnoreComments = true;
						settings.DtdProcessing = DtdProcessing.Ignore;
						var reader = XmlReader.Create(entryStream, settings);
						var doc = new XmlDocument();
						doc.Load(reader);
						nodes.AddRange(doc.SelectNodes("//addRequest").Cast<XmlNode>());
					}
				}
			}
			return nodes;
		}

		public class SyncStat : Model
		{
			[ModelField]
			public int count { get { return GetField(nameof(count), 0); } set { SetField(nameof(count), value); } }
			[ModelField]
			public double total { get { return GetField<double>("total", 0); } set { SetField("total", value); } }
			[ModelField]
			public double load { get { return GetField<double>("load", 0); } set { SetField("load", value); } }
			[ModelField]
			public double sync { get { return GetField<double>("sync", 0); } set { SetField("sync", value); } }
		}

		public class SyncDiff : Model
		{
			[ModelField]
			public SyncStat stats { get { return GetField<SyncStat>("stats", null); } set { SetField("stats", value); } }
			[ModelField]
			public GradesDiff grades { get { return GetField<GradesDiff>("grades", null); } set { SetField("grades", value); } }
			[ModelField]
			public SubjectsDiff subjects { get { return GetField<SubjectsDiff>("subjects", null); } set { SetField("subjects", value); } }
			[ModelField]
			public StructuresDiff structures { get { return GetField<StructuresDiff>("structures", null); } set { SetField("structures", value); } }
			[ModelField]
			public PersEducNatDiff persEducNat { get { return GetField<PersEducNatDiff>("persEducNat", null); } set { SetField("persEducNat", value); } }
			[ModelField]
			public ElevesDiff eleves { get { return GetField<ElevesDiff>("eleves", null); } set { SetField("eleves", value); } }
			[ModelField]
			public ParentsDiff parents { get { return GetField<ParentsDiff>("parents", null); } set { SetField("parents", value); } }
		}

		public async Task<SyncDiff> Synchronize(
			string file, bool subject = true, bool grade = true,
			bool structure = true, bool persEducNat = true,
			bool eleve = true, bool persRelEleve = true,
			bool apply = true)
		{
			var diff = new SyncDiff();
			diff.stats = new SyncStat();

			var totalWatch = new Stopwatch();
			totalWatch.Start();

			var stopWatch = new Stopwatch();
			stopWatch.Start();

			using (DB db = await DB.CreateAsync(dbUrl, true))
			{

				structures = await LoadEntStructuresAsync(db);
				profilesTypes = await LoadEntProfilesTypesAsync(db);
				structuresGroupsClasses = await LoadEntStructuresGroupsAsync(db, GroupType.CLS);
				structuresGroupsGroupes = await LoadEntStructuresGroupsAsync(db, GroupType.GRP);
				groups = await LoadEntGroupsAsync(db);
				stopWatch.Stop();

				diff.stats.load = stopWatch.Elapsed.TotalSeconds;
			
				if (subject)
				{
					stopWatch = new Stopwatch();
					stopWatch.Start();
					var nodes = LoadNodes(file, @"_MatiereEducNat_\d+.xml$");
					stopWatch.Stop();
					var load = stopWatch.Elapsed.TotalSeconds;
					stopWatch = new Stopwatch();
					stopWatch.Start();
					diff.subjects = await SyncSubjectsAsync(db, nodes, apply);
					stopWatch.Stop();
					diff.subjects.stats.load = load;
					diff.subjects.stats.sync = stopWatch.Elapsed.TotalSeconds;
				}

				if (grade)
				{
					stopWatch = new Stopwatch();
					stopWatch.Start();
					var nodes = LoadNodes(file, @"_MefEducNat_\d+.xml$");
					stopWatch.Stop();
					var load = stopWatch.Elapsed.TotalSeconds;
					stopWatch = new Stopwatch();
					stopWatch.Start();
					diff.grades = await SyncGradesAsync(db, nodes, apply);
					stopWatch.Stop();
					diff.grades.stats.load = load;
					diff.grades.stats.sync = stopWatch.Elapsed.TotalSeconds;
				}

				if (structure)
				{
					stopWatch = new Stopwatch();
					stopWatch.Start();
					var nodes = LoadNodes(file, @"_EtabEducNat_\d+.xml$");
					stopWatch.Stop();
					var load = stopWatch.Elapsed.TotalSeconds;
					stopWatch = new Stopwatch();
					stopWatch.Start();
					diff.structures = await SyncStructuresAsync(db, nodes, apply);
					stopWatch.Stop();
					diff.structures.stats.load = load;
					diff.structures.stats.sync = stopWatch.Elapsed.TotalSeconds;
				}

				if (persEducNat)
				{
					stopWatch = new Stopwatch();
					stopWatch.Start();
					var nodes = LoadNodes(file, @"_PersEducNat_\d+.xml$");
					stopWatch.Stop();
					var load = stopWatch.Elapsed.TotalSeconds;
					stopWatch = new Stopwatch();
					stopWatch.Start();
					diff.persEducNat = await SyncPersEducNatAsync(db, nodes, apply);
					stopWatch.Stop();
					diff.persEducNat.stats.load = load;
					diff.persEducNat.stats.sync = stopWatch.Elapsed.TotalSeconds;
				}

				//var stopWatch = new Stopwatch();
				//stopWatch.Start();
				//stopWatch.Stop();
				//Console.WriteLine($"LOAD PARENTS ELAPSE TIME: {stopWatch.Elapsed.TotalSeconds} s");

				if (eleve)
				{
					diff.parents = new ParentsDiff();
					diff.parents.stats = new SyncStat();

					Dictionary<long, User> aafParents = null;
					stopWatch = new Stopwatch();
					stopWatch.Start();
					aafParents = LoadPersRelEleveEducNatAsync(LoadNodes(file, @"_PersRelEleve_\d+.xml$"));
					stopWatch.Stop();
					diff.parents.stats.load = stopWatch.Elapsed.TotalSeconds;

					stopWatch = new Stopwatch();
					stopWatch.Start();
					var nodes = LoadNodes(file, @"_Eleve_\d+.xml$");
					stopWatch.Stop();
					var load = stopWatch.Elapsed.TotalSeconds;
					stopWatch = new Stopwatch();
					stopWatch.Start();
					diff.eleves = await SyncEleveAsync(db, nodes, persRelEleve, aafParents, apply);
					stopWatch.Stop();
					diff.eleves.stats.load = load;
					diff.eleves.stats.sync = stopWatch.Elapsed.TotalSeconds;
				}

				if (persRelEleve)
				{
					// sync parents profiles
					foreach (var structureItem in structures.Values)
						await SyncStructureParentsProfilesAsync(db, structureItem.id, apply);
				}

				db.Commit();
			}

			totalWatch.Stop();
			diff.stats.total = totalWatch.Elapsed.TotalSeconds;
			return diff;
		}

		public class SubjectsDiff : Model
		{
			[ModelField]
			public SyncStat stats { get { return GetField<SyncStat>("stats", null); } set { SetField("stats", value); } }
			[ModelField]
			public ModelList<Error> errors { get { return GetField<ModelList<Error>>("errors", null); } set { SetField("errors", value); } }
			[ModelField]
			public ModelList<Subject> add { get { return GetField<ModelList<Subject>>("add", null); } set { SetField("add", value); } }
			[ModelField]
			public ModelList<Subject> change { get { return GetField<ModelList<Subject>>("change", null); } set { SetField("change", value); } }
			[ModelField]
			public ModelList<Subject> remove { get { return GetField<ModelList<Subject>>("remove", null); } set { SetField("remove", value); } }
		}

		public async Task<SubjectsDiff> SyncSubjectsAsync(DB db, List<XmlNode> nodes, bool apply)
		{
			Console.WriteLine("SUBJECTS SYNCHRONIZE");

			var diff = new SubjectsDiff();
			diff.stats = new SyncStat();
			diff.errors = new ModelList<Error>();
			diff.add = new ModelList<Subject>();
			diff.change = new ModelList<Subject>();
			diff.remove = new ModelList<Subject>();

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
						var subjectDiff = entSubjects[id].DiffWithId(aafSubjects[id]);
						if (apply)
							await subjectDiff.UpdateAsync(db);
						diff.change.Add(subjectDiff);
					}
				}
				else
				{
					Console.WriteLine($"SUBJECT NEW {id} {aafSubjects[id].name}");
					if (apply)
						await aafSubjects[id].SaveAsync(db);
					diff.add.Add(aafSubjects[id]);
				}
			}

			foreach (var id in entSubjects.Keys)
			{
				if (!aafSubjects.ContainsKey(id))
				{
					Console.WriteLine($"SUBJECT REMOVE {id} {entSubjects[id].name}");
					if (apply)
						await entSubjects[id].DeleteAsync(db);
					diff.remove.Add(entSubjects[id]);
				}
			}

			return diff;
		}

		public class Error : Model
		{
			[ModelField]
			public string message { get { return GetField<string>("message", null); } set { SetField("message", value); } }
		}

		public class GradesDiff : Model
		{
			[ModelField]
			public SyncStat stats { get { return GetField<SyncStat>("stats", null); } set { SetField("stats", value); } }
			[ModelField]
			public ModelList<Error> errors { get { return GetField<ModelList<Error>>("errors", null); } set { SetField("errors", value); } }
			[ModelField]
			public ModelList<Grade> add { get { return GetField<ModelList<Grade>>("add", null); } set { SetField("add", value); } }
			[ModelField]
			public ModelList<Grade> change { get { return GetField<ModelList<Grade>>("change", null); } set { SetField("change", value); } }
			[ModelField]
			public ModelList<Grade> remove { get { return GetField<ModelList<Grade>>("remove", null); } set { SetField("remove", value); } }
		}

		public async Task<GradesDiff> SyncGradesAsync(DB db, List<XmlNode> nodes, bool apply)
		{
			Console.WriteLine("GRADES SYNCHRONIZE");

			var diff = new GradesDiff();
			diff.stats = new SyncStat();
			diff.errors = new ModelList<Error>();
			diff.add = new ModelList<Grade>();
			diff.change = new ModelList<Grade>();
			diff.remove = new ModelList<Grade>();

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
					// only take "l'école primaire et secondaire". They starts with
					// 0 and 1
					if (id.StartsWith("0", StringComparison.InvariantCulture) ||
					    id.StartsWith("1", StringComparison.InvariantCulture))
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
			}

			var entGrades = new Dictionary<string, Grade>();

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
						var gradeDiff = entGrades[id].DiffWithId(aafGrades[id]);
						if (apply)
							await gradeDiff.UpdateAsync(db);
						diff.change.Add(gradeDiff);
					}
				}
				else
				{
					Console.WriteLine($"GRADE NEW {id} {aafGrades[id].name}");
					if (apply)
						await aafGrades[id].SaveAsync(db);
					diff.add.Add(aafGrades[id]);
				}
			}

			foreach (var id in entGrades.Keys)
			{
				if (!aafGrades.ContainsKey(id))
				{
					Console.WriteLine($"GRADE REMOVE {id} {entGrades[id].name}");
					if (apply)
						await entGrades[id].DeleteAsync(db);
					diff.remove.Add(entGrades[id]);
				}
			}

			return diff;
		}

		public class GroupsDiff : Model
		{
			[ModelField]
			public ModelList<Group> add { get { return GetField<ModelList<Group>>("add", null); } set { SetField("add", value); } }
			[ModelField]
			public ModelList<Group> change { get { return GetField<ModelList<Group>>("change", null); } set { SetField("change", value); } }
			[ModelField]
			public ModelList<Group> remove { get { return GetField<ModelList<Group>>("remove", null); } set { SetField("remove", value); } }
		}


		public class StructuresDiff : Model
		{
			[ModelField]
			public SyncStat stats { get { return GetField<SyncStat>("stats", null); } set { SetField("stats", value); } }
			[ModelField]
			public ModelList<Error> errors { get { return GetField<ModelList<Error>>("errors", null); } set { SetField("errors", value); } }
			[ModelField]
			public ModelList<Structure> change { get { return GetField<ModelList<Structure>>("change", null); } set { SetField("change", value); } }
			[ModelField]
			public GroupsDiff groups { get { return GetField<GroupsDiff>("groups", null); } set { SetField("groups", value); } }
		}

		public async Task<StructuresDiff> SyncStructuresAsync(DB db, List<XmlNode> nodes, bool apply)
		{
			Console.WriteLine("STRUCTURES SYNCHRONIZE");

			var diff = new StructuresDiff();
			diff.stats = new SyncStat();
			diff.errors = new ModelList<Error>();
			diff.change = new ModelList<Structure>();
			diff.groups = new GroupsDiff();
			diff.groups.add = new ModelList<Group>();
			diff.groups.change = new ModelList<Group>();
			diff.groups.remove = new ModelList<Group>();

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
				ENTStructureNomCourant = System.Text.RegularExpressions.Regex.Replace(ENTStructureNomCourant, $"-ac-{ENTServAcAcademie}$", "");

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
			foreach (var item in structures.Values)
				entStructs[item.id] = item;
			diff.stats.count = entStructs.Values.Count;
			Console.WriteLine($"ETABS ENT COUNT: {entStructs.Count}");

			foreach (var id in aafEtabs.Keys)
			{
				if (entStructs.ContainsKey(id))
				{
					if (!entStructs[id].EqualsIntersection(aafEtabs[id]))
					{
						Console.WriteLine($"STRUCTURES CHANGED {id} {entStructs[id].name} => {aafEtabs[id].name}");
						var itemDiff = entStructs[id].DiffWithId(aafEtabs[id]);
						itemDiff.aaf_mtime = DateTime.Now;
						if (apply)
							await itemDiff.UpdateAsync(db);
						diff.change.Add(itemDiff);
					}

					// check structure groups
					var structGroups = await entStructs[id].GetGroupsAsync(db);
					var entClasses = new Dictionary<string, Group>();
					var entGroupes = new Dictionary<string, Group>();
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
					var aafClasses = new Dictionary<string, Group>();
					foreach (var aafClasse in (List<string>)aafEtabs[id].Fields["ENTStructureClasses"])
					{
						if (string.IsNullOrEmpty(aafClasse))
							continue;

						var tab = aafClasse.Split('$');
						if (tab.Length < 3)
							throw new Exception($"For structure {id} Invalid classes define {aafClasse}");
						var classe = new Group
						{
							aaf_name = tab[0],
							structure_id = id,
							name = tab[0],
							description = string.IsNullOrEmpty(tab[1]) ? null : tab[1]
						};
						classe.Fields["grades"] = tab.Skip(2);
						aafClasses[classe.aaf_name] = classe;
					}

					foreach (var classeId in aafClasses.Keys)
					{
						var aafClasse = aafClasses[classeId];
						Group entClasse = null;
						if (entClasses.ContainsKey(classeId))
						{
							entClasse = entClasses[classeId];
							if (!entClasse.EqualsIntersection(aafClasse))
							{
								Console.WriteLine($"CLASSE CHANGED {classeId} {entClasse.name} => {aafClasse.name}");
								var itemDiff = entClasse.DiffWithId(aafClasse);
								itemDiff.aaf_mtime = DateTime.Now;
								if (apply)
								{
									await itemDiff.UpdateAsync(db);
									await entClasse.LoadAsync(db);
								}
								diff.groups.change.Add(itemDiff);
							}
						}
						else
						{
							Console.WriteLine($"CLASSE NEW {classeId} {aafClasse.name}");
							aafClasse.type = "CLS";
							aafClasse.aaf_mtime = DateTime.Now;
							if (apply)
								await aafClasse.SaveAsync(db);
							entClasse = aafClasse;
							diff.groups.add.Add(aafClasse);
						}

						// synchronize the group's grades
						await entClasse.LoadExpandFieldAsync(db, nameof(entClasse.grades));
						var aafClasseGrades = (IEnumerable<string>)aafClasses[classeId].Fields["grades"];

						foreach (var classeGrade in entClasse.grades)
						{
							if (!aafClasseGrades.Contains(classeGrade.grade_id))
							{
								Console.WriteLine($"CLASSE {classeId} REMOVE GRADE {classeGrade.grade_id}");
								if (apply)
									await classeGrade.DeleteAsync(db);
								// TODO: notify a group change
							}
						}

						foreach (var gradeId in aafClasseGrades)
						{
							var foundGrade = entClasse.grades.FirstOrDefault((arg) => arg.grade_id == gradeId);
							if (foundGrade == null)
							{
								Console.WriteLine($"CLASSE {classeId} ADD GRADE {gradeId}");
								var newGroupGrade = new GroupGrade
								{
									group_id = entClasse.id,
									grade_id = gradeId
								};
								if (apply)
									await newGroupGrade.InsertAsync(db);
								// TODO: notify a group change
							}
						}
					}

					foreach (var classeId in entClasses.Keys)
					{
						if (!aafClasses.ContainsKey(classeId))
						{
							Console.WriteLine($"CLASSE REMOVE {classeId} {entClasses[classeId].name}");
							// remove the "classe"
							if (apply)
								await entClasses[classeId].DeleteAsync(db);
							diff.groups.remove.Add(entClasses[classeId]);
						}
					}

					// handle GROUPES ELEVES
					var aafGroupes = new Dictionary<string, Group>();
					foreach (var aafGroupe in (List<string>)aafEtabs[id].Fields["ENTStructureGroupes"])
					{
						if (string.IsNullOrEmpty(aafGroupe))
							continue;

						var tab = aafGroupe.Split('$');
						if (tab.Length < 2)
							throw new Exception($"For structure {id} invalid groupe define {aafGroupe}");
						var groupe = new Group
						{
							aaf_name = tab[0],
							structure_id = id,
							name = tab[0],
							description = string.IsNullOrEmpty(tab[1]) ? null : tab[1]
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
								var itemDiff = entGroupes[groupeId].DiffWithId(aafGroupes[groupeId]);
								itemDiff.aaf_mtime = DateTime.Now;
								if (apply)
									await itemDiff.UpdateAsync(db);
								diff.groups.change.Add(itemDiff);
							}
						}
						else
						{
							Console.WriteLine($"GROUPE NEW {groupeId} {aafGroupes[groupeId].name}");
							aafGroupes[groupeId].type = "GRP";
							aafGroupes[groupeId].aaf_mtime = DateTime.Now;
							if (apply)
								await aafGroupes[groupeId].InsertAsync(db);
							diff.groups.add.Add(aafGroupes[groupeId]);
						}
					}

					foreach (var groupeId in entGroupes.Keys)
					{
						if (!aafGroupes.ContainsKey(groupeId))
						{
							Console.WriteLine($"GROUPE REMOVE {groupeId} {entGroupes[groupeId].name}");
							if (apply)
								await entGroupes[groupeId].DeleteAsync(db);
							diff.groups.remove.Add(entGroupes[groupeId]);
						}
					}
				}
			}
			return diff;
		}

		public async Task<Dictionary<int, Structure>> LoadEntStructuresAsync(DB db)
		{
			var entStructures = new Dictionary<int, Structure>();

			var items = await db.SelectAsync<Structure>("SELECT * FROM `structure` WHERE `aaf_jointure_id` IS NOT NULL AND `aaf_sync_activated`=TRUE");
			foreach (var item in items)
				entStructures[(int)item.aaf_jointure_id] = item;
			
			return entStructures;
		}

		public async Task<Dictionary<string, ProfileType>> LoadEntProfilesTypesAsync(DB db)
		{
			var entProfilesTypes = new Dictionary<string, ProfileType>();

			var items = await db.SelectAsync<ProfileType>("SELECT * FROM `profile_type`");
			foreach (var item in items)
				entProfilesTypes[item.id] = item;
			return entProfilesTypes;
		}

		public async Task<Dictionary<int, Group>> LoadEntGroupsAsync(DB db)
		{
			var entGroups = new Dictionary<int, Group>();

			var items = await db.SelectAsync<Group>("SELECT * FROM `group` WHERE `aaf_name` IS NOT NULL");
			foreach (var item in items)
				entGroups[item.id] = item;
			
			return entGroups;
		}

		public enum GroupType
		{
			CLS,
			GRP
		}

		public async Task<Dictionary<string, Dictionary<string,Group>>> LoadEntStructuresGroupsAsync(DB db, GroupType type)
		{
			var entStructuresGroups = new Dictionary<string, Dictionary<string,Group>>();
			string currentStructId = null;
			Dictionary<string, Group> currentStructGroups = null;

			var items = await db.SelectAsync<Group>("SELECT * FROM `group` WHERE `aaf_name` IS NOT NULL AND `structure_id` IS NOT NULL AND `type`=? ORDER BY `structure_id`", type.ToString());
			foreach (var item in items)
			{
				if (currentStructId != item.structure_id)
				{
					currentStructGroups = new Dictionary<string, Group>();
					currentStructId = item.structure_id;
					entStructuresGroups[currentStructId] = currentStructGroups;
				}
				currentStructGroups[item.aaf_name] = item;
			}

			return entStructuresGroups;
		}

		public Dictionary<long,User> LoadPersRelEleveEducNatAsync(List<XmlNode> nodes)
		{
			var aafParents = new Dictionary<long, User>();
			var mulFields = new string[] { "mobile", "mail" };

			long id;
			Dictionary<string, string> attrs;
			Dictionary<string, List<string>> attrsMul;

			foreach (XmlNode node in nodes)
			{
				ReadAttributes(node, mulFields, out id, out attrs, out attrsMul);
				var aafParent = AttributesToUser(id, attrs, attrsMul);
				aafParent.Fields["attrs"] = attrs;
				aafParent.Fields["attrsMul"] = attrsMul;
				aafParents[id] = aafParent;
			}
			return aafParents;
		}

		async Task<Subject> GetOrCreateSubjectAsync(DB db, string id)
		{
			Subject subject = null;
			if (subjects == null)
			{
				subjects = new Dictionary<string, Subject>();
				foreach (var sub in await db.SelectAsync<Subject>("SELECT * FROM subject"))
					subjects[sub.id] = sub;
			}
			if (subjects.ContainsKey(id))
				subject = subjects[id];
			else
			{
				subject = new Subject { id = id };
				await subject.SaveAsync(db);
				subjects[id] = subject;
			}
			return subject;
		}

		Group GetGroupByAaf(int aaf_structure_id, string aaf_name, GroupType type)
		{
			Group group = null;
			if (structures.ContainsKey(aaf_structure_id))
			{
				var structure = structures[aaf_structure_id];
				if (type == GroupType.CLS)
				{
					if (structuresGroupsClasses.ContainsKey(structure.id))
					{
						var structGroups = structuresGroupsClasses[structure.id];
						if (structGroups.ContainsKey(aaf_name))
							group = structGroups[aaf_name];
					}
				}
				else
				{
					if (structuresGroupsGroupes.ContainsKey(structure.id))
					{
						var structGroups = structuresGroupsGroupes[structure.id];
						if (structGroups.ContainsKey(aaf_name))
							group = structGroups[aaf_name];
					}
				}
			}
			return group;
		}

		public class PersEducNatDiff : Model
		{
			[ModelField]
			public SyncStat stats { get { return GetField<SyncStat>("stats", null); } set { SetField("stats", value); } }
			[ModelField]
			public ModelList<Error> errors { get { return GetField<ModelList<Error>>("errors", null); } set { SetField("errors", value); } }
			[ModelField]
			public ModelList<User> add { get { return GetField<ModelList<User>>("add", null); } set { SetField("add", value); } }
			[ModelField]
			public ModelList<User> change { get { return GetField<ModelList<User>>("change", null); } set { SetField("change", value); } }
			[ModelField]
			public ModelList<User> remove { get { return GetField<ModelList<User>>("remove", null); } set { SetField("remove", value); } }
		}

		public async Task<PersEducNatDiff> SyncPersEducNatAsync(DB db, List<XmlNode> nodes, bool apply)
		{
			Console.WriteLine("PERSEDUCNAT SYNCHRONIZE");

			var mulFields = new string[] {
				"ENTAuxEnsClassesMatieres", "ENTAuxEnsGroupesMatieres", "ENTPersonFonctions"
			};

			var diff = new PersEducNatDiff();
			diff.stats = new SyncStat();
			diff.stats.count = 0;
			diff.errors = new ModelList<Error>();
			diff.add = new ModelList<User>();
			diff.change = new ModelList<User>();
			diff.remove = new ModelList<User>();

			var structuresIds = structures.Values.Select((arg) => arg.id);

			foreach (XmlNode node in nodes)
			{
				var id = Convert.ToInt64(node["identifier"]["id"].InnerText);

				var ENTPersonFonctions = new Dictionary<int, string>();

				Dictionary<string, string> attrs;
				Dictionary<string, List<string>> attrsMul;
				ReadAttributes(node, mulFields, out id, out attrs, out attrsMul);
				var aafUser = AttributesToUser(id, attrs, attrsMul);

				if (attrsMul.ContainsKey("ENTPersonFonctions"))
				{
					foreach (var fonction in attrsMul["ENTPersonFonctions"])
					{
						var tab = fonction.Split('$');
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

				var entUser = (await db.SelectAsync<User>("SELECT * FROM `user` WHERE `aaf_jointure_id`=?", id)).SingleOrDefault();

				// if user not found, try to find the user by its email
				if ((entUser == null) && !string.IsNullOrWhiteSpace(attrs["mail"]))
				{
					entUser = (await db.SelectAsync<User>(
						"SELECT * FROM `user` WHERE `aaf_jointure_id` IS NULL AND id IN "+
						"(SELECT `user_id` FROM `email` WHERE `address` LIKE ? AND `type`='Academique')", attrs["mail"])).SingleOrDefault();
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
						if (apply)
							await aafUser.SaveAsync(db);
						diff.add.Add(aafUser);
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
							if (apply)
								await email.SaveAsync(db);
						}
						// create a user "Ent" email (the default for teachers)
						if (apply)
							await aafUser.CreateDefaultEntEmailAsync(db);
						
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
							if (apply)
								await profile.SaveAsync(db);
						}
						// create the user groups
						if (apply)
							await SyncUserGroups(db, aafUser, attrsMul["ENTAuxEnsClassesMatieres"],
							                         attrsMul["ENTAuxEnsGroupesMatieres"]);
					}
				}
				else
				{
					if (!entUser.EqualsIntersection(aafUser))
					{
						Console.WriteLine($"EDUCNATPERS CHANGED {id} {entUser.firstname} {entUser.lastname} => {aafUser.firstname} {aafUser.lastname}");
						var itemDiff = entUser.DiffWithId(aafUser);

						if (apply)
							await itemDiff.UpdateAsync(db);
						diff.change.Add(itemDiff);
					}
					// synchronize fonctions/profiles
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
					await SyncUserProfiles(db, await entUser.GetProfilesAsync(db), aafProfiles, structuresIds, apply);

					// synchronize "classes" and "groupe eleve"
					if (apply)
						await SyncUserGroups(db, entUser, attrsMul["ENTAuxEnsClassesMatieres"],
						                     attrsMul["ENTAuxEnsGroupesMatieres"]);
				}
			}
			// TODO: remove all profiles managed by the AAF but not seen by the synchronization for the AAF structures
			// IDEA: always update aaf_mtime in a synchronisation and use the the aaf_mtime to remove old link

			return diff;
		}

		async Task SyncUserGroups(DB db, User user, List<string> aafClasses,
		                          List<string> aafGroupes)
		{
			// get the ENT groups handle by the AAF
			var entGroups = from userGroup in (await user.GetGroupsAsync(db))
					where groups.ContainsKey(userGroup.group_id) select userGroup;
			
			// convert to GroupUser list
			var aafGroups = new ModelList<GroupUser>();
			foreach (var classe in aafClasses)
			{
				var tab = classe.Split('$');
				if (tab.Length == 2)
				{
					var group = GetGroupByAaf(Convert.ToInt32(tab[0]), tab[1], GroupType.CLS);
					if (group != null)
					{
						aafGroups.Add(new GroupUser
						{
							type = "ELV",
							user_id = user.id,
							group_id = group.id,
							pending_validation = false
						});
					}
				}
				else if (tab.Length == 3)
				{
					var group = GetGroupByAaf(Convert.ToInt32(tab[0]), tab[1], GroupType.CLS);
					//Console.WriteLine($"AAF CLASSE: {classe}, GROUP: {group}");

					if (group != null)
					{
						// ensure the subject exists. Some subject can be used by teacher
						// but not given in the AAF
						var sub = await GetOrCreateSubjectAsync(db, tab[2]);

						aafGroups.Add(new GroupUser
						{
							type = "ENS",
							user_id = user.id,
							group_id = group.id,
							subject_id = sub.id,
							pending_validation = false
						});
					}
				}
			}
			foreach (var groupe in aafGroupes)
			{
				var tab = groupe.Split('$');
				if (tab.Length == 2)
				{
					var group = GetGroupByAaf(Convert.ToInt32(tab[0]), tab[1], GroupType.GRP);
					if (group != null)
					{
						aafGroups.Add(new GroupUser
						{
							type = "ELV",
							user_id = user.id,
							group_id = group.id,
							pending_validation = false
						});
					}
				}
				else if (tab.Length == 3)
				{
					var group = GetGroupByAaf(Convert.ToInt32(tab[0]), tab[1], GroupType.GRP);
					if (group != null)
					{
						var sub = await GetOrCreateSubjectAsync(db, tab[2]);

						aafGroups.Add(new GroupUser
						{
							type = "ENS",
							user_id = user.id,
							group_id = group.id,
							subject_id = sub.id,
							pending_validation = false
						});
					}
				}
			}
			await Model.SyncAsync(db, entGroups, aafGroups);
		}

		async Task SyncUserProfiles(DB db, ModelList<UserProfile> entProfiles, ModelList<UserProfile> aafProfiles,
		                            IEnumerable<string> structuresIds, bool apply)
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
					if (apply)
						await aafProfile.SaveAsync(db);
				}
				else if (entProfile.aaf_mtime == null)
				{
					Console.WriteLine("CONVERT TO AAF PROFILE: " + aafProfile.type);
					entProfile.aaf_mtime = DateTime.Now;
					if (apply)
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
					if (apply)
						await entProfile.DeleteAsync(db);
				}
			}
		}

		public class ParentsDiff : Model
		{
			[ModelField]
			public SyncStat stats { get { return GetField<SyncStat>("stats", null); } set { SetField("stats", value); } }
		}

		public class ElevesDiff : Model
		{
			[ModelField]
			public SyncStat stats { get { return GetField<SyncStat>("stats", null); } set { SetField("stats", value); } }
			[ModelField]
			public ModelList<Error> errors { get { return GetField<ModelList<Error>>("errors", null); } set { SetField("errors", value); } }
			[ModelField]
			public ModelList<User> add { get { return GetField<ModelList<User>>("add", null); } set { SetField("add", value); } }
			[ModelField]
			public ModelList<User> change { get { return GetField<ModelList<User>>("change", null); } set { SetField("change", value); } }
			[ModelField]
			public ModelList<User> remove { get { return GetField<ModelList<User>>("remove", null); } set { SetField("remove", value); } }
		}

		public async Task<ElevesDiff> SyncEleveAsync(DB db, List<XmlNode> nodes, bool persRelEleve, Dictionary<long, User> aafParents, bool apply)
		{
			Console.WriteLine("ELEVE SYNCHRONIZE");

			var mulFields = new string[] {
				"ENTEleveClasses", "ENTEleveGroupes", "ENTEleveCodeEnseignements",
				"ENTEleveEnseignements", "ENTPersonAutresPrenoms", "ENTElevePersRelEleve" };

			var diff = new ElevesDiff();
			diff.stats = new SyncStat();
			diff.errors = new ModelList<Error>();
			diff.add = new ModelList<User>();
			diff.change = new ModelList<User>();
			diff.remove = new ModelList<User>();

			var structuresIds = structures.Values.Select((arg) => arg.id);

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
				
				var entUser = (await db.SelectAsync<User>("SELECT * FROM `user` WHERE `aaf_jointure_id`=?", id)).SingleOrDefault();

				// if user not found, try to find the user by its aaf_struct_rattach_id
				if ((entUser == null) && (aafUser.aaf_struct_rattach_id != null))
				{
					entUser = (await db.SelectAsync<User>(
						"SELECT * FROM `user` WHERE `aaf_struct_rattach_id`=?", aafUser.aaf_struct_rattach_id)).SingleOrDefault();
				}
				// try to find the Eleve with other attributes (firstname, lastname, birthdate)
				if ((entUser == null) && !string.IsNullOrWhiteSpace(aafUser.firstname) &&
				        !string.IsNullOrWhiteSpace(aafUser.lastname) &&
				        (aafUser.birthdate != null))
				{
					entUser = (await db.SelectAsync<User>(
						"SELECT * FROM `user` WHERE `aaf_jointure_id` IS NULL AND `firstname`=? " +
						"AND `lastname`=? AND `birthdate`=?", aafUser.firstname, aafUser.lastname,
						aafUser.birthdate)).SingleOrDefault();
				}

				if (entUser == null)
				{
					Console.WriteLine($"ELEVE NEW {aafUser.firstname} {aafUser.lastname}");
					// create the user
					if(apply)
						await aafUser.SaveAsync(db);
					entUser = aafUser;
					// create a user "Ent" email (the default for students)
					if (apply)
						await entUser.CreateDefaultEntEmailAsync(db);
					
					// create the user profiles
					var profile = new UserProfile
					{
						user_id = aafUser.id,
						type = "ELV",
						structure_id = structures[ENTPersonStructRattach].id,
						aaf_mtime = DateTime.Now
					};
					if (apply)
						await profile.SaveAsync(db);
					
					// create the user groups
					if (apply)
						await SyncUserGroups(db, aafUser, attrsMul["ENTEleveClasses"], attrsMul["ENTEleveGroupes"]);
					diff.add.Add(entUser);
				}
				else
				{
					if (!entUser.EqualsIntersection(aafUser))
					{
						Console.WriteLine($"ELEVE CHANGED {id} {entUser.firstname} {entUser.lastname} => {aafUser.firstname} {aafUser.lastname}");
						var userDiff = entUser.DiffWithId(aafUser);
						if (apply)
						{
							await userDiff.UpdateAsync(db);
							await entUser.LoadAsync(db);
						}
						diff.change.Add(userDiff);
					}
					// synchronize fonctions/profiles
					var aafProfiles = new ModelList<UserProfile>();
					aafProfiles.Add(new UserProfile 
					{
						user_id = entUser.id,
						type = "ELV",
						structure_id = structures[ENTPersonStructRattach].id,
						aaf_mtime = DateTime.Now
					});
					await SyncUserProfiles(db, await entUser.GetProfilesAsync(db), aafProfiles, structuresIds, apply);

					// synchronize "classes" and "groupe eleve"
					if (apply)
						await SyncUserGroups(db, entUser, attrsMul["ENTEleveClasses"], attrsMul["ENTEleveGroupes"]);
				}

				// handle parents
				if (persRelEleve && attrsMul.ContainsKey("ENTElevePersRelEleve"))
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
							var entParent = (await db.SelectAsync<User>("SELECT * FROM `user` WHERE `aaf_jointure_id`=?", parent_aaf_jointure_id)).SingleOrDefault();
							// if parent not found, need to create it
							// TODO: try to find the parent with other attributes
							if (entParent == null)
							{
								if (apply)
									await aafParent.SaveAsync(db);
								entParent = aafParent;
							}
							else
							{
								if (!entParent.EqualsIntersection(aafParent))
								{
									Console.WriteLine($"PARENT CHANGED {parent_aaf_jointure_id} {entParent.firstname} {entParent.lastname} => {aafParent.firstname} {aafParent.lastname}");
									var userDiff = entParent.DiffWithId(aafParent);
									if (apply)
									{
										await userDiff.UpdateAsync(db);
										await entParent.LoadAsync(db);
									}
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
							if (apply)
								await Model.SyncAsync(db, await entParent.GetPhonesAsync(db), aafParentPhones);
							
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
							if (apply)
								await Model.SyncAsync(db, await entParent.GetEmailsAsync(db), aafParentEmails);
							
							aafUserChilds.Add(new UserChild
							{
								type = aafRelationType[int.Parse(tab[1])],
								parent_id = entParent.id,
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
					if (apply)
						await Model.SyncAsync(db, await entUser.GetParentsAsync(db), aafUserChilds);
				}
			}
			// TODO: remove all profiles managed by the AAF but not seen by the synchronization for the AAF structures
			// IDEA: always update aaf_mtime in a synchronisation and use the the aaf_mtime to remove old link

			// TODO: remove all groups managed by the AAF but not seen by the synchronization for the AAF structures

			return diff;
		}

		/// <summary>
		/// Syncs the structure parents profiles async.
		/// Use the relation between child and parent to find which profiles
		/// parents shoud have
		/// </summary>
		/// <param name="uai">The structure UAI code</param>
		async Task SyncStructureParentsProfilesAsync(DB db, string uai, bool apply)
		{
			var expectParents = new ModelList<UserProfile>();
			foreach (var item in await db.SelectAsync(
				"SELECT DISTINCT(`parent_id`) FROM `user_child` WHERE `child_id` IN " +
				"(SELECT `user_id` FROM `user_profile` WHERE `type`= 'ELV' AND `structure_id`= ?)", uai))
				expectParents.Add(new UserProfile { user_id = (string)item["parent_id"], structure_id = uai, type = "TUT" });

			var entParents = await db.SelectAsync<UserProfile>("SELECT * FROM `user_profile` WHERE `type`='TUT' AND `structure_id`=?", uai);

			if (apply)
				await Model.SyncAsync(db, entParents, expectParents);
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
						if (!string.IsNullOrEmpty(childNode.InnerText))
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

			if (attrs.ContainsKey("ENTEleveMEF"))
				user.student_grade_id = attrs["ENTEleveMEF"];

			// normalize the firstname. Lower the givenName and capitalize the first letters
			if (attrs.ContainsKey("givenName"))
				user.firstname = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(attrs["givenName"].ToLower());

			if (attrs.ContainsKey("sn"))
				user.lastname = attrs["sn"];

			return user;
		}
	}
}
