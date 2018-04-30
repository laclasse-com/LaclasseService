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
	public enum GroupType
	{
		CLS,
		GRP
	}

	public class Synchronizer
	{
		static readonly object globalLock = new object();
		readonly string dbUrl;
		readonly SyncFile syncFile;
		readonly AafGlobalZipFile zipFile;
		readonly List<string> structuresIds = null;
		int tutUserIdGenerator = 0;
		int ensUserIdGenerator = 0;
		int elvUserIdGenerator = 0;
		int groupIdGenerator = 0;
		readonly List<string> errors = new List<string>();

		/// <summary>
		/// Convert user to user link type from the AAF 2D id to the ENT id.
		/// See: http://infocentre.pleiade.education.fr/bcn/workspace/viewTable/n/N_LIEN_ELEVE_RESPONSABLE
		/// </summary>
		public readonly static Dictionary<int, string> aafRelationType = new Dictionary<int, string>
		{
			[1] = "PERE", // PERE
			[2] = "MERE", // MERE
			[3] = "TUTEUR", // TUTEUR
			[4] = "A_MMBR", // AUTRE MEMBRE DE LA FAMILLE
			[5] = "DDASS", // AIDE SOCIALE A L'ENFANCE
			[6] = "A_CAS", // AUTRE CAS
			//[7] = "ELEVE", // ELEVE LUI_MEME

			[20] = "PERE", // PERE
			[10] = "MERE", // MERE
			[50] = "TUTEUR", // TUTEUR
			[39] = "A_MMBR", // AUTRE MEMBRE DE LA FAMILLE
			[51] = "DDASS", // AIDE SOCIALE A L'ENFANCE
			[90] = "A_CAS", // AUTRE LIEN
			//[70] = "ELEVE", // ELEVE LUI MEME

			//[37] = "FRATRIE", // FRATRIE
			[38] = "ASCENDANT", // ASCENDANT
			[41] = "EDUCATEUR",  // EDUCATEUR
			[42] = "ASS. FAMIL", // ASSISTANT FAMILIAL
			[43] = "GARDE ENF.", // GARDE d'ENFANT
		};

		/// <summary>
		/// Convert an AAF fonction to the ENT profile
		/// </summary>
		public readonly static Dictionary<string, string> aafFonctionToProfile = new Dictionary<string, string>
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

		public readonly static string[] persEducNatProfiles = { "ACA", "DIR", "DOC", "ENS", "ETA", "EVS" };

		public static string[] userMulFields = {
			"ENTAuxEnsClassesPrincipal", "ENTAuxEnsClassesMatieres", "ENTAuxEnsGroupesMatieres", "ENTPersonFonctions",
			"ENTEleveClasses", "ENTEleveGroupes", "ENTEleveCodeEnseignements",
			"ENTEleveEnseignements", "ENTPersonAutresPrenoms", "ENTElevePersRelEleve",
			"mobile", "mail"
		};

		ModelList<Structure> aafStructures;
		ModelList<Structure> entStructures;
		Dictionary<int, Structure> aafStructuresByAafId;
		Dictionary<int, Structure> entStructuresByAafId;
		Dictionary<string, Structure> entStructuresById;

		// all users seen in a given synchronization
		Dictionary<string, User> entSyncSeenUsers;
		IEnumerable<string> interStructuresIds;
		Dictionary<string,bool> interStructures;
		Dictionary<int, Group> aafGroupsById;
		Dictionary<int, Group> entGroupsById;
		Dictionary<string, Group> aafGroupByTypeStructureAafName;
		Dictionary<string, Group> entGroupByTypeStructureAafName;

		ModelList<Grade> aafGrades;
		Dictionary<string, Grade> aafGradesById;

		ModelList<Grade> entGrades;
		Dictionary<string, Grade> entGradesById;
		Dictionary<string, Grade> entGradesUsedById;

		ModelList<Subject> aafSubjects;
		Dictionary<string, Subject> aafSubjectsById;

		ModelList<Subject> entSubjects;
		Dictionary<string, Subject> entSubjectsById;
		Dictionary<string, Subject> entSubjectsUsedById;

		ModelList<User> aafParents;
		Dictionary<long, User> aafParentsByAafId;

		ModelList<User> aafStudents;

		Dictionary<string, User> aafParentsById = null;

		Dictionary<int, User> entUsersByAafId = null;
		Dictionary<string, User> entUsersByAcademicEmail = null;
		Dictionary<string, User> entUsersByNameBirthdate = null;
		Dictionary<int, User> entUsersByStructRattachId = null;

		public Synchronizer(string dbUrl, SyncFile file, List<string> structuresIds = null)
		{
			this.dbUrl = dbUrl;
			syncFile = file;
			zipFile = new AafGlobalZipFile(file.FullPath);
			this.structuresIds = structuresIds;
		}

		public class SyncFile : Model
		{
			string zipFolder;

			public SyncFile(string zipFolder)
			{
				this.zipFolder = zipFolder;
			}

			public string FullPath
			{
				get
				{
					return Path.Combine(this.zipFolder, id);
				}
			}

			[ModelField]
			public string id { get { return GetField<string>(nameof(id), null); } set { SetField(nameof(id), value); } }
			[ModelField]
			public DateTime? date { get { return GetField<DateTime?>(nameof(date), null); } set { SetField(nameof(date), value); } }
			[ModelField]
			public long size { get { return GetField<long>(nameof(size), 0); } set { SetField(nameof(size), value); } }
			[ModelField]
			public SyncFileFormat format { get { return GetField(nameof(format), SyncFileFormat.Full); } set { SetField(nameof(format), value); } }
		}

		public static SyncFile GetFile(string syncFilesFolder, string zipFilesFolder, string fileName)
		{
			var files = GetFiles(syncFilesFolder, zipFilesFolder);
			return files.Find((obj) => obj.id == fileName);
		}

		public static ModelList<SyncFile> GetFiles(string syncFilesFolder, string zipFilesFolder)
		{
			var result = new ModelList<SyncFile>();
			var dir = new DirectoryInfo(syncFilesFolder);
			var zipDir = new DirectoryInfo(zipFilesFolder);

			lock(globalLock)
			{
				// convert TGZ to ZIP if not already done
				foreach (var file in dir.EnumerateFiles("*.tgz"))
				{
					var zipFile = Path.Combine(zipDir.FullName, file.Name.Substring(0, file.Name.Length - 4) + ".zip");
					if (!File.Exists(zipFile))
						ConvertToZip(file.FullName, zipFile);
				}

				foreach (var file in zipDir.EnumerateFiles("*.zip"))
				{
					var syncFile = new SyncFile (zipFilesFolder) { id = file.Name, size = file.Length };
					var matches = System.Text.RegularExpressions.Regex.Match(file.Name, "ENT2D\\.(20\\d\\d)(\\d\\d)(\\d\\d).*\\.zip");
					if (matches.Success)
						syncFile.date = new DateTime(
							Convert.ToInt32(matches.Groups[1].Value),
							Convert.ToInt32(matches.Groups[2].Value),
							Convert.ToInt32(matches.Groups[3].Value));
					if (System.Text.RegularExpressions.Regex.IsMatch(file.Name, "Delta"))
						syncFile.format = SyncFileFormat.Delta;
					else 
						syncFile.format = SyncFileFormat.Full;
					result.Add(syncFile);
				}
			}

			return result;
		}

		public static void ConvertToZip(string file, string destination)
		{
			using (var destStream = File.OpenWrite(destination))
			using (var writer = WriterFactory.Open(destStream, ArchiveType.Zip, new WriterOptions(CompressionType.Deflate)))
			using (Stream stream = File.OpenRead(file))
			using (var reader = ReaderFactory.Open(stream))
			{
				while (reader.MoveToNextEntry())
				{
					if (reader.Entry.IsDirectory)
						continue;

					using (var readerStream = reader.OpenEntryStream())
						writer.Write(reader.Entry.Key, readerStream);
				}
			}
		}

		public class SyncStat : Model
		{
			[ModelField]
			public int addCount { get { return GetField(nameof(addCount), 0); } set { SetField(nameof(addCount), value); } }
			[ModelField]
			public int changeCount { get { return GetField(nameof(changeCount), 0); } set { SetField(nameof(changeCount), value); } }
			[ModelField]
			public int removeCount { get { return GetField(nameof(removeCount), 0); } set { SetField(nameof(removeCount), value); } }
			[ModelField]
			public double total { get { return GetField<double>(nameof(total), 0); } set { SetField(nameof(total), value); } }
			[ModelField]
			public double load { get { return GetField<double>(nameof(load), 0); } set { SetField(nameof(load), value); } }
			[ModelField]
			public double entLoad { get { return GetField<double>(nameof(entLoad), 0); } set { SetField(nameof(entLoad), value); } }
			[ModelField]
			public double aafLoad { get { return GetField<double>(nameof(aafLoad), 0); } set { SetField(nameof(aafLoad), value); } }
			[ModelField]
			public double diff { get { return GetField<double>(nameof(diff), 0); } set { SetField(nameof(diff), value); } }
			[ModelField]
			public double sync { get { return GetField<double>(nameof(sync), 0); } set { SetField(nameof(sync), value); } }
		}

		public class SyncDiff : Model
		{
			[ModelField]
			public ModelList<Error> errors { get { return GetField<ModelList<Error>>(nameof(errors), null); } set { SetField(nameof(errors), value); } }
			[ModelField]
			public SyncStat stats { get { return GetField<SyncStat>(nameof(stats), null); } set { SetField(nameof(stats), value); } }
			[ModelField]
			public GradesDiff grades { get { return GetField<GradesDiff>(nameof(grades), null); } set { SetField(nameof(grades), value); } }
			[ModelField]
			public SubjectsDiff subjects { get { return GetField<SubjectsDiff>(nameof(subjects), null); } set { SetField(nameof(subjects), value); } }
			[ModelField]
			public StructuresDiff structures { get { return GetField<StructuresDiff>(nameof(structures), null); } set { SetField(nameof(structures), value); } }
			[ModelField]
			public GroupsDiff groups { get { return GetField<GroupsDiff>(nameof(groups), null); } set { SetField(nameof(groups), value); } }
			[ModelField]
			public UsersDiff persEducNat { get { return GetField<UsersDiff>(nameof(persEducNat), null); } set { SetField(nameof(persEducNat), value); } }
			[ModelField]
			public UsersDiff eleves { get { return GetField<UsersDiff>(nameof(eleves), null); } set { SetField(nameof(eleves), value); } }
			[ModelField]
			public UsersDiff parents { get { return GetField<UsersDiff>(nameof(parents), null); } set { SetField(nameof(parents), value); } }
			[ModelField]
			public UsersDiff global { get { return GetField<UsersDiff>(nameof(global), null); } set { SetField(nameof(global), value); } }

		}

		bool IsSyncStructure(string id)
		{
			return interStructures.ContainsKey(id);
		}

		public async Task<SyncDiff> SynchronizeAsync(
			bool subject = false, bool grade = false,
			bool structure = false, bool persEducNat = false,
			bool eleve = false, bool persRelEleve = false,
			bool apply = false)
		{
			SyncDiff res;
			using (DB db = await DB.CreateAsync(dbUrl, true))
			{
				res = await SynchronizeAsync(
					db, subject, grade, structure, persEducNat, eleve, persRelEleve, apply);
				db.Commit();
			}
			return res;
		}

		public async Task<SyncDiff> SynchronizeAsync(DB db,
			bool subject = false, bool grade = false,
			bool structure = false, bool persEducNat = false,
			bool eleve = false, bool persRelEleve = false,
			bool apply = false)
		{
			var diff = new SyncDiff();
			diff.stats = new SyncStat();

			var totalWatch = new Stopwatch();
			totalWatch.Start();

			Stopwatch stopWatch;

			if (structure || eleve || persEducNat || persRelEleve)
			{
				stopWatch = new Stopwatch();
				stopWatch.Start();
				LoadAafSubjects();
				stopWatch.Stop();
				diff.stats.aafLoad += stopWatch.Elapsed.TotalSeconds;

				stopWatch = new Stopwatch();
				stopWatch.Start();
				await LoadEntStructuresAsync(db, structuresIds);
				stopWatch.Stop();
				diff.stats.entLoad += stopWatch.Elapsed.TotalSeconds;

				stopWatch = new Stopwatch();
				stopWatch.Start();
				LoadAafStructures();
				stopWatch.Stop();
				diff.stats.aafLoad += stopWatch.Elapsed.TotalSeconds;

				// find the structures concerned by this synchronization
				if (structuresIds == null)
					interStructuresIds = entStructures.FindAll((obj) => obj.aaf_sync_activated).Select((arg) => arg.id);
				else
					interStructuresIds = entStructures.Select((arg) => arg.id).Intersect(structuresIds);
				interStructuresIds = interStructuresIds.Intersect(aafStructures.Select((arg) => arg.id));

				interStructures = new Dictionary<string, bool>();
				foreach (var id in interStructuresIds)
					interStructures[id] = true;
			}

			if (subject || persEducNat)
			{
				stopWatch = new Stopwatch();
				stopWatch.Start();
				await LoadEntSubjectsAsync(db);
				stopWatch.Stop();
				diff.stats.entLoad += stopWatch.Elapsed.TotalSeconds;
			}

			if (grade || eleve)
			{
				stopWatch = new Stopwatch();
				stopWatch.Start();
				await LoadEntGradesAsync(db);
				stopWatch.Stop();
				diff.stats.entLoad += stopWatch.Elapsed.TotalSeconds;
			}

			if (subject)
			{
				diff.subjects = new SubjectsDiff();
				diff.subjects.stats = new SyncStat();
				stopWatch = new Stopwatch();
				stopWatch.Start();
				LoadAafSubjects();
				stopWatch.Stop();
				diff.subjects.stats.aafLoad = stopWatch.Elapsed.TotalSeconds;
				diff.stats.aafLoad += stopWatch.Elapsed.TotalSeconds;

				stopWatch = new Stopwatch();
				stopWatch.Start();
				diff.subjects.diff = Model.Diff(entSubjects, aafSubjects, (src, dst) => src.id == dst.id);
				stopWatch.Stop();
				// only remove subject not used by anybody
				var remove = new ModelList<Subject>();
				foreach (var removeSubject in diff.subjects.diff.remove)
				{
					if (!entSubjectsUsedById.ContainsKey(removeSubject.id))
						remove.Add(removeSubject);
				}
				diff.subjects.diff.remove = remove;

				diff.subjects.stats.diff = stopWatch.Elapsed.TotalSeconds;
				diff.subjects.stats.addCount += diff.subjects.diff.add.Count;
				diff.subjects.stats.changeCount += diff.subjects.diff.change.Count;
				diff.subjects.stats.removeCount += diff.subjects.diff.remove.Count;
				diff.stats.diff += stopWatch.Elapsed.TotalSeconds;

				if (apply)
				{
					stopWatch = new Stopwatch();
					stopWatch.Start();
					await diff.subjects.diff.ApplyAsync(db);
					stopWatch.Stop();
					diff.subjects.stats.sync = stopWatch.Elapsed.TotalSeconds;
					diff.stats.sync += stopWatch.Elapsed.TotalSeconds;

					await LoadEntSubjectsAsync(db);
				}
			}

			if (grade)
			{
				diff.grades = new GradesDiff();
				diff.grades.stats = new SyncStat();
				stopWatch = new Stopwatch();
				stopWatch.Start();
				LoadAafGrades();
				stopWatch.Stop();
				diff.grades.stats.aafLoad = stopWatch.Elapsed.TotalSeconds;
				diff.stats.aafLoad += stopWatch.Elapsed.TotalSeconds;
				stopWatch = new Stopwatch();
				stopWatch.Start();
				diff.grades.diff = Model.Diff(entGrades, aafGrades, (src, dst) => src.id == dst.id);
				stopWatch.Stop();
				// only remove grade not used by anybody
				var remove = new ModelList<Grade>();
				foreach (var removeGrade in diff.grades.diff.remove)
				{
					if (!entGradesUsedById.ContainsKey(removeGrade.id))
						remove.Add(removeGrade);
				}
				diff.grades.diff.remove = remove;

				diff.grades.stats.diff = stopWatch.Elapsed.TotalSeconds;
				diff.grades.stats.addCount += diff.grades.diff.add.Count;
				diff.grades.stats.changeCount += diff.grades.diff.change.Count;
				diff.grades.stats.removeCount += diff.grades.diff.remove.Count;
				diff.stats.diff += stopWatch.Elapsed.TotalSeconds;
				if (apply)
				{
					stopWatch = new Stopwatch();
					stopWatch.Start();
					await diff.grades.diff.ApplyAsync(db);
					stopWatch.Stop();
					diff.grades.stats.sync = stopWatch.Elapsed.TotalSeconds;
					diff.stats.sync += stopWatch.Elapsed.TotalSeconds;
				}
			}

			if (structure)
			{
				diff.structures = new StructuresDiff();
				diff.structures.stats = new SyncStat();

				var entInterStructures = entStructures.FindAll((obj) => IsSyncStructure(obj.id));
				var aafInterStructures = aafStructures.FindAll((obj) => IsSyncStructure(obj.id));
				stopWatch = new Stopwatch();
				stopWatch.Start();
				diff.structures.diff = Model.Diff(
					entInterStructures, aafInterStructures,
					(src, dst) => src.id == dst.id,
					(src, dst) =>
				{
					var itemDiff = src.DiffWithId(dst);
					var groupsDiff = Model.Diff(
						src.groups.FindAll((obj) => obj.aaf_name != null), dst.groups,
						(s, d) => s.type == d.type && s.aaf_name == d.aaf_name,
						(s2, d2) =>
					{
						var groupDiff = s2.DiffWithId(d2);
						var gradesDiff = Model.Diff(s2.grades, d2.grades, (s3, d3) => s3.grade_id == d3.grade_id);
						if (!gradesDiff.IsEmpty)
						{
							groupDiff.grades = new ModelList<GroupGrade>();
							groupDiff.grades.diff = gradesDiff;
						}
						return groupDiff;
					});
					if (!groupsDiff.IsEmpty)
					{
						itemDiff.groups = new ModelList<Group>();
						itemDiff.groups.diff = groupsDiff;
						foreach (var addGroup in groupsDiff.add)
						{
							addGroup.Fields.Remove(nameof(addGroup.id));
							addGroup.name = addGroup.aaf_name;
							addGroup.aaf_mtime = DateTime.Now;
						}
					}
					return itemDiff;
				});
				stopWatch.Stop();
				diff.structures.stats.diff = stopWatch.Elapsed.TotalSeconds;
				diff.structures.stats.changeCount = diff.structures.diff.change.Count;
				diff.stats.diff += stopWatch.Elapsed.TotalSeconds;

				diff.structures.diff.Fields.Remove(nameof(diff.structures.diff.remove));
				diff.structures.diff.Fields.Remove(nameof(diff.structures.diff.add));
				if (apply)
				{
					stopWatch = new Stopwatch();
					stopWatch.Start();
					await diff.structures.diff.ApplyAsync(db);
					stopWatch.Stop();
					diff.structures.stats.sync = stopWatch.Elapsed.TotalSeconds;
					diff.stats.sync += stopWatch.Elapsed.TotalSeconds;
				}
			}

			if (persEducNat || eleve || persRelEleve)
			{
				stopWatch = new Stopwatch();
				stopWatch.Start();
				await LoadEntUsersAsync(db);
				stopWatch.Stop();
				diff.stats.entLoad += stopWatch.Elapsed.TotalSeconds;
				entSyncSeenUsers = new Dictionary<string, User>();
				// build the group resolver service
				if (structure && apply)
					await LoadEntStructuresAsync(db, interStructuresIds);
				// use the ENT known groups to resolv groups for users
				BuildEntGroupByTypeStructureAafName();
			}

			if (persEducNat)
			{
				stopWatch = new Stopwatch();
				stopWatch.Start();
				var aafTeachers = GetAafTeachers();
				stopWatch.Stop();
				diff.persEducNat = await SyncPersEducNatAsync(db, aafTeachers, apply);
				diff.persEducNat.stats.aafLoad = stopWatch.Elapsed.TotalSeconds;
				diff.stats.aafLoad += stopWatch.Elapsed.TotalSeconds;
				diff.stats.diff += diff.persEducNat.stats.diff;
				diff.stats.sync += diff.persEducNat.stats.sync;
			}

			if (persRelEleve)
			{
				stopWatch = new Stopwatch();
				stopWatch.Start();
				await LoadAafPersRelEleveAsync(db);
				await LoadAafEleveAsync(db);
				stopWatch.Stop();
				diff.parents = await SyncPersRelEleveAsync(db, aafParents, apply);
				diff.parents.stats.aafLoad = stopWatch.Elapsed.TotalSeconds;
				diff.stats.aafLoad += stopWatch.Elapsed.TotalSeconds;
				diff.stats.diff += diff.parents.stats.diff;
				diff.stats.sync += diff.parents.stats.sync;
			}

			if (eleve)
			{
				stopWatch = new Stopwatch();
				stopWatch.Start();
				await LoadAafEleveAsync(db);
				stopWatch.Stop();
				diff.eleves = await SyncEleveAsync(db, aafStudents, apply);
				diff.eleves.stats.aafLoad = stopWatch.Elapsed.TotalSeconds;
				diff.stats.aafLoad += stopWatch.Elapsed.TotalSeconds;
				diff.stats.diff += diff.eleves.stats.diff;
				diff.stats.sync += diff.eleves.stats.sync;
			}

			if (syncFile.format == SyncFileFormat.Full && (persEducNat || persRelEleve || eleve))
			{
				diff.global = await SyncNotSeenUsersAsync(db, persEducNat, persRelEleve, eleve, apply);
				diff.stats.diff += diff.global.stats.diff;
				diff.stats.sync += diff.global.stats.sync;
			}

			diff.errors = new ModelList<Error>();
			foreach (string error in errors)
				diff.errors.Add(new Error { message = error });

			totalWatch.Stop();
			diff.stats.total = totalWatch.Elapsed.TotalSeconds;
			return diff;
		}

		public class SubjectsDiff : Model
		{
			[ModelField]
			public SyncStat stats { get { return GetField<SyncStat>("stats", null); } set { SetField("stats", value); } }
			[ModelField]
			public ModelListDiff<Subject> diff { get { return GetField<ModelListDiff<Subject>>(nameof(diff), null); } set { SetField(nameof(diff), value);	} }
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
			public ModelListDiff<Grade> diff { get { return GetField<ModelListDiff<Grade>>("diff", null); } set { SetField("diff", value); } }
		}

		public class GroupsDiff : Model
		{
			[ModelField]
			public SyncStat stats { get { return GetField<SyncStat>(nameof(stats), null); } set { SetField(nameof(stats), value); } }
			[ModelField]
			public ModelListDiff<Group> diff { get { return GetField<ModelListDiff<Group>>(nameof(diff), null); } set { SetField(nameof(diff), value); } }
		}

		public class StructuresDiff : Model
		{
			[ModelField]
			public SyncStat stats { get { return GetField<SyncStat>(nameof(stats), null); } set { SetField(nameof(stats), value); } }
			[ModelField]
			public ModelListDiff<Structure> diff { get { return GetField<ModelListDiff<Structure>>(nameof(diff), null); } set { SetField(nameof(diff), value); } }
		}

		void BuildEntGroupByTypeStructureAafName()
		{
			entGroupByTypeStructureAafName = new Dictionary<string, Group>();
			
			foreach (var struc in entStructures)
			{
				foreach (var group in struc.groups)
				{
					if ((group.type != "CLS") && (group.type != "GRP"))
						continue;
					if (group.aaf_name == null)
						continue;
					entGroupByTypeStructureAafName[$"{struc.id}${group.type}${group.aaf_name}"] = group;
				}
			}
		}

		public async Task LoadEntStructuresAsync(DB db, IEnumerable<string> structuresIds = null)
		{
			var sql = "SELECT * FROM `structure`";
			if (structuresIds != null)
				sql += " WHERE " + DB.InFilter("id", structuresIds);

			entGroupsById = new Dictionary<int, Group>();
			entStructuresByAafId = new Dictionary<int, Structure>();
			entStructuresById = new Dictionary<string, Structure>();
			entStructures = await db.SelectExpandAsync<Structure>(sql, new object[] { });
			foreach (var structure in entStructures)
			{
				entStructuresById[structure.id] = structure;
				if (structure.aaf_jointure_id != null)
					entStructuresByAafId[(int)structure.aaf_jointure_id] = structure;
				foreach (var group in structure.groups)
				{
					await group.LoadExpandFieldAsync(db, nameof(group.grades));
					entGroupsById[group.id] = group;
				}
			}
		}

		public async Task LoadEntUsersAsync(DB db)
		{
			if (entUsersByAafId != null)
				return;
			entUsersByAafId = new Dictionary<int, User>();
			entUsersByAcademicEmail = new Dictionary<string, User>();
			entUsersByNameBirthdate = new Dictionary<string, User>();
			entUsersByStructRattachId = new Dictionary<int, User>();

			var items = await db.SelectExpandAsync<User>("SELECT * FROM `user`", new object[] { });
			foreach (var user in items)
			{
				if (user.aaf_jointure_id != null)
					entUsersByAafId[(int)user.aaf_jointure_id] = user;
				foreach (var email in user.emails)
					if (email.type == EmailType.Academique)
						entUsersByAcademicEmail[email.address.ToLower()] = user;
				if ((user.firstname != null) && (user.lastname != null) && (user.birthdate != null))
					entUsersByNameBirthdate[user.firstname.RemoveDiacritics().ToLowerInvariant() + "$" +
					                        user.lastname.RemoveDiacritics().ToLowerInvariant() + "$" +
					                        ((DateTime)user.birthdate).ToString("d")] = user;
				if (user.aaf_struct_rattach_id != null)
					entUsersByStructRattachId[(int)user.aaf_struct_rattach_id] = user;
			}
		}

		async Task LoadAafPersRelEleveAsync(DB db)
		{
			if (aafParentsByAafId != null)
				return;

			await LoadEntUsersAsync(db);

			aafParentsById = new Dictionary<string, User>();
			aafParentsByAafId = new Dictionary<long, User>();
			aafParents = GetAafUsers(zipFile.LoadNodes(@"_PersRelEleve_\d+.xml$"));

			foreach (var aafParent in aafParents)
			{
				aafParentsById[aafParent.id] = aafParent;
				aafParentsByAafId[(long)aafParent.aaf_jointure_id] = aafParent;
			}
		}

		async Task LoadAafEleveAsync(DB db)
		{
			if (syncFile.format == SyncFileFormat.Delta)
				await LoadEntUsersAsync(db);
			await LoadAafPersRelEleveAsync(db);
			if (aafStudents != null)
				return;

			aafStudents = GetAafUsers(zipFile.LoadNodes(@"_Eleve_\d+.xml$"));
		}

		public User GetEntUserByAaf(int aaf_jointure_id)
		{
			return entUsersByAafId.ContainsKey(aaf_jointure_id) ? entUsersByAafId[aaf_jointure_id] : null;
		}

		public User GetUserByAcademicEmail(string email)
		{
			var key = email.ToLower();
			return entUsersByAcademicEmail.ContainsKey(key) ? entUsersByAcademicEmail[key] : null;
		}

		public User GetEntUserByNameBirthdate(string firstname, string lastname, DateTime? birthdate)
		{
			if ((firstname == null) || (lastname == null) && (birthdate == null))
				return null;
			var key = firstname.RemoveDiacritics().ToLowerInvariant() + "$" +
			    lastname.RemoveDiacritics().ToLowerInvariant() + "$" +
			    ((DateTime)birthdate).ToString("d");
			return entUsersByNameBirthdate.ContainsKey(key) ? entUsersByNameBirthdate[key] : null;
		}

		public User GetEntUserByStructRattachId(int? aaf_struct_rattach_id)
		{
			if (aaf_struct_rattach_id == null)
				return null;
			return entUsersByStructRattachId.ContainsKey((int)aaf_struct_rattach_id) ? entUsersByStructRattachId[(int)aaf_struct_rattach_id] : null;
		}

		Group GetEntGroupById(int id)
		{
			return entGroupsById.ContainsKey(id) ? entGroupsById[id] : null;
		}

		ModelList<GroupUser> AafGroupUserToEntGroupUser(ModelList<GroupUser> aafGroupUsers)
		{
			var userGroups = new ModelList<GroupUser>();
			foreach (var aafGroupUser in aafGroupUsers)
			{
				if (!aafGroupsById.ContainsKey(aafGroupUser.group_id))
					continue;
				var aafGroup = aafGroupsById[aafGroupUser.group_id];
				if (!IsSyncStructure(aafGroup.structure_id))
					continue;
				var entGroup = GetEntGroupByAafGroup(aafGroup);
				if (entGroup != null)
					userGroups.Add(new GroupUser { type = aafGroupUser.type, group_id = entGroup.id, subject_id = aafGroupUser.subject_id
				});
			}
			return userGroups;
		}

		ModelList<UserChild> AafParentsToEntParents(ModelList<UserChild> aafUserChilds)
		{
			var userChilds = new ModelList<UserChild>();
			foreach (var aafUserChild in aafUserChilds)
			{
				if (!aafParentsById.ContainsKey(aafUserChild.parent_id))
					continue;
				var aafParent = aafParentsById[aafUserChild.parent_id];
				var entParent = GetEntUserByAaf((int)aafParent.aaf_jointure_id);
				if (entParent != null)
				{
					var entUserChild = new UserChild();
					foreach (var key in aafUserChild.Fields.Keys)
						entUserChild.Fields[key] = aafUserChild.Fields[key];
					entUserChild.Fields.Remove(nameof(entUserChild.child_id));
					entUserChild.parent_id = entParent.id;
					userChilds.Add(entUserChild);
				}
			}
			return userChilds;
		}

		Group GetEntGroupByAafGroup(Group aafGroup)
		{
			var key = $"{aafGroup.structure_id}${aafGroup.type}${aafGroup.aaf_name}";
			return entGroupByTypeStructureAafName.ContainsKey(key) ? entGroupByTypeStructureAafName[key] : null;
		}

		Group GetAafGroupByAaf(int aaf_structure_id, string aaf_name, GroupType type)
		{
			var key = $"{aaf_structure_id}${type}${aaf_name}";
			return (aafGroupByTypeStructureAafName.ContainsKey(key)) ? aafGroupByTypeStructureAafName[key] : null;
		}

		Grade GetAafGradeById(string id)
		{
			return (aafGradesById.ContainsKey(id)) ? aafGradesById[id] : null;
		}

		Subject GetEntSubjectById(string id)
		{
			return (entSubjectsById.ContainsKey(id)) ? entSubjectsById[id] : null;
		}

		Subject GetAafSubjectById(string id)
		{
			return (aafSubjectsById.ContainsKey(id)) ? aafSubjectsById[id] : null;
		}

		public class UsersDiff : Model
		{
			[ModelField]
			public SyncStat stats { get { return GetField<SyncStat>(nameof(stats), null); } set { SetField(nameof(stats), value); } }
			[ModelField]
			public ModelListDiff<User> diff { get { return GetField<ModelListDiff<User>>(nameof(diff), null); } set { SetField(nameof(diff), value); } }
		}

		public async Task<UsersDiff> SyncPersEducNatAsync(DB db, IEnumerable<User> aafTeachers, bool apply)
		{
			Console.WriteLine("PERSEDUCNAT SYNCHRONIZE");

			var diff = new UsersDiff();
			diff.stats = new SyncStat();
			diff.diff = new ModelListDiff<User>();
			diff.diff.add = new ModelList<User>();
			diff.diff.change = new ModelList<User>();

			var stopWatch = new Stopwatch();
			stopWatch.Start();

			foreach (var aafUser in aafTeachers)
			{
				var aafUserProfiles = new ModelList<UserProfile>();
				foreach (var profile in aafUser.profiles)
					if (IsSyncStructure(profile.structure_id))
						aafUserProfiles.Add(profile);
				if (aafUserProfiles.Count == 0)
					continue;

				var entUser = GetEntUserByAaf((int)aafUser.aaf_jointure_id);
				if ((entUser == null) && (aafUser.emails != null))
				{
					var acaEmail = aafUser.emails.Find((obj) => obj.type == EmailType.Academique);
					if (acaEmail != null)
						entUser = GetUserByAcademicEmail(acaEmail.address);
				}
				if (entUser == null)
				{
					aafUser.Fields.Remove(nameof(aafUser.id));
					aafUser.groups = AafGroupUserToEntGroupUser(aafUser.groups);
					aafUser.groups.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
					aafUser.profiles = aafUserProfiles;
					aafUser.profiles.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
					diff.diff.add.Add(aafUser);

					foreach (var groupUser in aafUser.groups)
					{
						// if the subject doesn't exists in the ENT, we need to create it (better than null subject)
						if (apply && groupUser.subject_id != null && GetEntSubjectById(groupUser.subject_id) == null)
						{
							var entSubject = new Subject { id = groupUser.subject_id, name = groupUser.subject_id };
							await entSubject.SaveAsync(db);
							entSubjectsById[entSubject.id] = entSubject;
						}
					}
				}
				else
				{
					// mark the user a seen
					entSyncSeenUsers[entUser.id] = entUser;
					var userDiff = entUser.DiffWithId(aafUser);
					// handle phones
					if (aafUser.phones != null)
					{
						var phonesDiff = Model.Diff(entUser.phones, aafUser.phones, (s, d) => s.type == d.type && s.number == d.number);
						if (!phonesDiff.IsEmpty)
						{
							userDiff.phones = new ModelList<Phone>();
							userDiff.phones.diff = phonesDiff;
						}
					}
					// handle emails
					if (aafUser.emails != null)
					{
						var emailsDiff = Model.Diff(entUser.emails.FindAll((obj) => obj.type == EmailType.Academique), aafUser.emails, (s, d) => s.type == d.type && s.address == d.address);
						if (!emailsDiff.IsEmpty)
						{
							userDiff.emails = new ModelList<Email>();
							userDiff.emails.diff = emailsDiff;
						}
					}
					// handle profiles
					var profilesDiff = Model.Diff(
						entUser.profiles.FindAll((obj) => obj.type != "ADM" && IsSyncStructure(obj.structure_id)),
						aafUserProfiles, null,
						(src, dst) =>
						{
							var itemDiff = src.DiffWithId(dst);
							// aaf_mtime is special. If the src has no aaf_mtime, we need to set one
							// else dont update it. The aaf_mtime is set at the last change time
							if (!itemDiff.IsEmpty && src.aaf_mtime == null)
								itemDiff.aaf_mtime = DateTime.Now;
							return itemDiff;
						}
					);
					if (!profilesDiff.IsEmpty)
					{
						userDiff.profiles = new ModelList<UserProfile>();
						userDiff.profiles.diff = profilesDiff;
						if (profilesDiff.add != null)
							profilesDiff.add.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
						if (profilesDiff.change != null)
							profilesDiff.change.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
						// only remove AAF created user_profile
						if (profilesDiff.remove != null)
						{
							var removeProfiles = new ModelList<UserProfile>();
							foreach (var userProfile in profilesDiff.remove)
							{
								if (userProfile.aaf_mtime != null)
									removeProfiles.Add(userProfile);
							}
							profilesDiff.remove = removeProfiles;
						}
						if (profilesDiff.IsEmpty)
							userDiff.UnSetField(nameof(User.profiles));
					}
					// handle groups
					var entInterGroups = entUser.groups.FindAll((obj) =>
					{
						var group = GetEntGroupById(obj.group_id);
						return (group != null) && IsSyncStructure(group.structure_id);
					});
					var aafInterGroups = AafGroupUserToEntGroupUser(aafUser.groups);
					var groupsDiff = Model.Diff(
						entInterGroups, aafInterGroups,
						(src, dst) => src.group_id == dst.group_id && src.type == dst.type && src.subject_id == dst.subject_id,
						(src, dst) =>
						{
							var itemDiff = src.DiffWithId(dst);
							// aaf_mtime is special. If the src has no aaf_mtime, we need to set one
							// else dont update it. The aaf_mtime is set at the last change time
							if (!itemDiff.IsEmpty && src.aaf_mtime == null)
								itemDiff.aaf_mtime = DateTime.Now;
							return itemDiff;
						}
					);


					if (!groupsDiff.IsEmpty)
					{
						userDiff.groups = new ModelList<GroupUser>();
						userDiff.groups.diff = groupsDiff;

						foreach (var groupUser in groupsDiff.add)
						{
							// if the subject doesn't exists in the ENT, we need to create it (better than null subject)
							if (apply && groupUser.subject_id != null && GetEntSubjectById(groupUser.subject_id) == null)
							{
								var entSubject = new Subject { id = groupUser.subject_id, name = groupUser.subject_id };
								await entSubject.SaveAsync(db);
								entSubjectsById[entSubject.id] = entSubject;
							}
						}
						if (groupsDiff.add != null)
							groupsDiff.add.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
						if (groupsDiff.change != null)
							groupsDiff.change.ForEach((obj) => obj.aaf_mtime = DateTime.Now);

						// only remove AAF created user from the group
						if (groupsDiff.remove != null)
						{
							var removeGroupUsers = new ModelList<GroupUser>();
							foreach (var groupUser in groupsDiff.remove)
							{
								if (groupUser.aaf_mtime != null)
									removeGroupUsers.Add(groupUser);
							}
							groupsDiff.remove = removeGroupUsers;
						}
						if (groupsDiff.IsEmpty)
							userDiff.UnSetField(nameof(User.groups));
					}
					if (userDiff.Fields.Count > 1)
						diff.diff.change.Add(userDiff);
				}
			}

			stopWatch.Stop();
			diff.stats.diff = stopWatch.Elapsed.TotalSeconds;
			diff.stats.addCount = diff.diff.add.Count;
			diff.stats.changeCount = diff.diff.change.Count;

			if (apply)
			{
				stopWatch = new Stopwatch();
				stopWatch.Start();
				await diff.diff.ApplyAsync(db);
				// add ENT email for new teachers
				foreach (var addUser in diff.diff.add)
					await addUser.CreateDefaultEntEmailAsync(db);

				stopWatch.Stop();
				diff.stats.sync = stopWatch.Elapsed.TotalSeconds;
			}
			return diff;
		}


		public async Task<UsersDiff> SyncPersRelEleveAsync(DB db, IEnumerable<User> aafParents, bool apply)
		{
			Console.WriteLine("PERSRELELEVE SYNCHRONIZE");

			var diff = new UsersDiff();
			diff.stats = new SyncStat();
			diff.diff = new ModelListDiff<User>();
			diff.diff.add = new ModelList<User>();
			diff.diff.change = new ModelList<User>();

			var stopWatch = new Stopwatch();
			stopWatch.Start();

			foreach (var aafUser in aafParents)
			{
				var aafUserProfiles = new ModelList<UserProfile>();
				if (aafUser.profiles != null)
				{
					foreach (var profile in aafUser.profiles)
						if (IsSyncStructure(profile.structure_id))
							aafUserProfiles.Add(profile);
				}
				if (aafUserProfiles.Count == 0)
					continue;

				var entUser = GetEntUserByAaf((int)aafUser.aaf_jointure_id);
				if (entUser == null)
				{
					aafUser.Fields.Remove(nameof(aafUser.id));
					aafUser.Fields.Remove(nameof(aafUser.children));
					aafUser.profiles = aafUserProfiles;
					// set the AAF mtime for thoses profiles
					aafUser.profiles.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
					diff.diff.add.Add(aafUser);
				}
				else
				{
					// mark the user a seen
					entSyncSeenUsers[entUser.id] = entUser;
					var userDiff = entUser.DiffWithId(aafUser);
					// handle phones
					if (aafUser.phones != null)
					{
						var phonesDiff = Model.Diff(entUser.phones, aafUser.phones, (s, d) => s.type == d.type && s.number == d.number);
						if (!phonesDiff.IsEmpty)
						{
							userDiff.phones = new ModelList<Phone>();
							userDiff.phones.diff = phonesDiff;
						}
					}
					// handle emails
					if (aafUser.emails != null)
					{
						var emailsDiff = Model.Diff(entUser.emails.FindAll((obj) => obj.type == EmailType.Autre), aafUser.emails, (s, d) => s.type == d.type && s.address == d.address);
						if (!emailsDiff.IsEmpty)
						{
							userDiff.emails = new ModelList<Email>();
							userDiff.emails.diff = emailsDiff;
						}
					}
					// handle profiles
					var profilesDiff = Model.Diff(
						entUser.profiles.FindAll((obj) => obj.type != "ADM" && IsSyncStructure(obj.structure_id)),
						aafUserProfiles, null,
						(src, dst) =>
						{
							var itemDiff = src.DiffWithId(dst);
							// aaf_mtime is special. If the src has no aaf_mtime, we need to set one
							// else dont update it. The aaf_mtime is set at the last change time
							if (!itemDiff.IsEmpty && src.aaf_mtime == null)
								itemDiff.aaf_mtime = DateTime.Now;
							return itemDiff;
						}
					);

					// in Delta mode, only add/change
					if (!profilesDiff.IsEmpty && syncFile.format == SyncFileFormat.Delta && profilesDiff.remove != null)
						profilesDiff.remove.Clear();

					if (!profilesDiff.IsEmpty)
					{
						userDiff.profiles = new ModelList<UserProfile>();
						userDiff.profiles.diff = profilesDiff;
						if (profilesDiff.add != null)
							profilesDiff.add.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
						if (profilesDiff.change != null)
							profilesDiff.change.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
						// only remove AAF created user_profile
						if (profilesDiff.remove != null)
						{
							var removeProfiles = new ModelList<UserProfile>();
							foreach (var userProfile in profilesDiff.remove)
							{
								if (userProfile.aaf_mtime != null)
									removeProfiles.Add(userProfile);
							}
							profilesDiff.remove = removeProfiles;
						}
						if (profilesDiff.IsEmpty)
							userDiff.UnSetField(nameof(User.profiles));
					}
					if (userDiff.Fields.Count > 1)
						diff.diff.change.Add(userDiff);
				}
			}

			stopWatch.Stop();
			diff.stats.diff = stopWatch.Elapsed.TotalSeconds;
			diff.stats.addCount = diff.diff.add.Count;
			diff.stats.changeCount = diff.diff.change.Count;

			if (apply)
			{
				stopWatch = new Stopwatch();
				stopWatch.Start();
				await diff.diff.ApplyAsync(db);
				// register newly created user to the ENT users
				// needed for the students to find their parents
				foreach (var newParent in diff.diff.add)
				{
					entSyncSeenUsers[newParent.id] = newParent;
					entUsersByAafId[(int)newParent.aaf_jointure_id] = newParent;
				}
				stopWatch.Stop();
				diff.stats.sync = stopWatch.Elapsed.TotalSeconds;
			}
			return diff;
		}

		public async Task<UsersDiff> SyncEleveAsync(DB db, IEnumerable<User> aafStudents, bool apply)
		{
			Console.WriteLine("ELEVE SYNCHRONIZE");

			var diff = new UsersDiff();
			diff.stats = new SyncStat();
			diff.diff = new ModelListDiff<User>();
			diff.diff.add = new ModelList<User>();
			diff.diff.change = new ModelList<User>();

			var stopWatch = new Stopwatch();
			stopWatch.Start();

			foreach (var aafUser in aafStudents)
			{
				var aafUserProfiles = new ModelList<UserProfile>();
				foreach (var profile in aafUser.profiles)
					if (IsSyncStructure(profile.structure_id))
						aafUserProfiles.Add(profile);
				if (aafUserProfiles.Count == 0)
					continue;

				var entUser = GetEntUserByAaf((int)aafUser.aaf_jointure_id);
				if (entUser == null)
				{
					entUser = GetEntUserByStructRattachId(aafUser.aaf_struct_rattach_id);
					// only take it is an aaf_jointure_id is not already present
					if (entUser != null)
					{
						if (entUser.aaf_jointure_id != null)
						{
							errors.Add("ERROR: ELEVE FOUND BY aaf_struct_rattach_id BUT WITH ANOTHER aaf_jointure_id, "+
							           "ELEVE MIGTH BE A DUPLICATE ACCOUNT "+
							           $"(LASTNAME: {aafUser.lastname}, FIRSTNAME: {aafUser.firstname}, AAF_JOINTURE_ID: {aafUser.aaf_jointure_id})"+
							           $" FOUND ENT (ID: {entUser.id}, AAF_JOINTURE_ID: {entUser.aaf_jointure_id})");
							entUser = null;
						}
					}
				}
				if (entUser == null)
				{
					entUser = GetEntUserByNameBirthdate(aafUser.firstname, aafUser.lastname, aafUser.birthdate);
					// only take it is an aaf_jointure_id is not already present
					// we do this because some student might have multiples accounts
					// and we dont want to have a flip / flop between the accounts
					if (entUser != null)
					{
						if (entUser.aaf_jointure_id != null)
						{
							errors.Add("ERROR: ELEVE FOUND BY NAME AND BIRTHDATE BUT WITH ANOTHER aaf_jointure_id, " +
									   "ELEVE MIGTH BE A DUPLICATE ACCOUNT " +
									   $"(LASTNAME: {aafUser.lastname}, FIRSTNAME: {aafUser.firstname}, AAF_JOINTURE_ID: {aafUser.aaf_jointure_id})" +
									   $" FOUND ENT (ID: {entUser.id}, AAF_JOINTURE_ID: {entUser.aaf_jointure_id})");
							entUser = null;
						}
					}
				}

				if (entUser == null)
				{
					aafUser.Fields.Remove(nameof(aafUser.id));
					aafUser.profiles = aafUserProfiles;
					aafUser.profiles.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
					aafUser.groups = AafGroupUserToEntGroupUser(aafUser.groups);
					// set the AAF mtime of this relations
					aafUser.groups.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
					aafUser.parents = AafParentsToEntParents(aafUser.parents);
					diff.diff.add.Add(aafUser);
				}
				else
				{
					// mark the user a seen
					entSyncSeenUsers[entUser.id] = entUser;
					var userDiff = entUser.DiffWithId(aafUser);
					// handle phones
					if (aafUser.phones != null)
					{
						var phonesDiff = Model.Diff(entUser.phones, aafUser.phones, (s, d) => s.type == d.type && s.number == d.number);
						if (!phonesDiff.IsEmpty)
						{
							userDiff.phones = new ModelList<Phone>();
							userDiff.phones.diff = phonesDiff;
						}
					}
					// handle emails
					if (aafUser.emails != null)
					{
						var emailsDiff = Model.Diff(entUser.emails.FindAll((obj) => obj.type == EmailType.Autre), aafUser.emails, (s, d) => s.type == d.type && s.address == d.address);
						if (!emailsDiff.IsEmpty)
						{
							userDiff.emails = new ModelList<Email>();
							userDiff.emails.diff = emailsDiff;
						}
					}
					// handle profiles
					var profilesDiff = Model.Diff(
						entUser.profiles.FindAll((obj) => obj.type != "ADM" && IsSyncStructure(obj.structure_id)),
						aafUserProfiles, null,
						(src, dst) => {
							var itemDiff = src.DiffWithId(dst);
							// aaf_mtime is special. If the src has no aaf_mtime, we need to set one
							// else dont update it. The aaf_mtime is set at the last change time
							if (!itemDiff.IsEmpty && src.aaf_mtime == null)
								itemDiff.aaf_mtime = DateTime.Now;
							return itemDiff;
						}
					);
					if (!profilesDiff.IsEmpty)
					{
						userDiff.profiles = new ModelList<UserProfile>();
						userDiff.profiles.diff = profilesDiff;
						if (profilesDiff.add != null)
							profilesDiff.add.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
						if (profilesDiff.change != null)
							profilesDiff.change.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
						// only remove AAF created user_profile
						if (profilesDiff.remove != null)
						{
							var removeProfiles = new ModelList<UserProfile>();
							foreach (var userProfile in profilesDiff.remove)
							{
								if (userProfile.aaf_mtime != null)
									removeProfiles.Add(userProfile);
							}
							profilesDiff.remove = removeProfiles;
						}
						if (profilesDiff.IsEmpty)
							userDiff.UnSetField(nameof(User.profiles));
					}

					// handle groups
					var entInterGroups = entUser.groups.FindAll((obj) =>
					{
						var group = GetEntGroupById(obj.group_id);
						return (group != null) && IsSyncStructure(group.structure_id);
					});
					var aafInterGroups = AafGroupUserToEntGroupUser(aafUser.groups);
					var groupsDiff = Model.Diff(
						entInterGroups, aafInterGroups,
						(src, dst) => src.group_id == dst.group_id && src.type == dst.type && src.subject_id == dst.subject_id,
						(src, dst) =>
						{
							var itemDiff = src.DiffWithId(dst);
							// aaf_mtime is special. If the src has no aaf_mtime, we need to set one
							// else dont update it. The aaf_mtime is set at the last change time
							if (!itemDiff.IsEmpty && src.aaf_mtime == null)
								itemDiff.aaf_mtime = DateTime.Now;
							return itemDiff;
						}
					);
					if (!groupsDiff.IsEmpty)
					{
						userDiff.groups = new ModelList<GroupUser>();
						userDiff.groups.diff = groupsDiff;
						// set the AAF mtime of this relations
						if (groupsDiff.add != null)
							groupsDiff.add.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
						if (groupsDiff.change != null)
							groupsDiff.change.ForEach((obj) => obj.aaf_mtime = DateTime.Now);
						// only remove AAF created user from the group
						if (groupsDiff.remove != null)
						{
							var removeGroupUsers = new ModelList<GroupUser>();
							foreach (var groupUser in groupsDiff.remove)
							{
								if (groupUser.aaf_mtime != null)
									removeGroupUsers.Add(groupUser);
							}
							groupsDiff.remove = removeGroupUsers;
						}
						if (groupsDiff.IsEmpty)
							userDiff.UnSetField(nameof(User.groups));
					}

					// handle parents relations
					var aafUserParents = AafParentsToEntParents(aafUser.parents);
					var parentsDiff = Model.Diff(entUser.parents, aafUserParents,(src, dst) => src.parent_id == dst.parent_id && src.type == dst.type);

					// in Delta mode, only add/change
					if (!parentsDiff.IsEmpty && syncFile.format == SyncFileFormat.Delta)
						parentsDiff.remove.Clear();
					if (!parentsDiff.IsEmpty)
					{
						userDiff.parents = new ModelList<UserChild>();
						userDiff.parents.diff = parentsDiff;
					}

					if (userDiff.Fields.Count > 1)
						diff.diff.change.Add(userDiff);
				}
			}

			stopWatch.Stop();
			diff.stats.diff = stopWatch.Elapsed.TotalSeconds;
			diff.stats.addCount = diff.diff.add.Count;
			diff.stats.changeCount = diff.diff.change.Count;

			if (apply)
			{
				stopWatch = new Stopwatch();
				stopWatch.Start();
				await diff.diff.ApplyAsync(db);
				// add ENT email for new students
				foreach (var addUser in diff.diff.add)
					await addUser.CreateDefaultEntEmailAsync(db);

				stopWatch.Stop();
				diff.stats.sync = stopWatch.Elapsed.TotalSeconds;
			}
			return diff;
		}

		public async Task<UsersDiff> SyncNotSeenUsersAsync(DB db, bool persEducNat, bool persRelEleve, bool eleve, bool apply)
		{
			Console.WriteLine("NOTSEENUSERS SYNCHRONIZE");

			var syncProfilesTypes = new List<string>();

			if (persEducNat)
				syncProfilesTypes.AddRange(persEducNatProfiles);
			if (persRelEleve)
				syncProfilesTypes.Add("TUT");
			if (eleve)
				syncProfilesTypes.Add("ELV");

			var diff = new UsersDiff();
			diff.stats = new SyncStat();
			diff.diff = new ModelListDiff<User>();
			diff.diff.change = new ModelList<User>();

			var stopWatch = new Stopwatch();
			stopWatch.Start();

			// find all users not seen in the current synchronization with a profiles
			// in the structures seen in the current synchronization and remove them
			foreach (var entUser in entUsersByAafId.Values)
			{
				if (entUser.aaf_jointure_id == null)
					continue;
				if (!entSyncSeenUsers.ContainsKey(entUser.id))
				{
					User changeUser = null;
					var userProfiles = entUser.profiles;

					var removeProfiles = entUser.profiles.FindAll(
						(obj) => IsSyncStructure(obj.structure_id) && (obj.aaf_mtime != null) && syncProfilesTypes.Contains(obj.type));
					if (removeProfiles.Any())
					{
						var userRemoveProfiles = new ModelList<UserProfile>();
						foreach (var userProfile in removeProfiles)
						{
							userRemoveProfiles.Add(userProfile);
							userProfiles.Remove(userProfile);
						}
						changeUser = new User { id = entUser.id };
						changeUser.profiles = new ModelList<UserProfile>();
						changeUser.profiles.diff = new ModelListDiff<UserProfile>();
						changeUser.profiles.diff.remove = userRemoveProfiles;
					}

					// garbage collect user in structure's group where the user has no profile
					if (entUser.groups != null)
					{
						foreach (var userGroup in entUser.groups)
						{
							var entGroup = GetEntGroupById(userGroup.group_id);
							if (entGroup == null)
								continue;
							if (entGroup.aaf_name == null)
								continue;
							if (entGroup.structure_id == null)
								continue;
							if (!IsSyncStructure(entGroup.structure_id))
								continue;
							if (userProfiles.Any((arg) => entGroup.structure_id == arg.structure_id))
								continue;

							if (changeUser == null)
								changeUser = new User { id = entUser.id };

							if (changeUser.groups == null)
							{
								changeUser.groups = new ModelList<GroupUser>();
								changeUser.groups.diff = new ModelListDiff<GroupUser>();
								changeUser.groups.diff.remove = new ModelList<GroupUser>();
							}
							changeUser.groups.diff.remove.Add(userGroup);
						}
					}
					if (changeUser != null)
						diff.diff.change.Add(changeUser);
				}
			}

			stopWatch.Stop();
			diff.stats.diff = stopWatch.Elapsed.TotalSeconds;
			diff.stats.changeCount = diff.diff.change.Count;

			if (apply)
			{
				stopWatch = new Stopwatch();
				stopWatch.Start();
				await diff.diff.ApplyAsync(db);
				stopWatch.Stop();
				diff.stats.sync = stopWatch.Elapsed.TotalSeconds;
			}
			return diff;
		}

		public User NodeToUser(XmlNode node, Dictionary<int, Structure> structures)
		{
			long id;
			string categoriePersonne;
			Dictionary<string, string> attrs;
			Dictionary<string, List<string>> attrsMul;

			ReadAttributes(node, userMulFields, out id, out categoriePersonne, out attrs, out attrsMul);
			return AttributesToUser(id, categoriePersonne, attrs, attrsMul, structures);
		}

		public static void ReadAttributes(XmlNode node, string[] mulFields, out long id, out string categoriePersonne, out Dictionary<string,string> attrs, out Dictionary<string,List<string>> attrsMul)
		{
			id = Convert.ToInt64(node["identifier"]["id"].InnerText);
			categoriePersonne = null;
			attrs = new Dictionary<string, string>();
			attrsMul = new Dictionary<string, List<string>>();

			foreach (XmlNode attr in node["operationalAttributes"])
			{
				if (attr.NodeType != XmlNodeType.Element)
					continue;
				if (attr.Attributes["name"].Value == "categoriePersonne")
					categoriePersonne = attr["value"].InnerText;
			}

			foreach (XmlNode attr in node["attributes"] ?? node["modifications"])
			{
				if (attr.NodeType != XmlNodeType.Element)
					continue;

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

		public User AttributesToUser(
			long id, string categoriePersonne, Dictionary<string, string> attrs,
			Dictionary<string, List<string>> attrsMul, Dictionary<int, Structure> structures)
		{
			var user = new User { aaf_jointure_id = id };
			Gender? gender = null;
			if (attrs.ContainsKey("personalTitle"))
				if (attrs["personalTitle"] == "Mme")
					gender = Gender.F;
				else if (attrs["personalTitle"] == "M.")
					gender = Gender.M;
			if (gender != null)
				user.gender = gender;

			if (categoriePersonne == "PersRelEleve")
				user.id = "TUT" + (++tutUserIdGenerator).ToString("D5");
			else if (categoriePersonne == "Eleve")
				user.id = "ELV" + (++elvUserIdGenerator).ToString("D5");
			else if (categoriePersonne == "PersEducNat")
				user.id = "ENS" + (++ensUserIdGenerator).ToString("D5");
			else
				throw new Exception($"Unknown categoriePersonne '{categoriePersonne}'");

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

			// normalize the lastname. Uppercase
			if (attrs.ContainsKey("sn"))
				user.lastname = attrs["sn"].ToUpper();

			// handle phones
			if (attrs.ContainsKey("telephoneNumber") && !string.IsNullOrWhiteSpace(attrs["telephoneNumber"]))
			{
				if (user.phones == null)
					user.phones = new ModelList<Phone>();

				user.phones.Add(new Phone
				{
					number = attrs["telephoneNumber"],
					type = "TRAVAIL"
				});
			}
			if (attrs.ContainsKey("homePhone") && !string.IsNullOrWhiteSpace(attrs["homePhone"]))
			{
				if (user.phones == null)
					user.phones = new ModelList<Phone>();
				
				user.phones.Add(new Phone
				{
					number = attrs["homePhone"],
					type = "MAISON"
				});
			}
			if (attrsMul.ContainsKey("mobile"))
			{
				foreach (var mobile in attrsMul["mobile"])
				{
					if (!string.IsNullOrWhiteSpace(mobile))
					{
						if (user.phones == null)
							user.phones = new ModelList<Phone>();

						user.phones.Add(new Phone
						{
							number = mobile,
							type = "PORTABLE"
						});
					}
				}
			}

			// handle emails
			if (attrsMul.ContainsKey("mail"))
			{
				if (user.emails == null)
					user.emails = new ModelList<Email>();

				foreach (var mail in attrsMul["mail"])
				{
					if (string.IsNullOrWhiteSpace(mail))
						continue;

					user.emails.Add(new Email
					{
						address = mail,
						type = (categoriePersonne == "PersEducNat") ? EmailType.Academique : EmailType.Autre
					});
				}
			}

			// handle student's classes
			if (attrsMul.ContainsKey("ENTEleveClasses"))
			{
				if (user.groups == null)
					user.groups = new ModelList<GroupUser>();

				foreach (var classe in attrsMul["ENTEleveClasses"])
				{
					var tab = classe.Split('$');
					if (tab.Length == 2)
					{
						var group = GetAafGroupByAaf(Convert.ToInt32(tab[0]), tab[1], GroupType.CLS);

						if (group != null)
						{
							// protect against duplicate
							if (!user.groups.Any((arg) => arg.type == "ELV" && arg.group_id == group.id))
							{
								user.groups.Add(new GroupUser
								{
									type = "ELV",
									group_id = group.id
								});
							}
						}
					}
				}
			}

			// handle student's groups
			if (attrsMul.ContainsKey("ENTEleveGroupes"))
			{
				if (user.groups == null)
					user.groups = new ModelList<GroupUser>();

				foreach (var groupe in attrsMul["ENTEleveGroupes"])
				{
					var tab = groupe.Split('$');
					if (tab.Length == 2)
					{
						var group = GetAafGroupByAaf(Convert.ToInt32(tab[0]), tab[1], GroupType.GRP);

						if (group != null)
						{
							// protect against duplicate
							if (!user.groups.Any((arg) => arg.type == "ELV" && arg.group_id == group.id))
							{
								user.groups.Add(new GroupUser
								{
									type = "ELV",
									group_id = group.id
								});
							}
						}
					}
				}
			}

			// handle teacher's classes
			if (attrsMul.ContainsKey("ENTAuxEnsClassesMatieres"))
			{
				if (user.groups == null)
					user.groups = new ModelList<GroupUser>();

				foreach (var classe in attrsMul["ENTAuxEnsClassesMatieres"])
				{
					var tab = classe.Split('$');
					if (tab.Length == 3)
					{
						var group = GetAafGroupByAaf(Convert.ToInt32(tab[0]), tab[1], GroupType.CLS);

						//Console.WriteLine($"AAF CLASSE: {classe}, GROUP: {group}");

						if (group != null)
						{
							// ensure the subject exists. Some subject can be used by teacher
							// but not given in the AAF
							var subject = GetAafSubjectById(tab[2]);
							var subjectId = tab[2];
							if (subject == null)
							{
								//subjectId = null;
								errors.Add($"ERROR: ADD USER {user.firstname} {user.lastname} {user.id} TO GROUP {group.id} {group.name} WITH NONE EXISTING SUBJECT ({tab[2]}) USE NULL");
							}

							// protect against duplicate
							if (!user.groups.Any((arg) => arg.type == "ENS" && arg.group_id == group.id && arg.subject_id == subjectId))
							{
								user.groups.Add(new GroupUser
								{
									type = "ENS",
									group_id = group.id,
									subject_id = subjectId
								});
							}
						}
					}
				}
			}

			// handle teacher's groups
			if (attrsMul.ContainsKey("ENTAuxEnsGroupesMatieres"))
			{
				if (user.groups == null)
					user.groups = new ModelList<GroupUser>();

				foreach (var classe in attrsMul["ENTAuxEnsGroupesMatieres"])
				{
					var tab = classe.Split('$');

					if (tab.Length == 3)
					{
						var group = GetAafGroupByAaf(Convert.ToInt32(tab[0]), tab[1], GroupType.GRP);

						//Console.WriteLine($"AAF GROUPE: {classe}, GROUP: {group}");

						if (group != null)
						{
							// ensure the subject exists. Some subject can be used by teacher
							// but not given in the AAF
							var subject = GetAafSubjectById(tab[2]);
							var subjectId = tab[2];
							if (subject == null)
							{
								//subjectId = null;
								errors.Add($"ERROR: ADD USER {user.firstname} {user.lastname} {user.id} TO GROUP {group.id} {group.name} WITH NONE EXISTING SUBJECT ({tab[2]}) USE NULL");
							}

							// protect against duplicate
							if (!user.groups.Any((arg) => arg.type == "ENS" && arg.group_id == group.id && arg.subject_id == subjectId))
							{
								user.groups.Add(new GroupUser
								{
									type = "ENS",
									group_id = group.id,
									subject_id = subjectId
								});
							}
						}
					}
				}
			}

			// handle teacher's "prof principal"
			if (attrsMul.ContainsKey("ENTAuxEnsClassesPrincipal"))
			{
				if (user.groups == null)
					user.groups = new ModelList<GroupUser>();

				foreach (var classe in attrsMul["ENTAuxEnsClassesPrincipal"])
				{
					var tab = classe.Split('$');

					if (tab.Length == 2)
					{
						var group = GetAafGroupByAaf(Convert.ToInt32(tab[0]), tab[1], GroupType.CLS);

						if (group != null)
						{
							user.groups.Add(new GroupUser
							{
								type = "PRI",
								group_id = group.id
							});
						}
					}
				}

			}

			// handle teacher's profiles in structures
			if (attrsMul.ContainsKey("ENTPersonFonctions"))
			{
				if (user.profiles == null)
					user.profiles = new ModelList<UserProfile>();

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

						var aaf_jointure_id = Convert.ToInt32(tab[0]);

						if (structures.ContainsKey(aaf_jointure_id))
						{
							if (!user.profiles.Any((arg) => arg.type == profileId && arg.structure_id == structures[aaf_jointure_id].id))
							{
								user.profiles.Add(new UserProfile
								{
									type = profileId,
									structure_id = structures[aaf_jointure_id].id,
								});
							}
						}
					}
				}
			}

			// handle student's structure profile
			if (attrs.ContainsKey("ENTPersonStructRattach") && !string.IsNullOrEmpty(attrs["ENTPersonStructRattach"]) && (categoriePersonne == "Eleve"))
			{
				if (user.profiles == null)
					user.profiles = new ModelList<UserProfile>();

				var ENTPersonStructRattach = int.Parse(attrs["ENTPersonStructRattach"]);

				if (structures.ContainsKey(ENTPersonStructRattach))
				{
					user.profiles.Add(new UserProfile
					{
						type = "ELV",
						structure_id = structures[ENTPersonStructRattach].id
					});
				}
			}

			// handle parents
			if (attrsMul.ContainsKey("ENTElevePersRelEleve"))
			{
				user.parents = new ModelList<UserChild>();
				foreach (var relEleve in attrsMul["ENTElevePersRelEleve"])
				{
					var tab = relEleve.Split('$');
					if (tab.Length == 6)
					{
						var parent_aaf_jointure_id = long.Parse(tab[0]);

						User parent = null;
						// check if the parent is provided in the AAF file
						if (aafParentsByAafId.ContainsKey(parent_aaf_jointure_id))
							parent = aafParentsByAafId[parent_aaf_jointure_id];
						// if the parent is not find in this AAF file and if we are
						// in a Delta file, take the parent from the current ENT known parent
						else if (syncFile.format == SyncFileFormat.Delta && entUsersByAafId.ContainsKey((int)parent_aaf_jointure_id))
						{
							parent = entUsersByAafId[(int)parent_aaf_jointure_id];
							aafParentsByAafId[parent_aaf_jointure_id] = parent;
						}

						// ERROR: parent not found...
						if (parent == null)
						{
							Console.WriteLine($"WARNING: INVALID ENTElevePersRelEleve PARENT WITH aaf_jointure_id {parent_aaf_jointure_id} NOT FOUND");
							continue;
						}
						else
						{
							var relTypeInt = int.Parse(tab[1]);
							if (aafRelationType.ContainsKey(relTypeInt))
							{
								var relType = aafRelationType[relTypeInt];

								user.parents.Add(new UserChild
								{
									type = relType,
									parent_id = parent.id,
									child_id = user.id,
									financial = tab[2] == "1",
									legal = tab[3] == "1",
									contact = tab[4] == "1"
								});

								// copy the child relation to the parent
								if (parent.children == null)
									parent.children = new ModelList<UserChild>();
								if (!parent.children.Any((arg) => arg.child_id == user.id))
									parent.children.Add(new UserChild
									{
										type = relType,
										parent_id = parent.id,
										child_id = user.id,
										financial = tab[2] == "1",
										legal = tab[3] == "1",
										contact = tab[4] == "1"
									});
								// convert the child profiles to parent profiles in the structures
								if (user.profiles != null)
								{
									if (parent.profiles == null)
										parent.profiles = new ModelList<UserProfile>();
									foreach (var profile in user.profiles.FindAll((obj) => obj.type == "ELV"))
										if (!parent.profiles.Any((arg) => arg.type == "TUT" && arg.structure_id == profile.structure_id))
											parent.profiles.Add(new UserProfile { type = "TUT", structure_id = profile.structure_id });
								}
							}
							else
							{
								Console.WriteLine($"WARNING: INVALID ENTElevePersRelEleve aafRelationType WITH ID {relTypeInt} NOT FOUND");
							}
						}
					}
				}
			}

			return user;
		}

		async Task LoadEntGradesAsync(DB db)
		{
			entGrades = await db.SelectAsync<Grade>("SELECT * FROM `grade`");
			entGradesById = new Dictionary<string, Grade>();
			foreach (var grade in entGrades)
				entGradesById[grade.id] = grade;

			entGradesUsedById = new Dictionary<string, Grade>();
			foreach (var grade in await db.SelectAsync<Grade>("SELECT * FROM `grade` WHERE `id` IN (SELECT DISTINCT(student_grade_id) FROM `user` WHERE student_grade_id IS NOT NULL)"))
				entGradesUsedById[grade.id] = grade;
		}

		public ModelList<Grade> GetAafGrades()
		{
			LoadAafGrades();
			return aafGrades;
		}

		void LoadAafGrades()
		{
			if (aafGrades != null)
				return;

			aafGrades = new ModelList<Grade>();
			aafGradesById = new Dictionary<string, Grade>();
			foreach (var node in zipFile.LoadNodes(@"_MefEducNat_\d+.xml$"))
			{
				var grade = NodeToGrade(node);
				if (grade != null)
				{
					aafGrades.Add(grade);
					aafGradesById[grade.id] = grade;
				}
			}
		}

		Grade NodeToGrade(XmlNode node)
		{
			var id = node["identifier"]["id"].InnerText;
			string ENTMefJointure = null;
			string ENTLibelleMef = null;
			string ENTMEFRattach = null;
			string ENTMEFSTAT11 = null;
			foreach (XmlNode attr in node["attributes"])
			{
				if (attr.NodeType != XmlNodeType.Element)
					continue;
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
				return new Grade
				{
					id = id,
					name = ENTLibelleMef,
					rattach = ENTMEFRattach,
					stat = ENTMEFSTAT11
				};
			}
			return null;
		}

		public ModelList<Subject> GetAafSubjects()
		{
			LoadAafSubjects();
			return aafSubjects;
		}

		async Task LoadEntSubjectsAsync(DB db)
		{
			entSubjects = await db.SelectAsync<Subject>("SELECT * FROM `subject`");
			entSubjectsById = new Dictionary<string, Subject>();
			foreach (var subject in entSubjects)
				entSubjectsById[subject.id] = subject;

			entSubjectsUsedById = new Dictionary<string, Subject>();
			foreach (var subject in await db.SelectAsync<Subject>("SELECT * FROM `subject` WHERE `id` IN (SELECT DISTINCT(subject_id) FROM `group_user` WHERE subject_id IS NOT NULL)"))
				entSubjectsUsedById[subject.id] = subject;
		}

		void LoadAafSubjects()
		{
			if (aafSubjects != null)
				return;
			
			aafSubjects = new ModelList<Subject>();
			aafSubjectsById = new Dictionary<string, Subject>();

			foreach (XmlNode node in zipFile.LoadNodes(@"_MatiereEducNat_\d+.xml$"))
			{
				var aafSubject = NodeToSubject(node);
				if (aafSubject != null)
				{
					aafSubjects.Add(aafSubject);
					aafSubjectsById[aafSubject.id] = aafSubject;
				}
			}
		}

		Subject NodeToSubject(XmlNode node)
		{
			var id = node["identifier"]["id"].InnerText;
			string ENTMatJointure = null;
			string ENTLibelleMatiere = null;
			foreach (XmlNode attr in node["attributes"])
			{
				if (attr.NodeType != XmlNodeType.Element)
					continue;
				if (attr.Attributes["name"].Value == "ENTMatJointure")
					ENTMatJointure = attr["value"].InnerText;
				else if (attr.Attributes["name"].Value == "ENTLibelleMatiere")
					ENTLibelleMatiere = attr["value"].InnerText;
			}
			if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(ENTMatJointure) &&
			   !string.IsNullOrEmpty(ENTLibelleMatiere) && (ENTMatJointure == id))
				return new Subject { id = id, name = ENTLibelleMatiere };
			else
			{
				errors.Add("Invalid SUBJECT FOUND. Some fields are null or empty "+
				           $"(id: {id}, EntMatJointure: {ENTMatJointure}, ENTLibelleMatiere: {ENTLibelleMatiere})");
				return null;
			}
		}

		public ModelList<Structure> GetAafStructures()
		{
			LoadAafStructures();
			return aafStructures;
		}

		void LoadAafStructures()
		{
			if (aafStructures != null)
				return;

			LoadAafGrades();
			aafStructures = new ModelList<Structure>();
			aafStructuresByAafId = new Dictionary<int, Structure>();
			aafGroupsById = new Dictionary<int, Group>();
			aafGroupByTypeStructureAafName = new Dictionary<string, Group>();

			foreach (XmlNode node in zipFile.LoadNodes(@"_EtabEducNat_\d+.xml$"))
			{
				if (node.NodeType != XmlNodeType.Element)
					continue;
				var aafStructure = NodeToStructure(node);
				if (aafStructure != null)
				{
					aafStructures.Add(aafStructure);
					aafStructuresByAafId[(int)aafStructure.aaf_jointure_id] = aafStructure;
					foreach (var group in aafStructure.groups)
					{
						// copy the group because we want to keep the AAF version
						// and if the group is created with a SaveAsync it will be transform in the ENT version
						var groupCopy = new Group();
						foreach (var key in group.Fields.Keys)
							groupCopy.Fields[key] = group.Fields[key];
						aafGroupsById[group.id] = groupCopy;
						aafGroupByTypeStructureAafName[$"{aafStructure.aaf_jointure_id}${group.type}${group.aaf_name}"] = groupCopy;
					}
				}
			}
		}

		Structure NodeToStructure(XmlNode node)
		{
			var id = node["identifier"]["id"].InnerText;
			List<string> ENTStructureClasses = null;
			List<string> ENTStructureGroupes = null;
			var attrs = new Dictionary<string, string>();
			foreach (XmlNode attr in node["attributes"])
			{
				if (attr.NodeType != XmlNodeType.Element)
					continue;
				attrs[attr.Attributes["name"].Value] = string.IsNullOrEmpty(attr["value"].InnerText) ? null : attr["value"].InnerText;
				if (attr.Attributes["name"].Value == "ENTStructureClasses")
				{
					ENTStructureClasses = new List<string>();
					foreach (XmlNode childNode in attr.ChildNodes)
					{
						if (childNode.NodeType != XmlNodeType.Element)
							continue;
						ENTStructureClasses.Add(childNode.InnerText);
					}
				}
				else if (attr.Attributes["name"].Value == "ENTStructureGroupes")
				{
					ENTStructureGroupes = new List<string>();
					foreach (XmlNode childNode in attr.ChildNodes)
					{
						if (childNode.NodeType != XmlNodeType.Element)
							continue;
						ENTStructureGroupes.Add(childNode.InnerText);
					}
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

			var aafStructure = new Structure
			{
				id = attrs["ENTStructureUAI"],
				aaf_jointure_id = Convert.ToInt32(attrs["ENTStructureJointure"]),
				siren = attrs["ENTStructureSIREN"],
				name = ENTStructureNomCourant,
				address = attrs["street"],
				zip_code = attrs["postalCode"],
				city = attrs["l"],
				phone = attrs["telephoneNumber"],
				fax = attrs["facsimileTelephoneNumber"],
				groups = new ModelList<Group>()
			};

			// handle CLASSES
			foreach (var aafClasse in ENTStructureClasses)
			{
				if (string.IsNullOrEmpty(aafClasse))
					continue;

				var tab = aafClasse.Split('$');
				if (tab.Length < 2)
					throw new Exception($"For structure {id} Invalid classes define {aafClasse}");
				var classe = new Group
				{
					id = --groupIdGenerator,
					type = "CLS",
					aaf_name = tab[0],
					structure_id = aafStructure.id,
					description = string.IsNullOrEmpty(tab[1]) ? null : tab[1],
					grades = new ModelList<GroupGrade>()
				};
				// only add if the aaf_name is not already defined because some "classe" are defined
				// multiples times
				if (!aafStructure.groups.Any((arg) => (arg.type == classe.type) && (arg.aaf_name == classe.aaf_name)))
				{
					aafStructure.groups.Add(classe);
					//handle group's grades
					var aafClasseGrades = tab.Skip(2);
					foreach (var classeGrade in aafClasseGrades)
					{
						var grade = GetAafGradeById(classeGrade);
						if (grade == null)
							errors.Add($"ERROR: GRADE WITH ID {classeGrade} NOT FOUND BUT USED IN GROUP "+
							           $"(ID: {classe.id}, STRUCTURE: {classe.structure_id}, NAME: {classe.aaf_name}");
						else
							classe.grades.Add(new GroupGrade { grade_id = grade.id });
					}
				}
				//else
				//	errors.Add($"ERROR: GROUP NAME DUPLICATE (STRUCTURE: {classe.structure_id}, TYPE: {classe.type}, NAME: {classe.aaf_name})");
			}

			// handle GROUPES ELEVES
			foreach (var aafGroupe in ENTStructureGroupes)
			{
				if (string.IsNullOrEmpty(aafGroupe))
					continue;

				var tab = aafGroupe.Split('$');
				if (tab.Length < 2)
					throw new Exception($"For structure {id} invalid groupe define {aafGroupe}");
				var groupe = new Group
				{
					id = --groupIdGenerator,
					type = "GRP",
					aaf_name = tab[0],
					structure_id = aafStructure.id,
					description = string.IsNullOrEmpty(tab[1]) ? null : tab[1],
					grades = new ModelList<GroupGrade>()
				};
				// only add if the aaf_name is not already defined because some "groupe" are defined
				// multiples times
				if (!aafStructure.groups.Any((arg) => (arg.type == groupe.type) && (arg.aaf_name == groupe.aaf_name)))
					aafStructure.groups.Add(groupe);
				//else
				//	errors.Add($"ERROR: GROUP NAME DUPLICATE (STRUCTURE: {groupe.structure_id}, TYPE: {groupe.type}, NAME: {groupe.aaf_name})");
			}
			return aafStructure;
		}

		ModelList<User> GetAafUsers(IEnumerable<XmlNode> nodes)
		{
			LoadAafSubjects();
			LoadAafStructures();

			var aafUsers = new ModelList<User>();
			foreach (XmlNode node in nodes)
			{
				if (node == null)
					continue;
				if (node.NodeType != XmlNodeType.Element)
					continue;
				var user = NodeToUser(node, aafStructuresByAafId);
				// dont keep user without profiles in our structures
				if ((user.profiles == null) || (user.profiles.Count > 0))
					aafUsers.Add(user);
			}
			return aafUsers;
		}

		public ModelList<User> GetAafTeachers()
		{
			return GetAafUsers(zipFile.LoadNodes(@"_PersEducNat_\d+.xml$"));
		}

		public async Task<ModelList<User>> GetAafStudentsAsync(DB db)
		{
			await LoadAafEleveAsync(db);
			return aafStudents;
		}

		public async Task<ModelList<User>> GetAafParentsAsync(DB db)
		{
			await LoadAafPersRelEleveAsync(db);
			// load students too to complete the parents profiles
			await LoadAafEleveAsync(db);
			return aafParents;
		}

		public static void DaySyncTask(Logger logger, string syncFilesFolder, string zipFilesFolder, string logFilesFolder, string dbUrl)
		{
			var task = DaySyncTaskAsync(logger, syncFilesFolder, zipFilesFolder, logFilesFolder, dbUrl);
			task.Wait();
		}

		public static async Task DaySyncTaskAsync(Logger logger, string syncFilesFolder, string zipFilesFolder, string logFilesFolder, string dbUrl)
		{
			var files = GetFiles(syncFilesFolder, zipFilesFolder);

			DateTime latestFileDateSync = DateTime.MinValue;

			using (DB db = await DB.CreateAsync(dbUrl, true))
			{
				var res = await db.SelectAsync<AafSync>("SELECT * FROM `aaf_sync` ORDER BY `file_date` DESC LIMIT 1");
				if (res.Count > 0)
					latestFileDateSync = res[0].file_date;

				var now = DateTime.Now;
				SyncFile nearestFile = null;
				foreach (var file in files)
				{
					// if the file date is unknown, dont take
					if (file.date == null)
						continue;

					// if the file date is older than the latest sync already run
					// dont take it, we want to ensure that a sync will not go back in the past
					if (file.date <= latestFileDateSync)
						continue;

					var fileDeltaNow = (DateTime)file.date - now;

					// if file is too old or in the future, dont take it
					if ((fileDeltaNow > TimeSpan.FromDays(7)) || (fileDeltaNow < TimeSpan.FromDays(-7)))
						continue;

					if (nearestFile == null)
						nearestFile = file;
					else if (fileDeltaNow > (DateTime)nearestFile.date - now)
						nearestFile = file;
				}

				if (nearestFile != null)
				{
					await SynchronizeFileAsync(logger, nearestFile, zipFilesFolder, logFilesFolder,
					                           db, dbUrl, SyncFileMode.Automatic);
				}
				db.Commit();
			}
		}

		public static async Task<AafSync> SynchronizeFileAsync(
			Logger logger, string syncFilesFolder, string zipFilesFolder, string logFilesFolder,
			string fileName, string dbUrl, List<string> structuresIds = null)
		{
			var files = GetFiles(syncFilesFolder, zipFilesFolder);
			var file = files.Find((obj) => obj.id == fileName);
			AafSync aafSync = null;
			if (file != null)
			{
				using (DB db = await DB.CreateAsync(dbUrl, true))
				{
					aafSync = await SynchronizeFileAsync(
						logger, file, zipFilesFolder, logFilesFolder, db, dbUrl,
						SyncFileMode.Manual, structuresIds);
					db.Commit();
				}
			}
			return aafSync;
		}

		public static async Task<AafSync> SynchronizeFileAsync(
			Logger logger, SyncFile file, string zipFilesFolder, string logFilesFolder,
			DB db, string dbUrl, SyncFileMode mode, List<string> structuresIds = null)
		{
			AafSync aafSync = null;

			Synchronizer sync = null;
			SyncDiff diff = null;
			Exception exception = null;
			try
			{
				sync = new Synchronizer(dbUrl, file, structuresIds);
				diff = await sync.SynchronizeAsync(
					db, subject: true, grade: true, structure: true,
					persEducNat: true, eleve: true, persRelEleve: true, apply: true
				);
			}
			catch (Exception e)
			{
				exception = e;
			}

			aafSync = new AafSync
			{
				file = file.id,
				file_date = (DateTime)file.date,
				format = file.format,
				mode = mode
			};

			if (exception != null)
			{
				aafSync.exception = exception.ToString();
				// save the structures that could have been concerned
				var structs = new ModelList<AafSyncStructure>();
				foreach (var struc in await db.SelectAsync<Structure>("SELECT * FROM `structure` WHERE `aaf_sync_activated`=TRUE"))
					structs.Add(new AafSyncStructure { structure_id = struc.id });
				aafSync.structures = structs;
			}
			else
			{
				// save the structures concerned by the sync
				aafSync.structures = new ModelList<AafSyncStructure>(sync.interStructuresIds.Select((arg) => new AafSyncStructure { structure_id = arg }));
			}

			if (await aafSync.SaveAsync(db, true))
			{
				if (exception != null)
					logger.Log(LogLevel.Alert, $"Exception while running AAF sync (id: {aafSync.id}) with file '{file.id}':\n" + exception);

				// save the diff
				if (exception == null && diff != null)
					File.WriteAllText(Path.Combine(logFilesFolder, aafSync.id.ToString() + ".diff"), diff.ToJson().ToString());
			}
			return aafSync;
		}
	}
}
