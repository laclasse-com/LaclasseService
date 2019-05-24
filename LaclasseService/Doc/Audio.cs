using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Erasme.Json;
using Erasme.Http;
using Laclasse.Utils;

namespace Laclasse.Doc
{
    public class AudioEncoder
    {
        readonly object instanceLock = new object();
        double timeProgress;
        double timeTotal;
        long frame;
        LongTaskState state = LongTaskState.waiting;

        public readonly DateTime CTime = DateTime.Now;
        public readonly string Id;
        public readonly Audio Audio;
        public readonly AudioEncoderService Service;
        public Blob Blob;
        TaskCompletionSource<Blob> tcs = new TaskCompletionSource<Blob>();

        public AudioEncoder(AudioEncoderService service, Audio audio)
        {
            Id = Guid.NewGuid().ToString();
            Service = service;
            Audio = audio;
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

        string BuildMp3(string filepath)
        {
            var progressUrl = new Uri(new Uri(Audio.context.docs.globalSetup.server.internalUrl), $"/api/audioencoder/{Id}/progress");

            string audioFile = null;
            try
            {
                audioFile = Path.Combine(Audio.context.tempDir, Guid.NewGuid().ToString());
                ProcessStartInfo startInfo = new ProcessStartInfo("/usr/bin/ffmpeg", BuildArguments(new string[] {
                    "-progress", progressUrl.ToString(),
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
            catch (Exception e)
            {
                audioFile = null;
                Audio.context.docs.logger.Log(LogLevel.Error, $"Error while encoding node {Audio.node.name} (id: {Audio.node.id}),  Exception: {e}");
            }
            return (audioFile != null && File.Exists(audioFile)) ? audioFile : null;
        }

        public void Run()
        {
            var streamTask = Audio.GetContentAsync();
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
                tempFile = Path.Combine(Audio.context.tempDir, Id);
                if (Audio.MimeToExtension.ContainsKey(Audio.node.mime))
                    tempFile += "." + Audio.MimeToExtension[Audio.node.mime];

                using (var tmpStream = File.OpenWrite(tempFile))
                    stream.CopyTo(tmpStream);
            }

            try
            {
                Blob audioMp3Blob = new Blob
                {
                    id = Guid.NewGuid().ToString(),
                    parent_id = Audio.node.blob_id,
                    mimetype = "audio/mpeg",
                    name = "webaudio"
                };
                var audioMp3File = BuildMp3(tempFile);
                if (audioMp3File != null)
                {
                    using (DB db = DB.Create(Audio.context.docs.dbUrl, true))
                    {
                        var task = Audio.context.blobs.CreateBlobFromTempFileAsync(db, audioMp3Blob, audioMp3File);
                        task.Wait();
                        audioMp3Blob = task.Result;
                        db.CommitAsync().Wait();
                    }
                    Blob = audioMp3Blob;
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
                ["node_id"] = Audio.node.id,
                ["ctime"] = CTime,
                ["progress"] = Progress,
                ["state"] = State.ToString()
            };
        }
    }

    public class AudioEncoderService : HttpRouting
    {
        readonly object instanceLock = new object();
        Dictionary<long, AudioEncoder> runningEncoders = new Dictionary<long, AudioEncoder>();
        Dictionary<string, AudioEncoder> runningEncodersById = new Dictionary<string, AudioEncoder>();

        PriorityTaskScheduler scheduler;

        public AudioEncoderService(PriorityTaskScheduler scheduler)
        {
            this.scheduler = scheduler;

            Get["/{id}"] = (p, c) =>
            {
                var id = (string)p["id"];
                AudioEncoder encoder = null;
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
                AudioEncoder encoder = null;
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
                                // get the total micro secondes position in the audio
                                if (values.ContainsKey("out_time_ms") && long.TryParse(values["out_time_ms"], out long outTimeMs))
                                    encoder.TimeProgress = ((double)outTimeMs) / 1000000.0d;
                                // get current frame position in the audio
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

        public AudioEncoder ScheduleEncode(Audio audio)
        {
            AudioEncoder encoder = null;
            lock (instanceLock)
            {
                lock (instanceLock)
                {
                    if (!runningEncoders.ContainsKey(audio.node.id))
                    {
                        encoder = new AudioEncoder(this, audio);
                        LongTask task = new LongTask(() => {
                            encoder.Run();
                            lock (instanceLock)
                            {
                                runningEncoders.Remove(encoder.Audio.node.id);
                                runningEncodersById.Remove(encoder.Id);
                            }
                        }, audio.node.owner, $"Build MP3 for '{audio.node.name}' (id: {audio.node.id})");
                        scheduler.Start(task);
                        runningEncoders[audio.node.id] = encoder;
                        runningEncodersById[encoder.Id] = encoder;
                    }
                    else
                        encoder = runningEncoders[audio.node.id];
                }
            }
            return encoder;
        }
    }


    public class Audio : Document
    {
        readonly object instanceLock = new object();
        readonly Dictionary<long, AudioEncoder> runningEncoders = new Dictionary<long, AudioEncoder>();
        readonly Dictionary<string, AudioEncoder> runningEncodersById = new Dictionary<string, AudioEncoder>();

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

        public async Task<(Stream, AudioEncoder)> GetWebAudioStreamOrEncoderAsync()
        {
            await node.blob.LoadExpandFieldAsync(context.db, "children");

            var audioBlob = node.blob.children.Find(child => child.name == "webaudio");
            if (audioBlob != null)
                return (context.blobs.GetBlobStream(audioBlob.id), null);

            return (null, context.docs.audioEncoder.ScheduleEncode(this));
        }

        public async Task<Stream> GetWebAudioStreamAsync()
        {
            var (stream, encoder) = await GetWebAudioStreamOrEncoderAsync();
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