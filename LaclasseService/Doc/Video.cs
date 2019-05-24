using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Utils;

namespace Laclasse.Doc
{
    public enum LongTaskState
    {
        waiting,
        running,
        done
    }

    public class VideoEncoder
    {
        readonly object instanceLock = new object();
        double timeProgress;
        double timeTotal;
        long frame;
        LongTaskState state = LongTaskState.waiting;

        public readonly DateTime CTime = DateTime.Now;
        public readonly string Id;
        public readonly Video Video;
        public readonly VideoEncoderService Service;
        public Blob Blob;
        TaskCompletionSource<Blob> tcs = new TaskCompletionSource<Blob>();

        public VideoEncoder(VideoEncoderService service, Video video)
        {
            Id = Guid.NewGuid().ToString();
            Service = service;
            Video = video;
        }

        public LongTaskState State
        {
            get
            {
                LongTaskState _value;
                lock (instanceLock)
                    _value = state;
                return _value;
            }
            set
            {
                lock (instanceLock)
                    state = value;
            }
        }

        public double Progress
        {
            get
            {
                double _value = 0;
                lock (instanceLock)
                {
                    if (Math.Abs(timeTotal) > 0d)
                        _value = Math.Min(1d, timeProgress / timeTotal);
                }
                return _value;
            }
        }

        public double TimeProgress
        {
            get
            {
                double _value;
                lock (instanceLock)
                    _value = timeProgress;
                return _value;
            }
            set
            {
                lock (instanceLock)
                    timeProgress = value;
            }
        }

        public double TimeTotal
        {
            get
            {
                double _value;
                lock (instanceLock)
                    _value = timeTotal;
                return _value;
            }
            set
            {
                lock (instanceLock)
                    timeTotal = value;
            }
        }

        public long Frame
        {
            get
            {
                long _value;
                lock (instanceLock)
                    _value = frame;
                return _value;
            }
            set
            {
                lock (instanceLock)
                    frame = value;
            }
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
            var progressUrl = new Uri(new Uri(Video.context.docs.globalSetup.server.internalUrl), $"/api/videoencoder/{Id}/progress");
            Console.WriteLine($"BuildMp4 {filepath}, url: {progressUrl}");

            string videoFile;
            try
            {
                Preview.ImageVideoPreview.GetVideoSize(filepath, out double width, out double height);
                TimeTotal = Preview.ImageVideoPreview.GetVideoDuration(filepath);

                videoFile = Path.Combine(Video.context.tempDir, Guid.NewGuid().ToString());
                if (Video.MimeToExtension.ContainsKey(Video.node.mime))
                    videoFile += "." + Video.MimeToExtension[Video.node.mime];

                List<string> args = new List<string>
                {
                    "-progress",
                    progressUrl.ToString(),
                    "-loglevel",
                    "quiet",
                    "-threads",
                    "1",
                    "-i",
                    filepath,
                    "-f",
                    "mp4",
                    "-vcodec",
                    "libx264",
                    "-preset",
                    "slow",
                    "-profile:v",
                    "baseline",
                    "-map_metadata",
                    "-1",
                    "-ab",
                    "64k",
                    "-ar",
                    "44100",
                    "-ac",
                    "1"
                };
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
                int resizedWidth = (int)(Math.Ceiling(resizedHeight * (width / height) / 16) * 16);
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
            catch (Exception e)
            {
                videoFile = null;
                Video.context.docs.logger.Log(LogLevel.Error, $"Error while encoding node {Video.node.name} (id: {Video.node.id}),  Exception: {e}");
            }
            return (videoFile != null && File.Exists(videoFile)) ? videoFile : null;
        }

        public void Run()
        {
            var streamTask = Video.GetContentAsync();
            streamTask.Wait();
            var stream = streamTask.Result;
            if (stream == null)
            {
                State = LongTaskState.done;
                return;
            }

            State = LongTaskState.running;
            string tempFile;
            using (stream)
            {
                tempFile = Path.Combine(Video.context.tempDir, Id);
                if (Video.MimeToExtension.ContainsKey(Video.node.mime))
                    tempFile += "." + Video.MimeToExtension[Video.node.mime];

                using (var tmpStream = File.OpenWrite(tempFile))
                    stream.CopyTo(tmpStream);
            }

            try
            {
                Blob videoMp4Blob = new Blob
                {
                    id = Guid.NewGuid().ToString(),
                    parent_id = Video.node.blob_id,
                    mimetype = "video/mp4",
                    name = "webvideo"
                };
                var videoMp4File = BuildMp4(tempFile);
                if (videoMp4File != null)
                {
                    using (DB db = DB.Create(Video.context.docs.dbUrl, true))
                    {
                        var task = Video.context.blobs.CreateBlobFromTempFileAsync(db, videoMp4Blob, videoMp4File);
                        task.Wait();
                        videoMp4Blob = task.Result;
                        db.CommitAsync().Wait();
                    }
                    Blob = videoMp4Blob;
                }
            }
            finally
            {
                State = LongTaskState.done;
                File.Delete(tempFile);
            }
            tcs.TrySetResult(Blob);
        }

        public Task<Blob> RunAsync()
        {
            return tcs.Task;
        }

        public JsonObject ToJson()
        {
            return new JsonObject
            {
                ["id"] = Id,
                ["node_id"] = Video.node.id,
                ["ctime"] = CTime,
                ["progress"] = Progress,
                ["state"] = State.ToString()
            };
        }
    }

    public class VideoEncoderService : HttpRouting
    {
        readonly object instanceLock = new object();
        Dictionary<long, VideoEncoder> runningEncoders = new Dictionary<long, VideoEncoder>();
        Dictionary<string, VideoEncoder> runningEncodersById = new Dictionary<string, VideoEncoder>();

        PriorityTaskScheduler scheduler;

        public VideoEncoderService(PriorityTaskScheduler scheduler)
        {
            this.scheduler = scheduler;

            Get["/{id}"] = (p, c) =>
            {
                var id = (string)p["id"];
                VideoEncoder encoder = null;
                lock (instanceLock)
                {
                    if (runningEncodersById.ContainsKey(id))
                        encoder = runningEncodersById[id];
                }
                if (encoder != null)
                {
                    c.Response.StatusCode = 200;
                    c.Response.Content = encoder.ToJson();
                }
            };

            PostAsync["/{id}/progress"] = async (p, c) =>
            {
                var values = new Dictionary<string, string>();

                string id = (string)p["id"];
                VideoEncoder encoder = null;
                lock (instanceLock)
                {
                    if (runningEncodersById.ContainsKey(id))
                        encoder = runningEncodersById[id];
                }
                if (encoder == null)
                    return;

                using (StreamReader sr = new StreamReader(c.Request.InputStream))
                {
                    var end = false;
                    while (!sr.EndOfStream && !end)
                    {
                        var line = await sr.ReadLineAsync();
                        var pos = line.IndexOf('=');
                        if (pos > 0)
                        {
                            var cmd = line.Substring(0, pos);
                            var value = line.Substring(pos + 1);
                            if (cmd == "progress")
                            {
                                end |= value == "end";
                                // get the total micro secondes position in the video
                                if (values.ContainsKey("out_time_ms") && long.TryParse(values["out_time_ms"], out long outTimeMs))
                                    encoder.TimeProgress = ((double)outTimeMs) / 1000000.0d;
                                // get current frame position in the video
                                if (values.ContainsKey("frame") && long.TryParse(values["frame"], out long frame))
                                    encoder.Frame = frame;
                                values.Clear();
                            }
                            else
                                values[cmd] = value;
                        }
                    }
                }
            };
        }

        public VideoEncoder ScheduleEncode(Video video)
        {
            VideoEncoder encoder = null;
            lock (instanceLock)
            {
                lock (instanceLock)
                {
                    if (!runningEncoders.ContainsKey(video.node.id))
                    {
                        encoder = new VideoEncoder(this, video);
                        LongTask task = new LongTask(() => {
                            encoder.Run();
                            lock (instanceLock)
                            {
                                runningEncoders.Remove(encoder.Video.node.id);
                                runningEncodersById.Remove(encoder.Id);
                            }
                        }, video.node.owner, $"Build MP4 for '{video.node.name}' (id: {video.node.id})");
                        scheduler.Start(task);
                        runningEncoders[video.node.id] = encoder;
                        runningEncodersById[encoder.Id] = encoder;
                    }
                    else
                        encoder = runningEncoders[video.node.id];
                }
            }
            return encoder;
        }
    }

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

        public async Task<(Stream,VideoEncoder)> GetWebVideoStreamOrEncoderAsync()
        {
            await node.blob.LoadExpandFieldAsync(context.db, "children");

            var videoBlob = node.blob.children.Find(child => child.name == "webvideo");
            if (videoBlob != null)
                return (context.blobs.GetBlobStream(videoBlob.id), null);

            return (null, context.docs.videoEncoder.ScheduleEncode(this));
        }

        public async Task<Stream> GetWebVideoStreamAsync()
        {
            var (stream, encoder) = await GetWebVideoStreamOrEncoderAsync();
            if (stream != null)
                return stream;
            if (encoder != null)
            {
                await encoder.RunAsync();
                if (encoder.Blob != null)
                    return context.blobs.GetBlobStream(encoder.Blob.id);
            }
            return null;
        }
    }
}