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
            if (Plugin.Singleton?.Config?.CassieOverrideConfig.ShouldOverrideAnnouncements != true)
            {
                return true;
            }

            PlayerRoleBase role = scp != null && scp.roleManager != null ? scp.roleManager.CurrentRole : null;
            if (role == null || role.Team != Team.SCPs)
            {
                return true;
            }

            CassiePlayback.ClearAll();
            CassieEventHandlers.HandleAnnouncingTermination(hit, role.RoleTypeId);
            return false;
        }
    }
}
