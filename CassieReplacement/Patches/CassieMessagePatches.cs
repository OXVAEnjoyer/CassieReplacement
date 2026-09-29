namespace CassieReplacement.Patches
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using CassieReplacement.Reader;
    using HarmonyLib;
    using NorthwoodLib.Pools;

    public static class CassieIntercept
    {
        private const string NoParseToken = "noparse";
        private const string NoCassieToken = "nocassie";
        private const string SizeZeroTag = "<size=0>";
        private const string SizeCloseTag = "</size>";
        private const string SizeSplitTag = "</size><split>";
        private const string SplitTag = "<split>";
        private const string DefaultPrefix = "customcassie";

        private static readonly char[] WordSeparators = { ' ' };

        public static bool TryTakeOver(string words, bool makeNoise, bool customAnnouncement)
        {
            if (string.IsNullOrWhiteSpace(words) || ContainsToken(words, NoParseToken))
            {
                return false;
            }

            Plugin plugin = Plugin.Singleton;
            CustomCassieReader reader = CustomCassieReader.Singleton;
            if (plugin?.Config == null || reader == null)
            {
                return false;
            }

            string prefix = plugin.Config.CustomCassiePrefix ?? DefaultPrefix;
            bool hasPrefix = ContainsToken(words, prefix);
            if (!hasPrefix && !plugin.Config.CassieOverrideConfig.ShouldOverrideAll)
            {
                return false;
            }

            bool useCassie = !ContainsToken(words, NoCassieToken);

            if (words.IndexOf(SizeZeroTag, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                HandleSized(reader, words, prefix, hasPrefix, makeNoise, customAnnouncement, useCassie);
                return true;
            }

            string[] wordSplit = words.Split(new[] { ';' }, 2);
            List<string> input = new List<string>();
            foreach (string word in wordSplit[0].Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!word.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                    && !word.Equals(NoCassieToken, StringComparison.OrdinalIgnoreCase))
                {
                    input.Add(word);
                }
            }

            string subtitles = wordSplit.Length > 1 ? wordSplit[1].Trim() : string.Empty;
            reader.CassieReadMessage(input, makeNoise, customAnnouncement, subtitles, useCassie);
            return true;
        }

        private static void HandleSized(CustomCassieReader reader, string words, string prefix, bool hasPrefix, bool makeNoise, bool customAnnouncement, bool useCassie)
        {
            string[] sections = words.Split(new[] { SizeSplitTag }, StringSplitOptions.None);

            if (hasPrefix)
            {
                List<string> head = new List<string>();
                foreach (string word in sections[0].Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!word.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        head.Add(word);
                    }
                }

                sections[0] = string.Join(" ", head);
            }

            StringBuilder subtitles = StringBuilderPool.Shared.Rent();
            StringBuilder input = StringBuilderPool.Shared.Rent();
            string inputText;
            string subtitleText;

            try
            {
                bool isFirstSection = true;
                foreach (string section in sections)
                {
                    if (string.IsNullOrWhiteSpace(section))
                    {
                        continue;
                    }

                    if (!isFirstSection)
                    {
                        subtitles.Append(SplitTag);
                        input.Append(SplitTag);
                    }

                    isFirstSection = false;

                    string[] parts = section.Split(new[] { SizeZeroTag }, StringSplitOptions.None);
                    subtitles.Append(parts[0]);
                    if (parts.Length > 1)
                    {
                        input.Append(parts[1].Replace(SizeCloseTag, string.Empty));
                    }
                }

                inputText = input.ToString();
                subtitleText = subtitles.ToString();
            }
            finally
            {
                StringBuilderPool.Shared.Return(input);
                StringBuilderPool.Shared.Return(subtitles);
            }

            reader.CassieReadMessage(
                inputText.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries),
                makeNoise,
                customAnnouncement,
                subtitleText,
                useCassie);
        }

        private static bool ContainsToken(string words, string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            int start = 0;
            while (start < words.Length)
            {
                while (start < words.Length && IsSeparator(words[start]))
                {
                    start++;
                }

                int end = start;
                while (end < words.Length && !IsSeparator(words[end]))
                {
                    end++;
                }

                if (end - start == token.Length && string.Compare(words, start, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }

                start = end;
            }

            return false;
        }

        private static bool IsSeparator(char c) => c == ' ' || c == ';';
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
        private const float PlaceholderDurationSeconds = 0.05f;

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

            totalDuration = PlaceholderDurationSeconds;
            __result = true;
            return false;
        }
    }
}
