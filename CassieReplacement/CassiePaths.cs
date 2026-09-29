namespace CassieReplacement
{
    using System.IO;
    using LabApi.Loader.Features.Paths;

    public static class CassiePaths
    {
        public const string Placeholder = "{labapi_configs}";

        public const string DefaultFolderName = "CASSIE Replacement";

        public static string DefaultAudioDirectory
        {
            get
            {
                string path = Path.Combine(PathManager.Configs.FullName, DefaultFolderName);
                Directory.CreateDirectory(path);
                return path;
            }
        }

        public static string Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return DefaultAudioDirectory;
            }

            path = path.Trim();
            if (path.IndexOf(Placeholder, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                path = ReplaceIgnoreCase(path, Placeholder, PathManager.Configs.FullName);
            }

            if (path.IndexOf("{exiled_config}", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
#if EXILED
                path = ReplaceIgnoreCase(path, "{exiled_config}", Exiled.API.Features.Paths.Configs);
#else
                path = ReplaceIgnoreCase(path, "{exiled_config}", PathManager.Configs.FullName);
#endif
            }
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(PathManager.Configs.FullName, path);
            }

            Directory.CreateDirectory(path);
            return Path.GetFullPath(path);
        }

        private static string ReplaceIgnoreCase(string input, string oldValue, string newValue)
        {
            int index = input.IndexOf(oldValue, System.StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return input;
            }

            return input.Substring(0, index) + newValue + input.Substring(index + oldValue.Length);
        }
    }
}
