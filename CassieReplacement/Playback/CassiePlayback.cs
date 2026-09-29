namespace CassieReplacement.Playback
{
    using CassieReplacement.Reader;
    using GameAnnouncement = global::Cassie.CassieAnnouncement;
    using GameDispatcher = global::Cassie.CassieAnnouncementDispatcher;
    using GamePayload = global::Cassie.CassieTtsPayload;

    // The namespace must not be called "Cassie": it would hide the game's own Cassie namespace.
    public static class CassiePlayback
    {
        public static void Play(string words, bool noise, bool custom, string subtitles = null)
        {
            GamePayload payload = string.IsNullOrEmpty(subtitles)
                ? new GamePayload(words, noise, custom)
                : new GamePayload(words, subtitles, noise);

            GameDispatcher.AddToQueue(new GameAnnouncement(payload, 0f, 0f));
        }

        /// <summary>Clears the base-game queue together with our own, so the two never drift apart.</summary>
        public static void ClearAll()
        {
            GameDispatcher.ClearAll();
            CustomCassieReader.Singleton?.CancelAll();
        }
    }
}
