namespace CassieReplacement.Reader.Models
{
    using System;
    using System.IO;
    using NVorbis;

    public sealed class CassieClip
    {
        public CassieClip(FileInfo file, float reverb = 0f, string prefix = "", bool shouldList = false)
        {
            FileInfo = file ?? throw new ArgumentNullException(nameof(file));
            Reverb = reverb;
            ShouldList = shouldList;

            // FIX: prefiks też normalizowany do małych liter (wyszukiwanie klipów jest lowercase - "Sam_" nigdy nie pasowało).
            Name = (prefix ?? string.Empty).ToLowerInvariant()
                   + Path.GetFileNameWithoutExtension(file.Name).ToLowerInvariant().Replace(' ', '_');

            // FIX: using -> uchwyt pliku zwalniany także przy wyjątku (blokada pliku na Windows).
            using (VorbisReader vorbisReader = new VorbisReader(file.FullName))
            {
                BaseLength = (float)vorbisReader.TotalTime.TotalSeconds;
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
