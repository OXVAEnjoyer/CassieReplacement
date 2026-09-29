namespace CassieReplacement
{
    using System.IO;
    using System.Text.RegularExpressions;
    using LabApi.Loader.Features.Paths;

    public static class CassiePaths
    {
        public const string Placeholder = "{labapi_configs}";

        public const string DefaultFolderName = "CASSIE Replacement";

        // "{exiled_config}" is accepted as an alias for configs written for the EXILED build.
        private static readonly Regex PlaceholderRegex = new Regex(
            @"\{(?:labapi_configs|exiled_config)\}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string ConfigsRoot => PathManager.Configs.FullName;

        public static string DefaultAudioDirectory
        {
            get
            {
                string path = Path.Combine(ConfigsRoot, DefaultFolderName);
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

            string resolved = PlaceholderRegex.Replace(path.Trim(), _ => ConfigsRoot);
            if (!Path.IsPathRooted(resolved))
            {
                resolved = Path.Combine(ConfigsRoot, resolved);
            }

            return Path.GetFullPath(resolved);
        }
    }
}
