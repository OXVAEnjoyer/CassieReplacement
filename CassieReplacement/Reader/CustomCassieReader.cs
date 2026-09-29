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
    using NVorbis;
    using SecretLabNAudio.Core;
    using SecretLabNAudio.Core.Extensions;

    /// <summary>
    /// Odtwarza własne klipy CASSIE. Komunikaty są kolejkowane i odtwarzane po kolei przez JEDNĄ coroutine.
    ///
    /// REFACTOR / FIX:
    ///  - jedna coroutine zamiast jednej na komunikat: koniec z nakładaniem się komunikatów, które nawzajem ucinały sobie audio
    ///    (StopPlayers jednego zatrzymywał słowo drugiego),
    ///  - koniec z HandlesToMessages/IsBeingUsed (modyfikacja słownika w foreach = InvalidOperationException, wyciek wpisów),
    ///  - anulowanie = KillCoroutines + Clear (wcześniej DateTime.Now, które NIE anulowało komunikatu będącego w 2.35 s 'bell lead-in'),
    ///  - koniec z statycznym ticksSinceCassieSpoke i coroutine CassieCheck (nigdy nie czytane, działała co klatkę do końca świata).
    /// </summary>
    public sealed class CustomCassieReader : IDisposable
    {
        private const string CoroutineTag = "CassieReplacement.Reader";
        private const float BellLeadInSeconds = 2.35f;
        private const float JamRepeatSeconds = 0.13f;
        private const int JamMaxPercent = 100;
        private const float PercentToFraction = 0.01f;
        private const int DefaultSampleRate = 48000;
        private const int MaxCachedClips = 1024;

        private readonly Plugin plugin;
        private readonly SpeakerManager speakers;
        private readonly Queue<CassieMessage> queue = new Queue<CassieMessage>();

        // OPTYMALIZACJA: jeden cache zdekodowanych próbek (klucz: klip + pitch). Zastępuje SampleCache, PitchSampleCache
        // (zapisywany, nigdy nie czytany) i PitchShiftedTempClips. Dekodowanie robione w tle, przed startem komunikatu.
        private readonly ConcurrentDictionary<SampleKey, SampleData> sampleCache = new ConcurrentDictionary<SampleKey, SampleData>();

        private CoroutineHandle runner;
        private int cachedDatabaseVersion = -1;

        private CustomCassieReader(Plugin plugin, SpeakerManager speakers)
        {
            this.plugin = plugin;
            this.speakers = speakers;
        }

        // FIX: brak statycznego inicjalizatora tworzącego instancję przed startem pluginu.
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

        /// <summary>Przerywa bieżący komunikat i czyści kolejkę.</summary>
        public void CancelAll()
        {
            Timing.KillCoroutines(runner);
            queue.Clear();
            speakers.StopAll();
        }

        public void Dispose()
        {
            CancelAll();
            sampleCache.Clear();
            ClipDatabase.UnregisterClips();

            if (ReferenceEquals(Singleton, this))
            {
                Singleton = null;
            }
        }

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

            // Dekodowanie ogg trwa w tle - main thread nie robi już I/O w PlayWord.
            while (!message.Prepared.IsCompleted)
            {
                yield return Timing.WaitForOneFrame;
            }

            foreach (CassieStep step in message.Steps)
            {
                if (step.Kind == StepKind.Yield)
                {
                    yield return Timing.WaitForSeconds(step.Seconds);
                    continue;
                }

                if (step.Clip == null)
                {
                    yield return Timing.WaitForSeconds(CassieTokens.EstimateWordSeconds(step.Name, step.Pitch));
                    continue;
                }

                float length = step.Clip.Length / step.Pitch;
                PlayWord(step);

                if (step.JamDelay > 0 && step.JamDelay < JamMaxPercent)
                {
                    yield return Timing.WaitForSeconds(length * step.JamDelay * PercentToFraction);
                    speakers.StopAll();

                    for (int i = 0; i < step.JamAmount; i++)
                    {
                        PlayWord(step);
                        yield return Timing.WaitForSeconds(JamRepeatSeconds);
                        speakers.StopAll();
                    }
                }
                else
                {
                    yield return Timing.WaitForSeconds(length);
                    speakers.StopAll();
                }
            }

            CassieStep last = message.Steps[message.Steps.Count - 1];
            if (last.Kind == StepKind.Word && last.Clip != null && last.Clip.Reverb > 0f)
            {
                yield return Timing.WaitForSeconds(last.Clip.Reverb / last.Pitch);
            }
        }

        private void PlayWord(in CassieStep step)
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

                audioPlayer.WithUnmanagedProvider(new FloatArraySampleProvider(data.Samples, data.SampleRate, data.Channels))
                    .WithMasterAmplification(playerVolume);
            }
        }

        private Task PrepareSamples(CassieMessage message)
        {
            int databaseVersion = ClipDatabase.Version;
            if (databaseVersion != cachedDatabaseVersion)
            {
                sampleCache.Clear();
                cachedDatabaseVersion = databaseVersion;
            }

            List<Task> tasks = null;
            HashSet<SampleKey> scheduled = null;

            foreach (CassieStep step in message.Steps)
            {
                if (step.Clip == null)
                {
                    continue;
                }

                SampleKey key = new SampleKey(step.Clip.Name, step.Pitch);
                if (sampleCache.ContainsKey(key))
                {
                    continue;
                }

                scheduled ??= new HashSet<SampleKey>();
                if (!scheduled.Add(key))
                {
                    continue;
                }

                // THREAD SAFETY: w tle działa wyłącznie czysto zarządzane dekodowanie (NVorbis) bez API Unity/SCP:SL.
                CassieClip clip = step.Clip;
                float pitch = step.Pitch;
                tasks ??= new List<Task>();
                tasks.Add(Task.Run(() => GetOrDecode(clip, pitch)));
            }

            return tasks == null ? Task.CompletedTask : Task.WhenAll(tasks);
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
                // Bez logowania z wątku tła - błąd trafia do SampleData i jest logowany na main threadzie w PlayWord.
                data = SampleData.Failure(ex.Message);
            }

            if (sampleCache.Count >= MaxCachedClips)
            {
                sampleCache.Clear();
            }

            sampleCache[key] = data;
            return data;
        }

        private static SampleData Decode(CassieClip clip, float pitch)
        {
            using VorbisReader reader = new VorbisReader(clip.FileInfo.FullName);

            int sampleRate = reader.SampleRate > 0 ? reader.SampleRate : DefaultSampleRate;
            int channels = reader.Channels > 0 ? reader.Channels : 1;
            long total = reader.TotalSamples * channels;
            if (total <= 0 || total > int.MaxValue)
            {
                return SampleData.Failure($"unsupported sample count ({total})");
            }

            float[] samples = new float[total];
            int filled = 0;
            while (filled < samples.Length)
            {
                // FIX: ReadSamples może zwrócić mniej niż zażądano - poprzednio jedno wywołanie zostawiało ciszę na końcu.
                int read = reader.ReadSamples(samples, filled, samples.Length - filled);
                if (read <= 0)
                {
                    break;
                }

                filled += read;
            }

            if (filled == 0)
            {
                return SampleData.Failure("no samples decoded");
            }

            if (filled < samples.Length)
            {
                Array.Resize(ref samples, filled);
            }

            if (pitch != 1f)
            {
                samples = ChangeSpeed(samples, channels, pitch);
            }

            return new SampleData(samples, sampleRate, channels);
        }

        /// <summary>
        /// Zmiana tempa/wysokości przez interpolację liniową.
        /// FIX: interpolacja per KLATKA (kanał po kanale). Poprzednio dla stereo mieszała próbki L i R.
        /// Również: rate 48000 nie jest już wpisane na sztywno.
        /// </summary>
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

            public string Error { get; private set; }

            public static SampleData Failure(string error) => new SampleData(null, 0, 0) { Error = error };
        }
    }
}
