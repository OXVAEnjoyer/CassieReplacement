namespace CassieReplacement.Patches
{
    using HarmonyLib;
    using PlayerRoles;
    using Respawning.NamingRules;
    using Respawning.Waves;

    [HarmonyPatch(typeof(Cassie.CassieAnnouncementDispatcher), nameof(Cassie.CassieAnnouncementDispatcher.PlayNewAnnouncement))]
    public static class WaveAnnouncementSendPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Cassie.CassieAnnouncement annc)
        {
            if (annc is not Cassie.CassieWaveAnnouncement wave
                || Plugin.Singleton?.Config?.CassieOverrideConfig.ShouldOverrideAnnouncements != true)
            {
                return true;
            }

            string unitLetter = string.Empty;
            int unitNumber = 0;
            if (NamingRulesManager.TryGetNamingRule(Team.FoundationForces, out UnitNamingRule rule))
            {
                CassieUnit.TryParse(rule.LastGeneratedName, out unitLetter, out unitNumber);
            }

            bool isMini = wave.Wave is NtfMiniWave || wave.Wave is ChaosMiniWave;
            Faction faction = wave.Wave is ChaosSpawnWave || wave.Wave is ChaosMiniWave
                ? Faction.FoundationEnemy
                : Faction.FoundationStaff;

            CassieEventHandlers.HandleAnnouncingWaveEntrance(faction, isMini, unitLetter, unitNumber);
            return false;
        }
    }
}
