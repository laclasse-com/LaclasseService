using System.Xml;
using System.Linq;
using System.Collections.Generic;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;

namespace Laclasse.Aaf
{
	public class AafGlobalZipFile
	{
		readonly string file;

		public AafGlobalZipFile(string file)
		{
			this.file = file;
		}

		public IEnumerable<XmlNode> LoadNodes(string regex)
		{
			using (var archive = SharpCompress.Archives.ArchiveFactory.Open(file))
			{
				foreach (var entry in archive.Entries)
				{
					if (!System.Text.RegularExpressions.Regex.IsMatch(entry.Key, regex))
						continue;

					using (var entryStream = entry.OpenEntryStream())
					{
						var settings = new XmlReaderSettings();
						settings.IgnoreComments = true;
						settings.DtdProcessing = DtdProcessing.Ignore;
						var reader = XmlReader.Create(entryStream, settings);
						var doc = new XmlDocument();

						while (reader.Read())
						{
							if (reader.NodeType == XmlNodeType.Element && (reader.Name == "addRequest" || reader.Name == "modifyRequest"))
								yield return doc.ReadNode(reader);
						}
					}
				}
			}
		}
	}
}
