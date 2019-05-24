using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;

namespace Laclasse.Doc
{
    public class Video : Document
    {
        public static Dictionary<string, string> MimeToExtension = new Dictionary<string, string>()
        {
            ["video/mp4"] = "mp4",
            ["video/x-flv"] = "flv",
            ["video/x-msvideo"] = "wmv",
            ["video/x-ms-wmv"] = "wmv",
            ["video/ogg"] = "ogv",
            ["video/quicktime"] = "mov",
            ["video/x-matroska"] = "mkv",
            ["video/webm"] = "webm",
            ["video/3gpp"] = "3gpp",
            ["video/mpeg"] = "mpg",
            ["video/x-ms-asf"] = "asf",
            ["video/avi"] = "avi"
        };

        public Video(Context context, Node node) : base(context, node)
        {
        }

        public async Task<Stream> GetWebVideoStreamAsync()
        {
            await node.blob.LoadExpandFieldAsync(context.db, "children");

            var videoBlob = node.blob.children.Find(child => child.name == "webvideo");
            if (videoBlob != null)
                return context.blobs.GetBlobStream(videoBlob.id);

            Stream videoStream = null;
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
                        Blob videoMp4Blob = new Blob
                        {
                            id = Guid.NewGuid().ToString(),
                            parent_id = node.blob_id,
                            mimetype = "video/mp4",
                            name = "webvideo"
                        };
                        var videoMp4File = BuildMp4(tempFile);
                        if (videoMp4File != null)
                        {
                            videoMp4Blob = await context.blobs.CreateBlobFromTempFileAsync(context.db, videoMp4Blob, videoMp4File);
                            videoStream = context.blobs.GetBlobStream(videoMp4Blob.id);
                        }
                    }
                    finally
                    {
                        File.Delete(tempFile);
                    }
                }
            }
            return videoStream;
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

        string BuildMp4(string filepath)
        {
            string videoFile;
            try
            {
                double width; double height;
                Preview.ImageVideoPreview.GetVideoSize(filepath, out width, out height);

                videoFile = Path.Combine(context.tempDir, Guid.NewGuid().ToString());
                if (MimeToExtension.ContainsKey(node.mime))
                    videoFile += "." + MimeToExtension[node.mime];

                List<string> args = new List<string>();
                args.Add("-loglevel"); args.Add("quiet");
                args.Add("-threads"); args.Add("1");
                args.Add("-i"); args.Add(filepath);
                args.Add("-f"); args.Add("mp4");
                args.Add("-vcodec"); args.Add("libx264");
                args.Add("-preset"); args.Add("slow");
                args.Add("-profile:v"); args.Add("baseline");
                args.Add("-map_metadata"); args.Add("-1");
                args.Add("-ab"); args.Add("64k");
                args.Add("-ar"); args.Add("44100");
                args.Add("-ac"); args.Add("1");
                // variable depending on the quality expected
                int resizedHeight;
                // 720p
                if (height >= 720)
                {
                    resizedHeight = 720;
                    args.Add("-b:v"); args.Add("2560k");
                }
                // 480p
                else if (height >= 480)
                {
                    resizedHeight = 480;
                    args.Add("-b:v"); args.Add("1280k");
                }
                // 240p
                else
                {
                    resizedHeight = 240;
                    args.Add("-b:v"); args.Add("640k");
                }
                int resizedWidth = (int)(Math.Ceiling((((double)resizedHeight) * (width / height)) / 16) * 16);
                args.Add("-s"); args.Add(resizedWidth + "x" + resizedHeight);

                args.Add(videoFile);
                ProcessStartInfo startInfo = new ProcessStartInfo("/usr/bin/ffmpeg", BuildArguments(args.ToArray()));

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();
                }
            }
            catch
            {
                videoFile = null;
            }
            return (videoFile != null && File.Exists(videoFile)) ? videoFile : null;
        }
    }
}