namespace CassieReplacement.Patches
{
    using HarmonyLib;

    public static class Patcher
    {
        private const string HarmonyId = "me.icedchai.cassie.patch";

        private static Harmony harmony;

        /// <summary>Idempotentne. Przy błędzie (np. zmieniona sygnatura metody po aktualizacji gry) cofa już założone patche.</summary>
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
            // OPTYMALIZACJA: UnpatchSelf zamiast UnpatchAll(id) - zdejmuje wyłącznie nasze patche.
            harmony?.UnpatchSelf();
            harmony = null;
        }
    }
}
