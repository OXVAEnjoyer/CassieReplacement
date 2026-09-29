namespace CassieReplacement.Playback
{
    using CassieReplacement.Reader;
    using GameAnnouncement = global::Cassie.CassieAnnouncement;
    using GameDispatcher = global::Cassie.CassieAnnouncementDispatcher;
    using GamePayload = global::Cassie.CassieTtsPayload;

    public static class CassiePlayback
    {
        public static void Play(string words, bool noise, bool custom, string subtitles = null)
        {
            GamePayload payload = string.IsNullOrEmpty(subtitles)
                ? new GamePayload(words, noise, custom)
                : new GamePayload(words, subtitles, noise);

            GameDispatcher.AddToQueue(new GameAnnouncement(payload, 0f, 0f));
        }

        public static void ClearAll()
        {
            GameDispatcher.ClearAll();
            CustomCassieReader.Singleton?.CancelAll();
        }
    }
}
