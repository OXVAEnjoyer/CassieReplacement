namespace CassieReplacement.Reader
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using CassieReplacement.Config;
    using CassieReplacement.Reader.Models;
    using NorthwoodLib.Pools;

    internal enum StepKind
    {
        Word,
        Yield,
    }

    internal readonly struct CassieStep
    {
        public CassieStep(StepKind kind, string name, CassieClip clip, float pitch, float seconds, int jamDelay, int jamAmount)
        {
            Kind = kind;
            Name = name;
            Clip = clip;
            Pitch = pitch;
            Seconds = seconds;
            JamDelay = jamDelay;
            JamAmount = jamAmount;
        }

        public StepKind Kind { get; }

        public string Name { get; }

        public CassieClip Clip { get; }

        public float Pitch { get; }

        public float Seconds { get; }

        public int JamDelay { get; }

        public int JamAmount { get; }
    }

    internal sealed class CassieMessage
    {
        public List<CassieStep> Steps { get; } = new List<CassieStep>();

        public string BaseAnnouncement { get; set; }

        public string Subtitle { get; set; } = string.Empty;

        public float LeadInSeconds { get; set; }

        public Task Prepared { get; set; } = Task.CompletedTask;
    }

    internal static class CassieMessageParser
    {
        private const float DotsPerSecond = 2f;

        public static CassieMessage Parse(IReadOnlyList<string> input, ClipDatabase database, CassieConfig config, bool useCassie, string translation)
        {
            CassieMessage message = new CassieMessage();
            StringBuilder baseAnnouncement = useCassie ? StringBuilderPool.Shared.Rent() : null;

            try
            {
                List<string> words = new List<string>(input.Count);
                foreach (string raw in input)
                {
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        words.Add(raw.ToLowerInvariant());
                    }
                }

                List<string> subtitleWords = new List<string>(words.Count);
                string prefix = string.Empty;
                string suffix = string.Empty;
                float pitch = 1f;
                int jamDelay = 0;
                int jamAmount = 0;

                int index = 0;
                while (index < words.Count)
                {
                    string word = words[index];

                    if (CassieTokens.TryParseJam(word, out int newJamDelay, out int newJamAmount))
                    {
                        jamDelay = newJamDelay;
                        jamAmount = newJamAmount;
                    }
                    else if (CassieTokens.TryParsePitch(word, out float newPitch))
                    {
                        pitch = newPitch;
                        baseAnnouncement?.Append(' ').Append(word);
                    }
                    else if (CassieTokens.TryParseModifier(word, out bool isPrefix, out string modifier))
                    {
                        if (isPrefix)
                        {
                            prefix = modifier;
                        }
                        else
                        {
                            suffix = modifier;
                        }
                    }
                    else if (CassieTokens.TryParseYield(word, out float yieldSeconds))
                    {
                        message.Steps.Add(new CassieStep(StepKind.Yield, word, null, pitch, yieldSeconds, 0, 0));
                        baseAnnouncement?.Append(' ').Append(word);
                    }
                    else
                    {
                        string lookupName = prefix + word + suffix;
                        database.TryGetClip(lookupName, out CassieClip clip);

                        if (clip == null && CassieTokens.TrySpellNumber(word, out string[] digits))
                        {
                            words.RemoveAt(index);
                            words.InsertRange(index, digits);
                            continue;
                        }

                        message.Steps.Add(new CassieStep(StepKind.Word, lookupName, clip, pitch, 0f, jamDelay, jamAmount));
                        jamDelay = 0;
                        jamAmount = 0;
                        subtitleWords.Add(word);

                        if (baseAnnouncement != null)
                        {
                            AppendWord(baseAnnouncement, config, lookupName, word, clip, pitch);
                        }
                    }

                    index++;
                }

                if (baseAnnouncement != null)
                {
                    message.BaseAnnouncement = "noparse" + baseAnnouncement;
                }

                message.Subtitle = string.IsNullOrWhiteSpace(translation) ? string.Join(" ", subtitleWords) : translation;
                return message;
            }
            finally
            {
                if (baseAnnouncement != null)
                {
                    StringBuilderPool.Shared.Return(baseAnnouncement);
                }
            }
        }

        private static void AppendWord(StringBuilder builder, CassieConfig config, string lookupName, string word, CassieClip clip, float pitch)
        {
            bool hasOverride = config.WordsToBasegameOverride.TryGetValue(lookupName, out string overrideWord);

            if (hasOverride || clip == null)
            {
                builder.Append(' ').Append(hasOverride ? overrideWord : word);
                return;
            }

            int dots = (int)Math.Round(clip.Length / pitch * DotsPerSecond, MidpointRounding.AwayFromZero);
            builder.Append(" pitch_1");
            for (int i = 0; i < dots; i++)
            {
                builder.Append(" .");
            }

            builder.Append(" pitch_").Append(CassieTokens.FormatPitch(pitch)).Append(" jam_0_0");
        }
    }
}
