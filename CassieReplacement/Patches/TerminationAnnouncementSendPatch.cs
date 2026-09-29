namespace CassieReplacement.Patches
{
    using HarmonyLib;
    using PlayerRoles;
    using PlayerStatsSystem;

    [HarmonyPatch(typeof(Cassie.CassieScpTerminationAnnouncement), nameof(Cassie.CassieScpTerminationAnnouncement.AnnounceScpTermination))]
    public static class TerminationAnnouncementSendPatch
    {
        public static bool Prefix(ReferenceHub scp, DamageHandlerBase hit)
        {
            if (!Plugin.Singleton.Config.CassieOverrideConfig.ShouldOverrideAnnouncements)
            {
                return true;
            }

            if (scp == null || scp.roleManager == null || scp.roleManager.CurrentRole == null || scp.roleManager.CurrentRole.Team != Team.SCPs)
            {
                return true;
            }

            global::Cassie.CassieAnnouncementDispatcher.ClearAll();
            CassieEventHandlers.HandleAnnouncingTermination(hit, scp.roleManager.CurrentRole.RoleTypeId);

            return false;
        }
    }
}
