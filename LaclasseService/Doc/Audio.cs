using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;

namespace Laclasse.Doc
{
    public class Audio : Document
    {
        public static Dictionary<string, string> MimeToExtension = new Dictionary<string, string>()
        {
            ["audio/midi"] = "midi",
            ["audio/mp3"] = "mp3",
            ["audio/mp4"] = "m4a",
            ["audio/mpeg"] = "mp3",
            ["audio/webm"] = "weba",
            ["audio/ogg"] = "ogg",
            ["audio/x-wav"] = "wav",
            ["audio/aac"] = "aac",
            ["audio/3gpp"] = "3gp",
            ["audio/3gpp2"] = "3g2",
            ["audio/x-ms-wma"] = "wma",
        };

        public Audio(Context context, Node node) : base(context, node)
        {
        }

        public async Task<Stream> GetWebAudioStreamAsync()
        {
            await node.blob.LoadExpandFieldAsync(context.db, "children");

            var audioBlob = node.blob.children.Find(child => child.name == "webaudio");
            if (audioBlob != null)
                return context.blobs.GetBlobStream(audioBlob.id);

            Stream audioStream = null;
            var stream = await GetContentAsync();
            if (stream != null)
            {
                using (stream)
                {
                    var tempFile = Path.Combine(context.tempDir, Guid.NewGuid().ToString());
                    if (MimeToExtension.ContainsKey(node.mime))
                        tempFile += "." + MimeToExtension[node.mime];

                    using (var tmpStream = File.OpenWrite(tempFile))
                        await stream.CopyToAsync(tmpStream);
                    try
                    {
                        Blob audioMp3Blob = new Blob
                        {
                            id = Guid.NewGuid().ToString(),
                            parent_id = node.blob_id,
                            mimetype = "audio/mp3",
                            name = "webaudio"
                        };
                        var audioMp3File = BuildMp3(tempFile);
                        if (audioMp3File != null)
                        {
                            audioMp3Blob = await context.blobs.CreateBlobFromTempFileAsync(context.db, audioMp3Blob, audioMp3File);
                            audioStream = context.blobs.GetBlobStream(audioMp3Blob.id);
                        }
                    }
                    finally
                    {
                        File.Delete(tempFile);
                    }
                }
            }
            return audioStream;
        }

        static string BuildArguments(string[] args)
        {
            string res = "";
            foreach (string arg in args)
            {
                string tmp = (string)arg.Clone();
                tmp = tmp.Replace("'", "\\'");
                if (res != "")
                    res += " ";
                res += "'" + tmp + "'";
            }
            return res;
        }

        string BuildMp3(string filepath)
        {
            string audioFile = null;
            try
            {
                audioFile = Path.Combine(context.tempDir, Guid.NewGuid().ToString());
                ProcessStartInfo startInfo = new ProcessStartInfo("/usr/bin/ffmpeg", BuildArguments(new string[] {
                    "-loglevel", "quiet", "-threads", "1",
                    "-i", filepath, "-map", "a",
                    "-f", "mp3", "-ab", "64k",
                    "-ar", "44100", "-ac", "1",
                    audioFile
                }));

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();
                }
            }
            catch {
                audioFile = null;
            }
            return (audioFile != null && File.Exists(audioFile)) ? audioFile : null;
        }
    }
}