namespace CassieReplacement.Reader
{
    using CassieReplacement;
    using CassieReplacement.Audio;
    using CassieReplacement.Config;
    using CassieReplacement.Reader.Models;
    using MEC;
    using NVorbis;
    using SecretLabNAudio.Core;
    using SecretLabNAudio.Core.Extensions;
    using SecretLabNAudio.Core.Processors;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using UnityEngine;
    using NorthwoodLib.Pools;
    using Utils.NonAllocLINQ;
        using static NineTailedFoxAnnouncer;

    public class CustomCassieReader
    {
        private static int ticksSinceCassieSpoke = 0;

        private string currentPrefix = string.Empty;

        private string currentSuffix = string.Empty;

        public static CustomCassieReader Singleton { get; internal set; } = new CustomCassieReader();

        private Dictionary<string, CassieClip> PitchShiftedTempClips { get; set; } = new Dictionary<string, CassieClip>();

        private readonly Dictionary<string, float[]> PitchSampleCache = new Dictionary<string, float[]>();

        private readonly Dictionary<string, SampleCacheEntry> SampleCache = new Dictionary<string, SampleCacheEntry>(StringComparer.OrdinalIgnoreCase);

        private sealed class SampleCacheEntry
        {
            public float[] Samples;
            public int SampleRate;
            public int Channels;
        }

        public ClipDatabase ClipDatabase { get; set; } = new ();

        private CassieClip GetClip(string name)
        {
            name = name.ToLower();
            CassieClip clip = ClipDatabase.GetClip(name);
            if (clip is not null)
            {
                return clip;
            }
            else
            {
                PitchShiftedTempClips.TryGetValue(name, out clip);
                return clip;
            }

        }

        private float GetClipLength(string clipName)
        {
            CassieClip clip = GetClip(clipName);

            string[] splits = clipName.Split('-');
            if (clip is not null)
            {
                return clip.Length;
            }
            else if (splits.Length > 1 && float.TryParse(splits[1], out float reverb))
            {
                return GetClipLength(splits[0]) - reverb;
            }

            return 0f;
        }

        private Config Config => Plugin.Singleton.Config;

        internal Dictionary<CoroutineHandle, List<string>> HandlesToMessages { get; set; } = new Dictionary<CoroutineHandle, List<string>>();

        private bool IsBeingUsed(string name)
        {
            foreach (var kvp in HandlesToMessages)
            {
                if (kvp.Key.IsRunning && kvp.Value.Contains(name))
                {
                    return true;
                }

                if (!kvp.Key.IsRunning)
                {
                    HandlesToMessages.Remove(kvp.Key);
                }
            }

            return false;
        }

        public List<AudioPlayer> AudioPlayers { get; set; } = new List<AudioPlayer>();

        public void StopAllPlayback()
        {
            if (AudioPlayers == null)
            {
                return;
            }

            foreach (AudioPlayer audioPlayer in AudioPlayers)
            {
                if (audioPlayer == null)
                {
                    continue;
                }

                AudioQueue queue = audioPlayer.Queue;
                queue?.Clear();
                audioPlayer.WithoutProvider();
            }
        }

        internal DateTime TimeBeforeWhichToPause { get; set; } = DateTime.MinValue;

        public static IEnumerator<float> CassieCheck()
        {
            while (true)
            {
                if (NineTailedFoxAnnouncer.singleton.queue.Count != 0)
                {
                    ticksSinceCassieSpoke = 0;
                }
                else
                {
                    ticksSinceCassieSpoke++;
                }

                yield return Timing.WaitForOneFrame;
            }
        }

        public void CassieReadMessage(List<string> messages, bool isNoisy = true, bool customAnnouncement = true, string translation = "", bool useCassie = true)
        {
            StartMessage(messages, AudioPlayers, isNoisy, customAnnouncement, translation, useCassie);
        }

        public void CassieReadMessage(string messages, bool isNoisy = true, bool customAnnouncement = true, string translation = "", bool useCassie = true)
        {
            StartMessage(messages.Split(' ').ToList(), AudioPlayers, isNoisy, customAnnouncement, translation, useCassie);
        }

        private static float[] Resample(float[] inputBuffer, int inputSampleRate, int outputSampleRate)
        {
            double sampleRateRatio = (double)outputSampleRate / inputSampleRate;
            int outputBufferLength = (int)(inputBuffer.Length * sampleRateRatio);

            float[] outputBuffer = new float[outputBufferLength];

            for (int i = 0; i < outputBufferLength; i++)
            {
                double position = i / sampleRateRatio;
                int leftIndex = (int)Math.Floor(position);
                int rightIndex = leftIndex + 1;

                double fraction = position - leftIndex;

                if (rightIndex >= inputBuffer.Length)
                {
                    outputBuffer[i] = inputBuffer[leftIndex];
                }
                else
                {
                    outputBuffer[i] = (float)(inputBuffer[leftIndex] * (1 - fraction) + inputBuffer[rightIndex] * fraction);
                }
            }

            return outputBuffer;
        }

        private void StartMessage(List<string> messages, List<AudioPlayer> audioPlayers, bool isNoisy = false, bool customAnnouncement = true, string translation = "", bool useCassie = true)
        {
            StringBuilder baseCassieAnnouncement = StringBuilderPool.Shared.Rent();
            HashSet<CassieClip> clipsToUnregister = new HashSet<CassieClip>();
            Dictionary<string, Task> tasks = new Dictionary<string, Task>();
            float pitch = 1.0f;

            for (int i = 0; i < messages.Count; i++)
            {
                string msg = messages[i].ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(msg))
                {
                    messages.RemoveAt(i);
                    i--;
                    continue;
                }
                if (NineTailedFoxAnnouncer.VoiceLine.IsJam(msg, out _, out _))
                {
                    continue;
                }

                if (NineTailedFoxAnnouncer.VoiceLine.IsPitch(msg, out float pitchValue))
                {
                    pitch = pitchValue;
                    if (useCassie)
                    {
                        baseCassieAnnouncement.Append($" {msg}");
                    }

                    continue;
                }
                if (msg.StartsWith("prefix_", StringComparison.OrdinalIgnoreCase)
                    || msg.StartsWith("suffix_", StringComparison.OrdinalIgnoreCase))
                {
                    if (msg.StartsWith("prefix_", StringComparison.OrdinalIgnoreCase))
                    {
                        currentPrefix = msg.Length > 7 ? msg.Substring(7) : string.Empty;
                    }
                    else
                    {
                        currentSuffix = msg.Length > 7 ? msg.Substring(7) : string.Empty;
                    }

                    messages.RemoveAt(i);
                    i--;
                    continue;
                }

                string oldMsg = msg;
                msg = $"{currentPrefix}{msg}{currentSuffix}";

                CassieClip msgCassieClip = ClipDatabase.RegisteredClips.FirstOrDefault(c => c.Name == msg);

                if (msgCassieClip is not null)
                {
                    if (pitch != 1.0f)
                    {
                        string newName = $"p{pitch}_{msgCassieClip.Name}";
                        if (!PitchShiftedTempClips.TryGetValue(newName, out CassieClip pitched))
                        {
                            msgCassieClip = new CassieClip(newName, msgCassieClip.FileInfo, msgCassieClip.BaseLength / pitch, msgCassieClip.Reverb / pitch);
                            PitchShiftedTempClips.Add(newName, msgCassieClip);
                            msg = msgCassieClip.Name;
                        }
                        else
                        {
                            msgCassieClip = pitched;
                            msg = pitched.Name;
                        }
                    }

                    messages[i] = msg;
                    clipsToUnregister.Add(msgCassieClip);

                    if (pitch != 1.0f)
                    {
                        float workingPitch = pitch;
                        string clipName = msgCassieClip.Name;
                        string path = msgCassieClip.FileInfo.FullName;
                        Task task = Task.Run(() =>
                        {
                            float[] array;
                            using (VorbisReader vorbisReader = new VorbisReader(path))
                            {
                                array = new float[vorbisReader.TotalSamples * vorbisReader.Channels];
                                vorbisReader.ReadSamples(array);
                            }

                            array = Resample(array, 48000, Convert.ToInt32(48000 / workingPitch));
                            lock (PitchSampleCache)
                            {
                                PitchSampleCache[clipName] = array;
                            }
                        });

                        if (!tasks.ContainsKey(msgCassieClip.Name))
                        {
                            tasks.Add(msgCassieClip.Name, task);
                        }
                    }

                    if (useCassie)
                    {
                        if (Config.WordsToBasegameOverride.TryGetValue(msg, out string word))
                        {
                            baseCassieAnnouncement.Append($" {word}");
                        }
                        else if (msg == "<split>")
                        {
                            baseCassieAnnouncement.Append(" <split>");
                        }
                        else
                        {
                            int howManyDotsToAdd = (int)Math.Round(msgCassieClip.Length * 2, MidpointRounding.AwayFromZero);
                            baseCassieAnnouncement.Append(" pitch_1");
                            for (int j = 0; j < howManyDotsToAdd; j++)
                            {
                                baseCassieAnnouncement.Append(" .");
                            }

                            baseCassieAnnouncement.Append($" pitch_{pitch} jam_0_0");
                        }
                    }
                }
                else if (int.TryParse(oldMsg, out int num))
                {
                    string[] numbers = NineTailedFoxAnnouncer.ConvertNumber(num).Split(' ');
                    messages.RemoveAt(i);
                    for (int j = 0; j < numbers.Length; j++)
                    {
                        messages.Insert(i + j, numbers[j]);
                    }

                    i--;
                }
                else
                {
                    messages[i] = msg;

                    if (useCassie)
                    {
                        baseCassieAnnouncement.Append($" {(Config.WordsToBasegameOverride.TryGetValue(msg, out string word) ? word : oldMsg)}");
                    }
                }
            }

            currentPrefix = string.Empty;
            currentSuffix = string.Empty;
            if (useCassie)
            {
                baseCassieAnnouncement.Insert(0, "noparse ");
                string subtitle = string.IsNullOrWhiteSpace(translation) ? string.Join(" ", messages) : translation;
                CassiePlayback.Play(StringBuilderPool.Shared.ToStringReturn(baseCassieAnnouncement), false, isNoisy, customAnnouncement, subtitle);
            }
            else
            {
                StringBuilderPool.Shared.Return(baseCassieAnnouncement);
            }

            float bellLeadIn = (useCassie && isNoisy) ? CassieBellLeadInSeconds : 0f;
            HandlesToMessages.Add(
                Timing.RunCoroutine(ReadWords(messages, audioPlayers, clipsToUnregister, tasks, bellLeadIn)),
                messages);
        }

        private const float CassieBellLeadInSeconds = 2.35f;

        private IEnumerator<float> ReadWords(
            List<string> messages,
            List<AudioPlayer> audioPlayers,
            HashSet<CassieClip> clipsToUnregister = null,
            Dictionary<string, Task> tasksToAwait = null,
            float bellLeadInSeconds = 0f)
        {
            if (messages.Count == 0)
            {
                yield break;
            }

            if (bellLeadInSeconds > 0f)
            {
                yield return Timing.WaitForSeconds(bellLeadInSeconds);
            }

            DateTime timeStarted = DateTime.Now;
            int jamDelay = 0;
            int jamAmount = 0;
            float pitch = 1.0f;

            foreach (string msg in messages)
            {
                if (TimeBeforeWhichToPause >= timeStarted)
                {
                    break;
                }

                if (NineTailedFoxAnnouncer.VoiceLine.IsPitch(msg, out float pitchValue))
                {
                    pitch = pitchValue;
                    continue;
                }

                if (NineTailedFoxAnnouncer.VoiceLine.IsYield(msg, out float yield))
                {
                    yield return Timing.WaitForSeconds(yield);
                    continue;
                }

                if (NineTailedFoxAnnouncer.VoiceLine.IsJam(msg, out int newDelay, out int newAmount))
                {
                    jamDelay = newDelay;
                    jamAmount = newAmount;
                    continue;
                }

                int workingJamDelay = jamDelay;
                int workingJamAmount = jamAmount;
                jamDelay = 0;
                jamAmount = 0;

                tasksToAwait.TryGetValue(msg, out Task currentWordTask);
                while (currentWordTask is not null && !currentWordTask.IsCompleted)
                {
                    yield return Timing.WaitForOneFrame;
                }

                CassieClip clip = GetClip(msg);
                if (clip is null || clip.FileInfo is null || !clip.FileInfo.Exists)
                {
                    string jams = string.Empty;
                    if (workingJamDelay != 0 || workingJamAmount != 0)
                    {
                        jams = $"jam_{workingJamDelay}_{workingJamAmount} ";
                    }

                    yield return Timing.WaitForSeconds(NineTailedFoxAnnouncer.singleton.CalculateDuration($"{jams}{msg}", speed: pitch));
                    continue;
                }

                float volume = Config.CassieVolume;
                PlayWord(audioPlayers, clip, msg, volume, pitch);

                if (workingJamDelay > 0 && workingJamDelay < 100)
                {
                    yield return Timing.WaitForSeconds(GetClipLength(msg) * workingJamDelay * 0.01f);
                    StopPlayers(audioPlayers);
                    for (int i = 0; i < workingJamAmount; i++)
                    {
                        PlayWord(audioPlayers, clip, msg, volume, pitch);
                        yield return Timing.WaitForSeconds(0.13f);
                        StopPlayers(audioPlayers);
                    }
                }
                else
                {
                    yield return Timing.WaitForSeconds(GetClipLength(msg));
                    StopPlayers(audioPlayers);
                }
            }

            if (GetClip(messages.Last()) is not null)
            {
                yield return Timing.WaitForSeconds(GetClip(messages.Last()).Reverb);
            }

            if (clipsToUnregister is not null)
            {
                foreach (var clip in clipsToUnregister)
                {
                    if (!IsBeingUsed(clip.Name))
                    {
                        PitchShiftedTempClips.Remove(clip.Name);
                        lock (PitchSampleCache)
                        {
                            PitchSampleCache.Remove(clip.Name);
                        }

                        lock (SampleCache)
                        {
                            List<string> doomed = new List<string>();
                            foreach (string key in SampleCache.Keys)
                            {
                                if (key.Equals(clip.Name, StringComparison.OrdinalIgnoreCase)
                                    || key.EndsWith("_" + clip.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    doomed.Add(key);
                                }
                            }

                            foreach (string key in doomed)
                            {
                                SampleCache.Remove(key);
                            }
                        }
                    }
                }
            }
        }

        private void PlayWord(List<AudioPlayer> audioPlayers, CassieClip clip, string clipKey, float volume, float pitch)
        {
            if (audioPlayers == null || clip?.FileInfo == null || !clip.FileInfo.Exists)
            {
                return;
            }
            if (!TryGetSamples(clip, clipKey, pitch, out SampleCacheEntry entry))
            {
                return;
            }

            foreach (AudioPlayer audioPlayer in audioPlayers)
            {
                if (audioPlayer == null)
                {
                    continue;
                }

                float playerVolume = volume;
                if (audioPlayer == Plugin.CassiePlayerGlobal)
                {
                    playerVolume *= Config.GlobalSpeakerVolumeMultiplier;
                }

                audioPlayer.WithUnmanagedProvider(new FloatArraySampleProvider(entry.Samples, entry.SampleRate, entry.Channels))
                    .WithMasterAmplification(playerVolume);
            }
        }

        private bool TryGetSamples(CassieClip clip, string cacheKey, float pitch, out SampleCacheEntry entry)
        {
            string key = pitch == 1f ? cacheKey : $"p{pitch}_{cacheKey}";
            lock (SampleCache)
            {
                if (SampleCache.TryGetValue(key, out entry))
                {
                    return entry?.Samples != null && entry.Samples.Length > 0;
                }
            }

            try
            {
                float[] array;
                int sampleRate;
                int channels;
                using (VorbisReader vorbisReader = new VorbisReader(clip.FileInfo.FullName))
                {
                    sampleRate = vorbisReader.SampleRate > 0 ? vorbisReader.SampleRate : 48000;
                    channels = vorbisReader.Channels > 0 ? vorbisReader.Channels : 1;
                    array = new float[vorbisReader.TotalSamples * channels];
                    vorbisReader.ReadSamples(array);
                }

                if (pitch != 1f && pitch > 0f)
                {
                    array = Resample(array, sampleRate, Math.Max(1, Convert.ToInt32(sampleRate / pitch)));
                }

                entry = new SampleCacheEntry
                {
                    Samples = array,
                    SampleRate = sampleRate,
                    Channels = channels,
                };

                lock (SampleCache)
                {
                    SampleCache[key] = entry;
                }
                if (pitch != 1f)
                {
                    lock (PitchSampleCache)
                    {
                        PitchSampleCache[cacheKey] = array;
                    }
                }

                return array.Length > 0;
            }
            catch
            {
                entry = null;
                return false;
            }
        }

        private static void StopPlayers(List<AudioPlayer> audioPlayers)
        {
            if (audioPlayers == null)
            {
                return;
            }

            foreach (AudioPlayer audioPlayer in audioPlayers)
            {
                audioPlayer?.WithoutProvider();
            }
        }
    }
}
