namespace CassieReplacement.Reader
{
    using CassieReplacement.Config;
    using CassieReplacement.Reader.Models;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    public class ClipDatabase
    {
        private List<CassieClip> registeredClips { get; set; } = new List<CassieClip>();

        public List<CassieClip> RegisteredClips => registeredClips;

        public List<string> RegisteredClipNames => RegisteredClips.Select(c => c.Name).ToList();

        public List<string> ListableClipNames => RegisteredClips.Where(c => c.ShouldList).Select(c => c.Name).ToList();

        public CassieClip GetClip(string name)
        {
            name = name.ToLower();
            IEnumerable<CassieClip> clips = registeredClips.Where(c => c.Name == name);
            return clips.FirstOrDefault();
        }

        public float GetClipLength(string clipName)
        {
            CassieClip clip = GetClip(clipName);
            if (clip is not null)
            {
                return clip.Length;
            }

            return 0f;
        }

        public float GetClipBaseLength(string clipName)
        {
            CassieClip clip = GetClip(clipName);
            if (clip is not null)
            {
                return clip.BaseLength;
            }

            return 0f;
        }

        public void RegisterFolder(CassieDirectorySerializable directoryConfiguration, string directory = null)
        {
            string resolvedPath = CassiePaths.Resolve(directoryConfiguration.Path);
            DirectoryInfo d = new DirectoryInfo(resolvedPath);
            if (directory is not null)
            {
                d = new DirectoryInfo(directory);
            }

            if (!d.Exists)
            {
                d.Create();
            }

            foreach (DirectoryInfo directoryInfo in d.GetDirectories())
            {
                RegisterFolder(directoryConfiguration, directoryInfo.FullName);
            }

            foreach (FileInfo file in d.GetFiles("*.ogg"))
            {
                CassieClip cassieClip = new CassieClip(file, directoryConfiguration.BleedTime, directoryConfiguration.Prefix, directoryConfiguration.ShouldList);
                while (RegisteredClipNames.Contains(cassieClip.Name))
                {
                    cassieClip.Name += "_";
                }

                registeredClips.Add(cassieClip);
            }
        }

        public void UnregisterClips()
        {
            registeredClips.Clear();
        }
    }
}
