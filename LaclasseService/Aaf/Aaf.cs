// Aaf.cs
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017 Metropole de Lyon
// Copyright (c) 2017 Daniel LACROIX
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
using System.Collections.Generic;
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Aaf
{
	public class Aaf : HttpRouting
	{
		readonly string zipFilesFolder;

		public Aaf(string dbUrl, string syncFilesFolder, string zipFilesFolder)
		{
			this.zipFilesFolder = zipFilesFolder;

			GetAsync["/"] = async (p, c) =>
			{
				await c.EnsureIsStructureAdminAsync();
				c.Response.StatusCode = 200;
				c.Response.Content = Synchronizer.GetFiles(syncFilesFolder, zipFilesFolder).Filter(c);
			};

			GetAsync["/{id}/structures"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				var synchronizer = new Synchronizer(dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]));
				c.Response.Content = synchronizer.GetAafStructures().Filter(c);
			};

			GetAsync["/{id}/structures/diff"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				List<string> ids = null;
				if (c.Request.QueryStringArray.ContainsKey("id"))
					ids = c.Request.QueryStringArray["id"];
				
				var synchronizer = new Synchronizer(
					dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]), ids);
				var res = await synchronizer.SynchronizeAsync(
					structure: true);
				c.Response.Content = res.structures;
			};

			GetAsync["/{id}/structures/sync"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				List<string> ids = null;
				if (c.Request.QueryStringArray.ContainsKey("id"))
					ids = c.Request.QueryStringArray["id"];

				var synchronizer = new Synchronizer(dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]), ids);
				var res = await synchronizer.SynchronizeAsync(
					structure: true, apply: true);
				c.Response.Content = res.structures;
			};

			GetAsync["/{id}/subjects"] = async (p, c) =>
			{
				await c.EnsureIsStructureAdminAsync();
				var synchronizer = new Synchronizer(dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]));
				c.Response.Content = synchronizer.GetAafSubjects().Filter(c);
			};

			GetAsync["/{id}/subjects/diff"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				var synchronizer = new Synchronizer(dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]));
				var res = await synchronizer.SynchronizeAsync(subject: true);
				c.Response.Content = res.subjects;
			};

			GetAsync["/{id}/subjects/sync"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				var synchronizer = new Synchronizer(dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]));
				var res = await synchronizer.SynchronizeAsync(apply: true, subject: true);
				c.Response.Content = res.subjects;
			};

			GetAsync["/{id}/grades"] =  async (p, c) =>
			{
				await c.EnsureIsStructureAdminAsync();
				var synchronizer = new Synchronizer(dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]));
				c.Response.Content = synchronizer.GetAafGrades().Filter(c);
			};

			GetAsync["/{id}/grades/diff"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				var synchronizer = new Synchronizer(dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]));
				var res = await synchronizer.SynchronizeAsync(grade: true);
				c.Response.Content = res.grades;
			};

			GetAsync["/{id}/grades/sync"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				var synchronizer = new Synchronizer(dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]));
				var res = await synchronizer.SynchronizeAsync(apply: true, grade: true);
				c.Response.Content = res.grades;
			};

			GetAsync["/{id}/teachers"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				var synchronizer = new Synchronizer(dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]));
				c.Response.Content = synchronizer.GetAafTeachers().Filter(c);
			};

			GetAsync["/{id}/teachers/diff"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				var synchronizer = new Synchronizer(dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]));
				var res = await synchronizer.SynchronizeAsync(persEducNat: true);
				c.Response.Content = res.persEducNat;
			};

			GetAsync["/{id}/students"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				var synchronizer = new Synchronizer(dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]));
				c.Response.Content = synchronizer.GetAafStudents().Filter(c);
			};

			GetAsync["/{id}/parents"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				var synchronizer = new Synchronizer(dbUrl, Path.Combine(zipFilesFolder, (string)p["id"]));
				c.Response.Content = synchronizer.GetAafParents().Filter(c);
			};

			GetAsync["/{id}"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				var aafFile = GetFile((string)p["id"]);
				if (aafFile != null)
				{
					List<string> ids = null;
					if (c.Request.QueryStringArray.ContainsKey("structure_id"))
						ids = c.Request.QueryStringArray["structure_id"];

					var apply = false;
					if (c.Request.QueryString.ContainsKey("apply"))
						apply = Convert.ToBoolean(c.Request.QueryString["apply"]);

					var format = SyncFileFormat.Full;
					if (c.Request.QueryString.ContainsKey("format"))
						format = (SyncFileFormat)Enum.Parse(typeof(SyncFileFormat), c.Request.QueryString["format"]);

					bool subject = false;
					if (c.Request.QueryString.ContainsKey("subject"))
						subject = Convert.ToBoolean(c.Request.QueryString["subject"]);
					bool grade = false;
					if (c.Request.QueryString.ContainsKey("grade"))
						grade = Convert.ToBoolean(c.Request.QueryString["grade"]);
					bool structure = false;
					if (c.Request.QueryString.ContainsKey("structure"))
						structure = Convert.ToBoolean(c.Request.QueryString["structure"]);
					bool persEducNat = false;
					if (c.Request.QueryString.ContainsKey("persEducNat"))
						persEducNat = Convert.ToBoolean(c.Request.QueryString["persEducNat"]);
					bool eleve = false;
					if (c.Request.QueryString.ContainsKey("eleve"))
						eleve = Convert.ToBoolean(c.Request.QueryString["eleve"]);
					bool persRelEleve = false;
					if (c.Request.QueryString.ContainsKey("persRelEleve"))
						persRelEleve = Convert.ToBoolean(c.Request.QueryString["persRelEleve"]);
					
					var sync = new Synchronizer(dbUrl, aafFile.FullName, ids);

					var diff = await sync.SynchronizeAsync(
						subject: subject, grade: grade, structure: structure,
						persEducNat: persEducNat, eleve: eleve, persRelEleve: persRelEleve,
						apply: apply, format: format);

					c.Response.StatusCode = 200;
					c.Response.Content = diff;
				}
			};
		}

		FileInfo GetFile(string id)
		{
			var dir = new DirectoryInfo(zipFilesFolder);
			foreach (var file in dir.EnumerateFiles("*.zip"))
				if (file.Name == id)
					return file;
			return null;
		}
	}
}
