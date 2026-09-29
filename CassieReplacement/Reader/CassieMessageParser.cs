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

    /// <summary>
    /// REFACTOR: jeden krok odtwarzania. Zastępuje przepisywanie listy stringów w miejscu
    /// (zmienianie nazw klipów na "p1.5_slowo", wstawianie/usuwanie elementów podczas iteracji).
    /// </summary>
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

        /// <summary>Słowo wraz z prefiksem/sufiksem.</summary>
        public string Name { get; }

        /// <summary>null = brak klipu, odtwarzanie zastępujemy szacowaną pauzą.</summary>
        public CassieClip Clip { get; }

        public float Pitch { get; }

        public float Seconds { get; }

        public int JamDelay { get; }

        public int JamAmount { get; }
    }

    internal sealed class CassieMessage
    {
        public List<CassieStep> Steps { get; } = new List<CassieStep>();

        /// <summary>Tekst dla bazowego CASSIE (ciche "kropki" + szum). null gdy useCassie == false.</summary>
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
            StringBuilder baseBuilder = useCassie ? StringBuilderPool.Shared.Rent() : null;

            try
            {
                List<string> work = new List<string>(input.Count);
                foreach (string raw in input)
                {
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        work.Add(raw.ToLowerInvariant());
                    }
                }

                List<string> subtitleWords = new List<string>(work.Count);

                // FIX: prefix/suffix/pitch/jam to zmienne LOKALNE (poprzednio pola instancji Singletona - stan przeciekał między komunikatami).
                string prefix = string.Empty;
                string suffix = string.Empty;
                float pitch = 1f;
                int jamDelay = 0;
                int jamAmount = 0;

                int i = 0;
                while (i < work.Count)
                {
                    string word = work[i];

                    if (CassieTokens.TryParseJam(word, out int newJamDelay, out int newJamAmount))
                    {
                        jamDelay = newJamDelay;
                        jamAmount = newJamAmount;
                        i++;
                        continue;
                    }

                    if (CassieTokens.TryParsePitch(word, out float newPitch))
                    {
                        pitch = newPitch;
                        baseBuilder?.Append(' ').Append(word);
                        i++;
                        continue;
                    }

                    if (CassieTokens.TryParseModifier(word, out bool isPrefix, out string modifier))
                    {
                        if (isPrefix)
                        {
                            prefix = modifier;
                        }
                        else
                        {
                            suffix = modifier;
                        }

                        i++;
                        continue;
                    }

                    if (CassieTokens.TryParseYield(word, out float yieldSeconds))
                    {
                        message.Steps.Add(new CassieStep(StepKind.Yield, word, null, pitch, yieldSeconds, 0, 0));
                        baseBuilder?.Append(' ').Append(word);
                        i++;
                        continue;
                    }

                    string lookupName = prefix + word + suffix;
                    database.TryGetClip(lookupName, out CassieClip clip);

                    if (clip == null && CassieTokens.TrySpellNumber(word, out string[] digits))
                    {
                        // Liczba bez własnego klipu -> rozwijamy na cyfry i przetwarzamy je w tej samej pętli.
                        work.RemoveAt(i);
                        work.InsertRange(i, digits);
                        continue;
                    }

                    message.Steps.Add(new CassieStep(StepKind.Word, lookupName, clip, pitch, 0f, jamDelay, jamAmount));
                    jamDelay = 0;
                    jamAmount = 0;
                    subtitleWords.Add(word);

                    if (baseBuilder != null)
                    {
                        AppendToBaseAnnouncement(baseBuilder, config, lookupName, word, clip, pitch);
                    }

                    i++;
                }

                if (baseBuilder != null)
                {
                    message.BaseAnnouncement = "noparse" + baseBuilder.ToString();
                }

                // FIX: napisy nie zawierają już tokenów "pitch_x"/"jam_x_y" ani przemianowanych nazw klipów.
                message.Subtitle = string.IsNullOrWhiteSpace(translation) ? string.Join(" ", subtitleWords) : translation;
                return message;
            }
            finally
            {
                if (baseBuilder != null)
                {
                    StringBuilderPool.Shared.Return(baseBuilder);
                }
            }
        }

        private static void AppendToBaseAnnouncement(StringBuilder builder, CassieConfig config, string lookupName, string rawWord, CassieClip clip, float pitch)
        {
            bool hasOverride = config.WordsToBasegameOverride.TryGetValue(lookupName, out string overrideWord);

            if (clip == null)
            {
                builder.Append(' ').Append(hasOverride ? overrideWord : rawWord);
                return;
            }

            if (hasOverride)
            {
                builder.Append(' ').Append(overrideWord);
                return;
            }

            // Bazowy CASSIE odgrywa ciszę ("kropki") o tej samej długości co klip - dzięki temu napisy/szum idą równolegle.
            // (Martwa gałąź 'msg == "<split>"' z oryginału usunięta - klip o takiej nazwie nie może istnieć.)
            int dots = (int)Math.Round(clip.Length / pitch * DotsPerSecond, MidpointRounding.AwayFromZero);
            builder.Append(" pitch_1");
            for (int j = 0; j < dots; j++)
            {
                builder.Append(" .");
            }

            // FIX: format niezależny od kultury (na serwerze z przecinkiem dziesiętnym powstawało "pitch_1,5").
            builder.Append(" pitch_").Append(CassieTokens.FormatPitch(pitch)).Append(" jam_0_0");
        }
    }
}
