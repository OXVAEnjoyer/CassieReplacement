namespace CassieReplacement.Patches
{
    using HarmonyLib;

    public static class Patcher
    {
        private static Harmony HarmonyInstance { get; set; }

        public static void DoPatching()
        {
            HarmonyInstance = new Harmony("me.icedchai.cassie.patch");
            HarmonyInstance.PatchAll(typeof(Patcher).Assembly);
        }

        public static void DoUnpatch()
        {
            HarmonyInstance?.UnpatchAll("me.icedchai.cassie.patch");
            HarmonyInstance = null;
        }
    }
}
