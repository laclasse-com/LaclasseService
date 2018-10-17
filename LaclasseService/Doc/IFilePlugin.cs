using System;

namespace Laclasse.Doc
{
	public interface IFilePlugin
    {
		string Name { get; }

        string[] MimeTypes { get; }

//        void ProcessContent(JsonValue data, JsonValue diff, string contentFilePath);
    }
}

