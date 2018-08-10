using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Directory;
using Laclasse.Authentication;

namespace Laclasse.Aaf
{
	public enum SyncFileFormat
	{
		Full,
		Delta
	}

	public enum SyncFileMode
	{
		Automatic,
		Manual
	}

	[Model(Table = "aaf_sync", PrimaryKey = nameof(id))]
	public class AafSync : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField(Required = true)]
		public string file { get { return GetField<string>(nameof(file), null); } set { SetField(nameof(file), value); } }
		[ModelField]
		public DateTime file_date { get { return GetField(nameof(file_date), DateTime.MinValue); } set { SetField(nameof(file_date), value); } }
		[ModelField]
		public DateTime ctime { get { return GetField(nameof(ctime), DateTime.Now); } set { SetField(nameof(ctime), value); } }
		[ModelField]
		public SyncFileFormat format { get { return GetField(nameof(format), SyncFileFormat.Full); } set { SetField(nameof(format), value); } }
		[ModelField]
		public SyncFileMode mode { get { return GetField(nameof(mode), SyncFileMode.Automatic); } set { SetField(nameof(mode), value); } }
		[ModelField]
		public string exception { get { return GetField<string>(nameof(exception), null); } set { SetField(nameof(exception), value); } }
		[ModelExpandField(Name = nameof(structures), ForeignModel = typeof(AafSyncStructure))]
		public ModelList<AafSyncStructure> structures { get { return GetField<ModelList<AafSyncStructure>>(nameof(structures), null); } set { SetField(nameof(structures), value); } }

		public override async Task EnsureRightAsync(HttpContext context, Right right, Model diff)
		{
			if (right == Right.Create)
				throw new WebException(405, "Method not allowed");
			if (right == Right.Delete || right == Right.Update)
				await context.EnsureIsSuperAdminAsync();
			else
			{
				var authUser = await context.EnsureIsAuthenticatedAsync();
				// exception detail only available to super admin
				if (!authUser.IsSuperAdmin && exception != null)
					exception = "INTERNAL ERROR";

				// TODO: change this by the next version
				if (authUser.user.profiles != null)
				{
					foreach (var profile in authUser.user.profiles)
					{
						if (authUser.HasRightsOnStructure(profile.structure_id, false, false, true))
							return;
					}
				}

				//foreach (var struc in structures)
				//{
				//	if (authUser.HasRightsOnStructure(struc.structure_id, false, false, true))
				//		return;
				//}
				await context.EnsureIsSuperAdminAsync();
			}
		}
	}

	[Model(Table = "aaf_sync_structure", PrimaryKey = nameof(id))]
	public class AafSyncStructure : Model
	{
		[ModelField]
		public int id { get { return GetField(nameof(id), 0); } set { SetField(nameof(id), value); } }
		[ModelField(Required = true, ForeignModel = typeof(AafSync))]
		public int aaf_sync_id { get { return GetField(nameof(aaf_sync_id), 0); } set { SetField(nameof(aaf_sync_id), value); } }
		[ModelField(Required = true)]
		public string structure_id { get { return GetField<string>(nameof(structure_id), null); } set { SetField(nameof(structure_id), value); } }
	}

	public class AafSyncService: ModelService<AafSync>
	{
		public AafSyncService(string dbUrl, string logPath, Logger logger, string syncFilesFolder, string zipFilesFolder, string logFilesFolder): base(dbUrl)
		{
/*			GetAsync["/"] = async (p, c) =>
			{
				await Task.FromResult(false);
				c.Response.StatusCode = 200;
				c.Response.Content = "HELLO";
			};*/

			GetAsync["/{id:int}/diff"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();
				var filePath = Path.Combine(logPath, p["id"] + ".diff");
				if (File.Exists(filePath))
				{
					c.Response.StatusCode = 200;
					c.Response.Headers["content-type"] = "application/json; charset=utf-8";
					c.Response.Content = File.OpenRead(filePath);
				}
			};

			PostAsync["/"] = async (p, c) =>
			{
				await c.EnsureIsSuperAdminAsync();

				var json = await c.Request.ReadAsJsonAsync();

				if (json.ContainsKey("file") && json["file"].JsonType == JsonType.String)
				{
					List<string> structuresIds = null;

					if (json.ContainsKey("structures") && json["structures"].JsonType == JsonType.Array)
					{
						structuresIds = new List<string>();
						foreach (var alimStructure in (JsonArray)json["structures"])
						{
							if (alimStructure.JsonType == JsonType.Object && alimStructure.ContainsKey("structure_id"))
							{
								string structure_id = alimStructure["structure_id"];
								structuresIds.Add(structure_id);
							}
						}
					}

					var aafSync = await Synchronizer.SynchronizeFileAsync(
						logger, syncFilesFolder, zipFilesFolder, logFilesFolder,
						json["file"], dbUrl, structuresIds);

					if (aafSync != null)
					{
						c.Response.StatusCode = 200;
						c.Response.Content = aafSync;
					}
					else
						c.Response.StatusCode = 404;
				}
			};
		}
	}
}
