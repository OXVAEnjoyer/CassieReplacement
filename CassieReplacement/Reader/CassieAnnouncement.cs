namespace CassieReplacement.Reader
{
    using CassieReplacement.Config;
    using CassieReplacement.Playback;
    using PlayerRoles;

#pragma warning disable SA1600
    public class CassieAnnouncement
    {
        private string words = string.Empty;

        private string translation = string.Empty;

        public CassieAnnouncement()
        {
        }

        public CassieAnnouncement(string words, string translation = "")
        {
            Words = words;
            Translation = translation;
        }

        public bool IsNoisy { get; set; } = true;

        public string Words
        {
            get => words;
            set => words = (value ?? string.Empty).ToLowerInvariant();
        }

        public string Translation
        {
            get => translation;
            set => translation = value ?? string.Empty;
        }

        private static CassieOverrideConfigs OverrideConfig => Plugin.Singleton.Config.CassieOverrideConfig;

        public static CassieAnnouncement operator +(CassieAnnouncement left, CassieAnnouncement right)
        {
            return new CassieAnnouncement($"{left.Words} {right.Words}", $"{left.Translation} {right.Translation}");
        }

        public CassieAnnouncement Replace(string oldText, CassieAnnouncement newText)
        {
            return new CassieAnnouncement(Words.Replace(oldText, newText.Words), Translation.Replace(oldText, newText.Translation)) { IsNoisy = IsNoisy };
        }

        public CassieAnnouncement Replace(string oldText, string newText)
        {
            return new CassieAnnouncement(Words.Replace(oldText, newText), Translation.Replace(oldText, newText)) { IsNoisy = IsNoisy };
        }

        public CassieAnnouncement GenericReplacement()
        {
            int scps = 0;
            int classD = 0;
            int scientists = 0;
            int foundationForces = 0;
            int chaos = 0;
            int flamingos = 0;

            foreach (ReferenceHub hub in ReferenceHub.AllHubs)
            {
                if (hub.IsSCP(includeZombies: false))
                {
                    scps++;
                }

                switch (hub.GetTeam())
                {
                    case Team.ClassD:
                        classD++;
                        break;
                    case Team.Scientists:
                        scientists++;
                        break;
                    case Team.FoundationForces:
                        foundationForces++;
                        break;
                    case Team.ChaosInsurgency:
                        chaos++;
                        break;
                    case Team.Flamingos:
                        flamingos++;
                        break;
                }
            }

            CassieOverrideConfigs config = OverrideConfig;
            CassieAnnouncement threatOverview = scps == 0
                ? config.ThreatOverviewNoScps
                : scps == 1 ? config.ThreatOverviewOneScp : config.ThreatOverviewScps;

            // {threatoverview} contains {scps}, so it has to be replaced first.
            return Replace("{threatoverview}", threatOverview)
                .Replace("{scps}", scps.ToString())
                .Replace("{classds}", classD.ToString())
                .Replace("{scientists}", scientists.ToString())
                .Replace("{foundationforces}", foundationForces.ToString())
                .Replace("{chaosinsurgencys}", chaos.ToString())
                .Replace("{flamingos}", flamingos.ToString());
        }

        public void Announce(bool? isNoisy = null, bool isSubtitles = true)
        {
            CassieAnnouncement processed = GenericReplacement();
            if (string.IsNullOrWhiteSpace(processed.Words))
            {
                return;
            }

            CassiePlayback.Play(processed.Words, isNoisy ?? IsNoisy, isSubtitles, processed.Translation);
        }
    }
}
