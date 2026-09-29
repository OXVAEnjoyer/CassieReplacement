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
    /// Registry of known clips. Readers always see an immutable snapshot that is replaced atomically,
    /// so folders can be registered on a background thread while the game thread keeps looking words up.
    /// </summary>
    public sealed class ClipDatabase
    {
        private readonly object writeLock = new object();

        private volatile Dictionary<string, CassieClip> clips = new Dictionary<string, CassieClip>(StringComparer.Ordinal);

        private int version;

        /// <summary>Incremented on every change; used to invalidate caches built from the registered clips.</summary>
        public int Version => Volatile.Read(ref version);

        public int Count => clips.Count;

        public bool TryGetClip(string name, out CassieClip clip) => clips.TryGetValue(name, out clip);

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
                    Logger.Error($"[CassieReplacement] Registering '{configuration.Path}' failed: {ex}");
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
            foreach (DirectoryInfo child in directory.EnumerateDirectories())
            {
                Scan(child, configuration, output);
            }

            foreach (FileInfo file in directory.EnumerateFiles("*.ogg"))
            {
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
