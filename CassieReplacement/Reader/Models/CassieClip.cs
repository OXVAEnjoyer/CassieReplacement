namespace CassieReplacement.Reader.Models
{
    using System;
    using System.IO;
    using NAudio.Wave;
    using SecretLabNAudio.Core.FileReading;

    public sealed class CassieClip
    {
        public CassieClip(FileInfo file, float reverb = 0f, string prefix = "", bool shouldList = false)
        {
            FileInfo = file ?? throw new ArgumentNullException(nameof(file));
            Reverb = reverb;
            ShouldList = shouldList;
            Name = (prefix ?? string.Empty).ToLowerInvariant()
                   + Path.GetFileNameWithoutExtension(file.Name).ToLowerInvariant().Replace(' ', '_');

            using (WaveStream stream = CreateAudioReader.Stream(file.FullName))
            {
                BaseLength = (float)stream.TotalTime.TotalSeconds;
            }
        }

        public string Name { get; internal set; }

        public FileInfo FileInfo { get; }

        public float BaseLength { get; }

        public float Reverb { get; }

        public bool ShouldList { get; }

        public float Length => Math.Max(0f, BaseLength - Reverb);
    }
}
