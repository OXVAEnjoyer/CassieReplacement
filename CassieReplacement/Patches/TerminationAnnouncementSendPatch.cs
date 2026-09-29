namespace CassieReplacement.Patches
{
    using CassieReplacement.Playback;
    using HarmonyLib;
    using PlayerRoles;
    using PlayerStatsSystem;

    [HarmonyPatch(typeof(Cassie.CassieScpTerminationAnnouncement), nameof(Cassie.CassieScpTerminationAnnouncement.AnnounceScpTermination))]
    public static class TerminationAnnouncementSendPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ReferenceHub scp, DamageHandlerBase hit)
        {
            // FIX: Singleton == null (plugin wyłączony w trakcie gry) nie może rzucać NRE w środku obsługi śmierci.
            if (Plugin.Singleton?.Config?.CassieOverrideConfig.ShouldOverrideAnnouncements != true)
            {
                return true;
            }

            PlayerRoleBase role = scp != null && scp.roleManager != null ? scp.roleManager.CurrentRole : null;
            if (role == null || role.Team != Team.SCPs)
            {
                return true;
            }

            // Czyści kolejkę gry ORAZ własną kolejkę audio (inaczej stare słowa dogrywają się po komunikacie o terminacji).
            CassiePlayback.ClearAll();
            CassieEventHandlers.HandleAnnouncingTermination(hit, role.RoleTypeId);
            return false;
        }
    }
}
