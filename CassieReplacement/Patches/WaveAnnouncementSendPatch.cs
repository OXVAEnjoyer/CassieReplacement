#if !EXILED
namespace CassieReplacement.Patches
{
    using HarmonyLib;
    using PlayerRoles;
    using Respawning.NamingRules;
    using Respawning.Waves;

    [HarmonyPatch(typeof(Cassie.CassieAnnouncementDispatcher), nameof(Cassie.CassieAnnouncementDispatcher.PlayNewAnnouncement))]
    public static class WaveAnnouncementSendPatch
    {
        public static bool Prefix(Cassie.CassieAnnouncement annc)
        {
            Cassie.CassieWaveAnnouncement wave = annc as Cassie.CassieWaveAnnouncement;
            if (wave == null || !Plugin.Singleton.Config.CassieOverrideConfig.ShouldOverrideAnnouncements)
            {
                return true;
            }

            string unitLetter = string.Empty;
            int unitNumber = 0;
            UnitNamingRule rule;
            if (NamingRulesManager.TryGetNamingRule(Team.FoundationForces, out rule) && !string.IsNullOrEmpty(rule.LastGeneratedName) && rule.LastGeneratedName.Contains("-"))
            {
                string[] parts = rule.LastGeneratedName.Split('-');
                unitLetter = parts[0];
                int.TryParse(parts[1], out unitNumber);
            }

            bool mini = wave.Wave is NtfMiniWave || wave.Wave is ChaosMiniWave;
            Faction faction = wave.Wave is ChaosSpawnWave || wave.Wave is ChaosMiniWave
                ? Faction.FoundationEnemy
                : Faction.FoundationStaff;
            CassieEventHandlers.HandleAnnouncingWaveEntrance(faction, mini, unitLetter, unitNumber);
            return false;
        }
    }
}
#endif
