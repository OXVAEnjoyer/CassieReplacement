using System;
using System.Globalization;
using System.Text;
using GameCassie = Cassie.CassieAnnouncement;
using GameDispatcher = Cassie.CassieAnnouncementDispatcher;
using GamePayload = Cassie.CassieTtsPayload;

public class NineTailedFoxAnnouncer
{
    public static readonly NineTailedFoxAnnouncer singleton = new NineTailedFoxAnnouncer();

    public readonly AnnouncementQueue queue = new AnnouncementQueue();

    public float CalculateDuration(string text, float speed = 1f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0f;

        if (speed <= 0f)
            speed = 1f;

        float seconds = 0f;
        foreach (string word in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (VoiceLine.IsJam(word, out _, out _) || VoiceLine.IsPitch(word, out _) || VoiceLine.IsYield(word, out _))
                continue;

            seconds += Math.Max(0.35f, word.Length * 0.07f);
        }

        return seconds / speed;
    }

    public static string ConvertNumber(int number)
    {
        if (number < 0)
            return "minus " + ConvertNumber(-number);

        string digits = number.ToString(CultureInfo.InvariantCulture);
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < digits.Length; i++)
        {
            if (i > 0)
                builder.Append(' ');

            builder.Append(digits[i]);
        }

        return builder.ToString();
    }

    public sealed class AnnouncementQueue
    {
        public int Count
        {
            get { return GameDispatcher.AllAnnouncementsPreview.Count; }
        }
    }

    public static class VoiceLine
    {
        public static bool IsJam(string word, out int delay, out int amount)
        {
            delay = 0;
            amount = 0;
            if (string.IsNullOrEmpty(word))
                return false;

            string[] parts = word.Split('_');
            if (parts.Length != 3 || !string.Equals(parts[0], "jam", StringComparison.OrdinalIgnoreCase))
                return false;

            return int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out delay)
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
        }

        public static bool IsPitch(string word, out float pitch)
        {
            pitch = 1f;
            if (string.IsNullOrEmpty(word) || !word.StartsWith("pitch_", StringComparison.OrdinalIgnoreCase))
                return false;

            return float.TryParse(word.Substring(6), NumberStyles.Float, CultureInfo.InvariantCulture, out pitch);
        }

        public static bool IsYield(string word, out float yield)
        {
            yield = 0f;
            if (string.IsNullOrEmpty(word) || !word.StartsWith("yield_", StringComparison.OrdinalIgnoreCase))
                return false;

            return float.TryParse(word.Substring(6), NumberStyles.Float, CultureInfo.InvariantCulture, out yield);
        }
    }
}

public static class CassiePlayback
{
    public static void Play(string words, bool hold, bool noise, bool custom, string subtitles = null)
    {
        GamePayload payload = string.IsNullOrEmpty(subtitles)
            ? new GamePayload(words, noise, custom)
            : new GamePayload(words, subtitles, noise);
        GameDispatcher.AddToQueue(new GameCassie(payload, 0f, 0f));
    }
}
