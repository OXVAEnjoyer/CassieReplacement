namespace CassieReplacement.Playback
{
    using CassieReplacement.Reader;
    using GameAnnouncement = global::Cassie.CassieAnnouncement;
    using GameDispatcher = global::Cassie.CassieAnnouncementDispatcher;
    using GamePayload = global::Cassie.CassieTtsPayload;

    // UWAGA: namespace celowo NIE nazywa się "Cassie" - kolidowałby z globalnym namespace gry (Cassie.*).
    public static class CassiePlayback
    {
        /// <summary>
        /// Wysyła komunikat do bazowego CASSIE gry.
        /// REFACTOR: usunięty nieużywany parametr 'hold'.
        /// </summary>
        public static void Play(string words, bool noise, bool custom, string subtitles = null)
        {
            GamePayload payload = string.IsNullOrEmpty(subtitles)
                ? new GamePayload(words, noise, custom)
                : new GamePayload(words, subtitles, noise);

            GameDispatcher.AddToQueue(new GameAnnouncement(payload, 0f, 0f));
        }

        /// <summary>Czyści kolejkę gry ORAZ własną kolejkę audio - inaczej oba systemy się rozjeżdżają.</summary>
        public static void ClearAll()
        {
            GameDispatcher.ClearAll();
            CustomCassieReader.Singleton?.CancelAll();
        }
    }
}
