namespace CassieReplacement.Reader
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CassieReplacement.Audio;
    using CassieReplacement.Config;
    using CassieReplacement.Playback;
    using CassieReplacement.Reader.Models;
    using LabApi.Features.Console;
    using MEC;
    using NAudio.Wave;
    using SecretLabNAudio.Core;
    using SecretLabNAudio.Core.Extensions;
    using SecretLabNAudio.Core.Extensions.Processors;
    using SecretLabNAudio.Core.FileReading;
    using SecretLabNAudio.Core.Processors;
    using SecretLabNAudio.Core.Providers;

    public sealed class CustomCassieReader : IDisposable
    {
        private const string CoroutineTag = "CassieReplacement.Reader";
        private const float BellLeadInSeconds = 2.35f;
        private const float JamRepeatSeconds = 0.13f;
        private const int JamMaxPercent = 100;
        private const float PercentToFraction = 0.01f;
        private const int ReadChunkSamples = 16384;
        private const long BytesPerMegabyte = 1024L * 1024L;

        private readonly Plugin plugin;
        private readonly SpeakerManager speakers;
        private readonly Queue<CassieMessage> queue = new Queue<CassieMessage>();
        private readonly ConcurrentDictionary<SampleKey, SampleData> sampleCache = new ConcurrentDictionary<SampleKey, SampleData>();

        private readonly object cacheLock = new object();

        private CoroutineHandle runner;
        private long cachedBytes;
        private int cachedDatabaseVersion = -1;

        private CustomCassieReader(Plugin plugin, SpeakerManager speakers)
        {
            this.plugin = plugin;
            this.speakers = speakers;
        }

        public static CustomCassieReader Singleton { get; private set; }

        public ClipDatabase ClipDatabase { get; } = new ClipDatabase();

        private CassieConfig Config => plugin.Config;

        internal static void Create(Plugin plugin, SpeakerManager speakers)
        {
            Singleton?.Dispose();
            Singleton = new CustomCassieReader(plugin, speakers);
        }

        public void CassieReadMessage(string words, bool isNoisy = true, bool customAnnouncement = true, string translation = "", bool useCassie = true)
        {
            CassieReadMessage(words.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), isNoisy, customAnnouncement, translation, useCassie);
        }

        public void CassieReadMessage(IReadOnlyList<string> words, bool isNoisy = true, bool customAnnouncement = true, string translation = "", bool useCassie = true)
        {
            speakers.EnsureReady();

            CassieMessage message = CassieMessageParser.Parse(words, ClipDatabase, Config, useCassie, translation);
            if (message.BaseAnnouncement != null)
            {
                CassiePlayback.Play(message.BaseAnnouncement, isNoisy, customAnnouncement, message.Subtitle);
            }

            if (message.Steps.Count == 0)
            {
                return;
            }

            message.LeadInSeconds = useCassie && isNoisy ? BellLeadInSeconds : 0f;
            message.Prepared = PrepareSamples(message);
            queue.Enqueue(message);

            if (!runner.IsRunning)
            {
                runner = Timing.RunCoroutine(RunQueue(), CoroutineTag);
            }
        }

        public float MeasureDuration(IReadOnlyList<string> words)
        {
            CassieMessage message = CassieMessageParser.Parse(words, ClipDatabase, Config, false, string.Empty);
            if (message.Steps.Count == 0)
            {
                return 0f;
            }

            float seconds = 0f;

            foreach (CassieStep step in message.Steps)
            {
                seconds += GetStepSeconds(step);
            }

            return seconds + GetReverbSeconds(message);
        }

        public void CancelAll()
        {
            Timing.KillCoroutines(runner);
            queue.Clear();
            speakers.StopAll();
        }

        public void Dispose()
        {
            CancelAll();
            ClearCache();
            ClipDatabase.UnregisterClips();

            if (ReferenceEquals(Singleton, this))
            {
                Singleton = null;
            }
        }

        private static float GetStepSeconds(CassieStep step)
        {
            if (step.Kind == StepKind.Yield)
            {
                return step.Seconds;
            }

            if (step.Clip == null)
            {
                return CassieTokens.EstimateWordSeconds(step.Name, step.Pitch);
            }

            float length = step.Clip.Length / step.Pitch;
            return IsJammed(step)
                ? (length * step.JamDelay * PercentToFraction) + (step.JamAmount * JamRepeatSeconds)
                : length;
        }

        private static float GetReverbSeconds(CassieMessage message)
        {
            CassieStep last = message.Steps[message.Steps.Count - 1];
            return last.Kind == StepKind.Word && last.Clip != null ? last.Clip.Reverb / last.Pitch : 0f;
        }

        private static bool IsJammed(CassieStep step) => step.JamDelay > 0 && step.JamDelay < JamMaxPercent;

        private IEnumerator<float> RunQueue()
        {
            while (queue.Count > 0)
            {
                IEnumerator<float> playback = PlayMessage(queue.Dequeue());
                while (playback.MoveNext())
                {
                    yield return playback.Current;
                }
            }
        }

        private IEnumerator<float> PlayMessage(CassieMessage message)
        {
            if (message.LeadInSeconds > 0f)
            {
                yield return Timing.WaitForSeconds(message.LeadInSeconds);
            }

            while (!message.Prepared.IsCompleted)
            {
                yield return Timing.WaitForOneFrame;
            }

            foreach (CassieStep step in message.Steps)
            {
                if (step.Kind == StepKind.Word && step.Clip != null)
                {
                    PlayWord(step);
                    yield return Timing.WaitForSeconds(step.Clip.Length / step.Pitch * (IsJammed(step) ? step.JamDelay * PercentToFraction : 1f));
                    speakers.StopAll();

                    if (IsJammed(step))
                    {
                        for (int i = 0; i < step.JamAmount; i++)
                        {
                            PlayWord(step);
                            yield return Timing.WaitForSeconds(JamRepeatSeconds);
                            speakers.StopAll();
                        }
                    }
                }
                else
                {
                    yield return Timing.WaitForSeconds(GetStepSeconds(step));
                }
            }

            yield return Timing.WaitForSeconds(GetReverbSeconds(message));
        }

        private void PlayWord(CassieStep step)
        {
            SampleData data = GetOrDecode(step.Clip, step.Pitch);
            if (data.Samples == null)
            {
                Logger.Warn($"[CassieReplacement] Cannot play '{step.Name}': {data.Error}");
                return;
            }

            float volume = Config.CassieVolume;
            IReadOnlyList<AudioPlayer> players = speakers.Players;

            for (int i = 0; i < players.Count; i++)
            {
                AudioPlayer audioPlayer = players[i];
                if (audioPlayer == null)
                {
                    continue;
                }

                float playerVolume = ReferenceEquals(audioPlayer, speakers.GlobalPlayer)
                    ? volume * Config.GlobalSpeakerVolumeMultiplier
                    : volume;

                audioPlayer.WithUnmanagedProvider(new RawSourceSampleProvider(data.Samples, data.SampleRate, data.Channels))
                    .WithMasterAmplification(playerVolume);
            }
        }

        private Task PrepareSamples(CassieMessage message)
        {
            int databaseVersion = ClipDatabase.Version;
            if (databaseVersion != cachedDatabaseVersion)
            {
                ClearCache();
                cachedDatabaseVersion = databaseVersion;
            }

            List<Task> tasks = new List<Task>();
            HashSet<SampleKey> scheduled = new HashSet<SampleKey>();

            foreach (CassieStep step in message.Steps)
            {
                if (step.Clip == null)
                {
                    continue;
                }

                SampleKey key = new SampleKey(step.Clip.Name, step.Pitch);
                if (sampleCache.ContainsKey(key) || !scheduled.Add(key))
                {
                    continue;
                }

                CassieClip clip = step.Clip;
                float pitch = step.Pitch;
                tasks.Add(Task.Run(() => GetOrDecode(clip, pitch)));
            }

            return Task.WhenAll(tasks);
        }

        private SampleData GetOrDecode(CassieClip clip, float pitch)
        {
            SampleKey key = new SampleKey(clip.Name, pitch);
            if (sampleCache.TryGetValue(key, out SampleData cached))
            {
                return cached;
            }

            SampleData data;
            try
            {
                data = Decode(clip, pitch);
            }
            catch (Exception ex)
            {
                data = SampleData.Failure(ex.Message);
            }

            Store(key, data);
            return data;
        }

        private void Store(SampleKey key, SampleData data)
        {
            long budget = Math.Max(1, Config.MaxCacheMegabytes) * BytesPerMegabyte;

            lock (cacheLock)
            {
                if (data.SizeBytes > budget)
                {
                    return;
                }

                if (cachedBytes + data.SizeBytes > budget)
                {
                    ClearCache();
                }

                if (sampleCache.TryAdd(key, data))
                {
                    cachedBytes += data.SizeBytes;
                }
            }
        }

        private void ClearCache()
        {
            lock (cacheLock)
            {
                sampleCache.Clear();
                cachedBytes = 0;
            }
        }

        private static SampleData Decode(CassieClip clip, float pitch)
        {
            if (!TryCreateAudioReader.StreamAndProvider(clip.FileInfo.FullName, out WaveStream stream, out ISampleProvider provider))
            {
                return SampleData.Failure("unsupported audio format");
            }

            using (stream)
            using (ProcessorChain chain = new ProcessorChain(provider, false).ToPlayerCompatible())
            {
                WaveFormat format = chain.WaveFormat;
                int expectedSamples = (int)(stream.TotalTime.TotalSeconds * format.SampleRate * format.Channels);
                float[] samples = ReadToEnd(chain, expectedSamples);

                if (samples.Length == 0)
                {
                    return SampleData.Failure("no samples decoded");
                }

                if (pitch != 1f)
                {
                    samples = ChangeSpeed(samples, format.Channels, pitch);
                }

                return new SampleData(samples, format.SampleRate, format.Channels);
            }
        }

        private static float[] ReadToEnd(ISampleProvider source, int expectedSamples)
        {
            float[] buffer = new float[Math.Max(expectedSamples, ReadChunkSamples)];
            int filled = 0;

            while (true)
            {
                if (filled == buffer.Length)
                {
                    Array.Resize(ref buffer, buffer.Length * 2);
                }

                int read = source.Read(buffer, filled, buffer.Length - filled);
                if (read <= 0)
                {
                    break;
                }

                filled += read;
            }

            Array.Resize(ref buffer, filled);
            return buffer;
        }

        private static float[] ChangeSpeed(float[] input, int channels, float pitch)
        {
            int inputFrames = input.Length / channels;
            if (inputFrames == 0)
            {
                return input;
            }

            int outputFrames = Math.Max(1, (int)(inputFrames / pitch));
            float[] output = new float[outputFrames * channels];

            for (int frame = 0; frame < outputFrames; frame++)
            {
                double position = frame * (double)pitch;
                int left = Math.Min((int)position, inputFrames - 1);
                int right = Math.Min(left + 1, inputFrames - 1);
                float fraction = (float)(position - left);

                for (int channel = 0; channel < channels; channel++)
                {
                    float a = input[(left * channels) + channel];
                    float b = input[(right * channels) + channel];
                    output[(frame * channels) + channel] = a + ((b - a) * fraction);
                }
            }

            return output;
        }

        private readonly struct SampleKey : IEquatable<SampleKey>
        {
            private readonly string name;
            private readonly float pitch;

            public SampleKey(string name, float pitch)
            {
                this.name = name;
                this.pitch = pitch;
            }

            public bool Equals(SampleKey other) => pitch.Equals(other.pitch) && string.Equals(name, other.name, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is SampleKey other && Equals(other);

            public override int GetHashCode() => (StringComparer.Ordinal.GetHashCode(name) * 397) ^ pitch.GetHashCode();
        }

        private sealed class SampleData
        {
            public SampleData(float[] samples, int sampleRate, int channels)
            {
                Samples = samples;
                SampleRate = sampleRate;
                Channels = channels;
            }

            public float[] Samples { get; }

            public int SampleRate { get; }

            public int Channels { get; }

            public long SizeBytes => Samples == null ? 0 : (long)Samples.Length * sizeof(float);

            public string Error { get; private set; }

            public static SampleData Failure(string error) => new SampleData(null, 0, 0) { Error = error };
        }
    }
}
