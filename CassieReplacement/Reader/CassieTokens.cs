namespace CassieReplacement.Reader
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// REFACTOR: zastępuje Compat/NineTailedFoxAnnouncer (klasa w globalnej przestrzeni nazw z polem 'singleton' małą literą,
    /// atrapą kolejki i statycznymi klasami zagnieżdżonymi). Tu tylko parsowanie tokenów i szacowanie czasu.
    /// </summary>
    internal static class CassieTokens
    {
        public const string JamPrefix = "jam_";
        public const string PitchPrefix = "pitch_";
        public const string YieldPrefix = "yield_";
        public const string PrefixToken = "prefix_";
        public const string SuffixToken = "suffix_";

        // Stałe zamiast magic numbers rozsianych po kodzie.
        public const float MinPitch = 0.25f;
        public const float MaxPitch = 4f;
        public const float MaxYieldSeconds = 30f;
        private const float MinWordSeconds = 0.35f;
        private const float SecondsPerCharacter = 0.07f;
        private const int ModifierTokenLength = 7; // "prefix_" / "suffix_"

        // OPTYMALIZACJA: bez Split('_') na każdym słowie (poprzednio alokacja tablicy 3x na słowo).
        public static bool TryParseJam(string word, out int delay, out int amount)
        {
            delay = 0;
            amount = 0;
            if (word == null || !word.StartsWith(JamPrefix, StringComparison.OrdinalIgnoreCase))
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
            // FIX: wartość jest walidowana - pitch 0/ujemny/NaN dawał dzielenie przez zero i WaitForSeconds(NaN).
            if (TryParseFloatToken(word, PitchPrefix, out pitch))
            {
                pitch = Math.Min(Math.Max(pitch, MinPitch), MaxPitch);
                return true;
            }

            pitch = 1f;
            return false;
        }

        public static bool TryParseYield(string word, out float seconds)
        {
            if (TryParseFloatToken(word, YieldPrefix, out seconds))
            {
                seconds = Math.Min(Math.Max(seconds, 0f), MaxYieldSeconds);
                return true;
            }

            seconds = 0f;
            return false;
        }

        public static bool TryParseModifier(string word, out bool isPrefix, out string value)
        {
            isPrefix = word.StartsWith(PrefixToken, StringComparison.OrdinalIgnoreCase);
            bool isSuffix = !isPrefix && word.StartsWith(SuffixToken, StringComparison.OrdinalIgnoreCase);
            value = string.Empty;

            if (!isPrefix && !isSuffix)
            {
                return false;
            }

            value = word.Length > ModifierTokenLength ? word.Substring(ModifierTokenLength) : string.Empty;
            return true;
        }

        public static float EstimateWordSeconds(string word, float pitch)
        {
            float seconds = Math.Max(MinWordSeconds, word.Length * SecondsPerCharacter);
            return seconds / (pitch > 0f ? pitch : 1f);
        }

        /// <summary>Liczba -> osobne cyfry ("minus" dla ujemnych). Zwraca false dla pojedynczej cyfry (nic do rozwinięcia).</summary>
        public static bool TrySpellNumber(string word, out string[] tokens)
        {
            tokens = null;
            if (!int.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number))
            {
                return false;
            }

            // FIX (krytyczny): pojedyncza cyfra rozwijała się w samą siebie -> nieskończona pętla i freeze serwera,
            // gdy brakowało klipu np. "7.ogg" (lub był ustawiony prefix/suffix).
            if (number >= 0 && number <= 9)
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
            if (word == null || !word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return float.TryParse(word.Substring(prefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   && !float.IsNaN(value)
                   && !float.IsInfinity(value);
        }
    }
}
