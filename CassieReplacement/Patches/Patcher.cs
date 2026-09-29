namespace CassieReplacement.Patches
{
    using HarmonyLib;

    public static class Patcher
    {
        private const string HarmonyId = "me.icedchai.cassie.patch";

        private static Harmony harmony;

        public static void Apply()
        {
            if (harmony != null)
            {
                return;
            }

            harmony = new Harmony(HarmonyId);
            try
            {
                harmony.PatchAll(typeof(Patcher).Assembly);
            }
            catch
            {
                Remove();
                throw;
            }
        }

        public static void Remove()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
        }
    }
}
