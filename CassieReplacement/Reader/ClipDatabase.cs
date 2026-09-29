namespace CassieReplacement.Reader
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using CassieReplacement.Reader.Models;
    using LabApi.Features.Console;

    /// <summary>
    /// FIX (thread safety): baza działa na niemutowalnym snapshocie (copy-on-write).
    /// Rejestrację można bezpiecznie robić w Task.Run, a główny wątek czyta zawsze spójny słownik.
    /// Poprzednio List&lt;CassieClip&gt; był modyfikowany z puli wątków podczas enumeracji na main threadzie.
    /// </summary>
    public sealed class ClipDatabase
    {
        private readonly object writeLock = new object();

        private volatile Dictionary<string, CassieClip> clips = new Dictionary<string, CassieClip>(StringComparer.Ordinal);

        private int version;

        /// <summary>Zwiększane przy każdej zmianie - cache próbek audio unieważnia się po tym numerze.</summary>
        public int Version => Volatile.Read(ref version);

        public int Count => clips.Count;

        public IEnumerable<CassieClip> Clips => clips.Values;

        // OPTYMALIZACJA: O(1) zamiast FirstOrDefault po liście przy każdym słowie.
        public bool TryGetClip(string lowerInvariantName, out CassieClip clip) => clips.TryGetValue(lowerInvariantName, out clip);

        public List<string> GetListableClipNames()
        {
            List<string> names = new List<string>();
            foreach (CassieClip clip in clips.Values)
            {
                if (clip.ShouldList)
                {
                    names.Add(clip.Name);
                }
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        public void RegisterFolder(CassieDirectorySerializable configuration, bool createIfMissing = false)
        {
            DirectoryInfo root = new DirectoryInfo(CassiePaths.Resolve(configuration.Path));
            if (!root.Exists)
            {
                if (!createIfMissing)
                {
                    return;
                }

                root.Create();
            }

            List<CassieClip> found = new List<CassieClip>();
            Scan(root, configuration, found);

            lock (writeLock)
            {
                Dictionary<string, CassieClip> next = new Dictionary<string, CassieClip>(clips, StringComparer.Ordinal);
                foreach (CassieClip clip in found)
                {
                    // Kolizje nazw: dopisujemy '_' (zachowanie zgodne z oryginałem), ale w O(1) na próbę.
                    string name = clip.Name;
                    while (next.ContainsKey(name))
                    {
                        name += "_";
                    }

                    clip.Name = name;
                    next.Add(name, clip);
                }

                clips = next;
                Interlocked.Increment(ref version);
            }
        }

        /// <summary>Rejestracja w tle. Wyjątki są logowane (wcześniej połykane przez nieobserwowany Task).</summary>
        public Task RegisterFolderAsync(CassieDirectorySerializable configuration)
        {
            return Task.Run(() =>
            {
                try
                {
                    RegisterFolder(configuration);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[CassieReplacement] RegisterFolder failed for '{configuration.Path}': {ex}");
                }
            });
        }

        public void UnregisterClips()
        {
            lock (writeLock)
            {
                clips = new Dictionary<string, CassieClip>(StringComparer.Ordinal);
                Interlocked.Increment(ref version);
            }
        }

        private static void Scan(DirectoryInfo directory, CassieDirectorySerializable configuration, List<CassieClip> output)
        {
            // Kolejność (podfoldery przed plikami) zachowana - wpływa na rozstrzyganie duplikatów nazw.
            foreach (DirectoryInfo child in directory.EnumerateDirectories())
            {
                Scan(child, configuration, output);
            }

            foreach (FileInfo file in directory.EnumerateFiles("*.ogg"))
            {
                // FIX: jeden uszkodzony plik nie przerywa już rejestracji całego folderu (i nie wywala Enable()).
                try
                {
                    output.Add(new CassieClip(file, configuration.BleedTime, configuration.Prefix, configuration.ShouldList));
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[CassieReplacement] Skipping unreadable clip '{file.FullName}': {ex.Message}");
                }
            }
        }
    }
}
