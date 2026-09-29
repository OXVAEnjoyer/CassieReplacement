namespace CassieReplacement.Patches
{
    using CassieReplacement.Reader;
    using HarmonyLib;
    using NorthwoodLib.Pools;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public static class CassieIntercept
    {
        public static bool TryTakeOver(string words, bool makeNoise, bool customAnnouncement)
        {
            if (string.IsNullOrWhiteSpace(words))
            {
                return false;
            }

            if (ContainsToken(words, "noparse"))
            {
                return false;
            }

            if (Plugin.Singleton?.Config == null || CustomCassieReader.Singleton == null)
            {
                return false;
            }

            string prefix = Plugin.Singleton.Config.CustomCassiePrefix ?? "customcassie";
            bool overrideAll = Plugin.Singleton.Config.CassieOverrideConfig.ShouldOverrideAll;
            bool hasPrefix = ContainsToken(words, prefix)
                || words.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

            if (!hasPrefix && !overrideAll)
            {
                return false;
            }

            bool useCassie = !ContainsToken(words, "nocassie");

            if (words.IndexOf("<size=0>", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                HandleSized(words, prefix, hasPrefix, makeNoise, customAnnouncement, useCassie);
                return true;
            }
            string[] wordsplit = words.Split(new[] { ';' }, 2);
            List<string> input = wordsplit[0]
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLowerInvariant())
                .ToList();

            input.RemoveAll(w =>
                w.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || w.Equals("nocassie", StringComparison.OrdinalIgnoreCase));

            string subtitles = wordsplit.Length > 1 ? wordsplit[1].Trim() : string.Empty;

            Plugin.Singleton.EnsureSpeakers();
            CustomCassieReader.Singleton.CassieReadMessage(input, makeNoise, customAnnouncement, subtitles, useCassie);
            return true;
        }

        private static void HandleSized(string words, string prefix, bool hasPrefix, bool makeNoise, bool customAnnouncement, bool useCassie)
        {
            string[] dividedBySplits = words.Split(new[] { "</size><split>" }, StringSplitOptions.None);

            if (hasPrefix && dividedBySplits.Length > 0)
            {
                string[] head = dividedBySplits[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                dividedBySplits[0] = string.Join(
                    " ",
                    head.Where(w => !w.Equals(prefix, StringComparison.OrdinalIgnoreCase)));
            }

            StringBuilder subtitles = StringBuilderPool.Shared.Rent();
            StringBuilder input = StringBuilderPool.Shared.Rent();

            for (int i = 0; i < dividedBySplits.Length; i++)
            {
                string section = dividedBySplits[i];
                if (string.IsNullOrWhiteSpace(section))
                {
                    continue;
                }

                string[] dividedBySize = section.Split(new[] { "<size=0>" }, StringSplitOptions.None);
                subtitles.Append(dividedBySize[0]);
                input.Append(dividedBySize.TryGet(1, out string input1) ? input1 : string.Empty);
                if (i < dividedBySplits.Length - 2)
                {
                    subtitles.Append("<split>");
                    input.Append("<split>");
                }
            }

            Plugin.Singleton.EnsureSpeakers();
            CustomCassieReader.Singleton.CassieReadMessage(
                input.ToString().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                makeNoise,
                customAnnouncement,
                subtitles.ToString(),
                useCassie);
            StringBuilderPool.Shared.Return(input);
            StringBuilderPool.Shared.Return(subtitles);
        }

        private static bool ContainsToken(string words, string token)
        {
            if (string.IsNullOrEmpty(words) || string.IsNullOrEmpty(token))
            {
                return false;
            }

            foreach (string part in words.Split(new[] { ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Equals(token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Cassie.CassieAnnouncementDispatcher), nameof(Cassie.CassieAnnouncementDispatcher.AddToQueue))]
    public static class CassieAddToQueuePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Cassie.CassieAnnouncement announcement, ref bool __result)
        {
            if (announcement == null)
            {
                return true;
            }

            Cassie.CassieTtsPayload payload = announcement.Payload;
            if (!CassieIntercept.TryTakeOver(
                    payload.Content,
                    payload.PlayBackground,
                    payload.SubtitleSource != Cassie.CassieTtsPayload.SubtitleMode.None))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Cassie.CassieTtsAnnouncer), nameof(Cassie.CassieTtsAnnouncer.TryPlay))]
    public static class CassieTryPlayPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Cassie.CassieTtsPayload tts, ref float totalDuration, ref bool __result)
        {
            if (!CassieIntercept.TryTakeOver(
                    tts.Content,
                    tts.PlayBackground,
                    tts.SubtitleSource != Cassie.CassieTtsPayload.SubtitleMode.None))
            {
                return true;
            }

            totalDuration = 0.05f;
            __result = true;
            return false;
        }
    }
}
