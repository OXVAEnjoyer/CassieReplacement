namespace CassieReplacement.Reader
{
    using System;
    using System.Globalization;

    internal static class CassieTokens
    {
        public const float MinPitch = 0.25f;
        public const float MaxPitch = 4f;
        public const float MaxYieldSeconds = 30f;

        private const string JamPrefix = "jam_";
        private const string PitchPrefix = "pitch_";
        private const string YieldPrefix = "yield_";
        private const string PrefixToken = "prefix_";
        private const string SuffixToken = "suffix_";
        private const int ModifierTokenLength = 7;
        private const float MinWordSeconds = 0.35f;
        private const float SecondsPerCharacter = 0.07f;

        public static bool TryParseJam(string word, out int delay, out int amount)
        {
            delay = 0;
            amount = 0;
            if (!word.StartsWith(JamPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            int separator = word.IndexOf('_', JamPrefix.Length);
            if (separator < 0 || word.IndexOf('_', separator + 1) >= 0)
            {
                return false;
            }

            return int.TryParse(word.Substring(JamPrefix.Length, separator - JamPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out delay)
                   && int.TryParse(word.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
        }

        public static bool TryParsePitch(string word, out float pitch)
        {
            if (!TryParseFloatToken(word, PitchPrefix, out pitch))
            {
                pitch = 1f;
                return false;
            }

            pitch = Math.Min(Math.Max(pitch, MinPitch), MaxPitch);
            return true;
        }

        public static bool TryParseYield(string word, out float seconds)
        {
            if (!TryParseFloatToken(word, YieldPrefix, out seconds))
            {
                seconds = 0f;
                return false;
            }

            seconds = Math.Min(Math.Max(seconds, 0f), MaxYieldSeconds);
            return true;
        }

        /// <summary>Recognises "prefix_x" and "suffix_x"; an empty value clears the modifier.</summary>
        public static bool TryParseModifier(string word, out bool isPrefix, out string value)
        {
            isPrefix = word.StartsWith(PrefixToken, StringComparison.Ordinal);
            bool isSuffix = word.StartsWith(SuffixToken, StringComparison.Ordinal);
            value = string.Empty;

            if (!isPrefix && !isSuffix)
            {
                return false;
            }

            value = word.Substring(ModifierTokenLength);
            return true;
        }

        public static float EstimateWordSeconds(string word, float pitch)
        {
            return Math.Max(MinWordSeconds, word.Length * SecondsPerCharacter) / pitch;
        }

        /// <summary>
        /// Spells a multi-digit or negative number as separate tokens. Single digits are left alone,
        /// otherwise a digit without a clip would expand into itself forever.
        /// </summary>
        public static bool TrySpellNumber(string word, out string[] tokens)
        {
            tokens = null;
            if (!int.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number)
                || (number >= 0 && number <= 9))
            {
                return false;
            }

            string digits = Math.Abs((long)number).ToString(CultureInfo.InvariantCulture);
            int offset = number < 0 ? 1 : 0;
            tokens = new string[digits.Length + offset];
            if (offset == 1)
            {
                tokens[0] = "minus";
            }

            for (int i = 0; i < digits.Length; i++)
            {
                tokens[i + offset] = digits[i].ToString();
            }

            return true;
        }

        public static string FormatPitch(float pitch) => pitch.ToString("0.###", CultureInfo.InvariantCulture);

        private static bool TryParseFloatToken(string word, string prefix, out float value)
        {
            value = 0f;
            return word.StartsWith(prefix, StringComparison.Ordinal)
                   && float.TryParse(word.Substring(prefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   && !float.IsNaN(value)
                   && !float.IsInfinity(value);
        }
    }
}
