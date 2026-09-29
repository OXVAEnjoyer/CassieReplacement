namespace CassieReplacement
{
    using System;
    using CassieReplacement.Audio;
    using CassieReplacement.Config;
    using CassieReplacement.Patches;
    using CassieReplacement.Reader;
    using CassieReplacement.Reader.Models;
    using LabApi.Events.Handlers;
    using LabApi.Features;
    using LabApi.Features.Console;
    using LabApi.Loader.Features.Plugins;
    using MEC;
    using SecretLabNAudio.Core;

    // REFACTOR: EXILED (#if) usunięty - plugin jest teraz czystym LabAPI. Wersja EXILED była oznaczona jako WIP.
    public sealed class Plugin : Plugin<CassieConfig>
    {
        // FIX: wersja w jednym miejscu (README/folder mówiły 1.7.0, kod 1.9.0).
        private static readonly Version PluginVersion = new(1, 9, 0);

        private CoroutineHandle startupHandle;

        public static Plugin Singleton { get; private set; }

        // Zachowana kompatybilność wsteczna z innymi pluginami korzystającymi z tych właściwości.
        public static AudioPlayer CassiePlayerGlobal => Singleton?.Speakers?.GlobalPlayer;

        public static AudioPlayer CassiePlayer => Singleton?.Speakers?.SpatialPlayer;

        public override string Name => "CASSIE Replacement";

        public override string Description => "CASSIE replacement plugin (SecretLabNAudio)";

        public override string Author => "icedchqi";

        public override Version Version => PluginVersion;

        public override Version RequiredApiVersion => new(LabApiProperties.CompiledVersion);

        public SpeakerManager Speakers { get; private set; }

        public override void Enable()
        {
            Singleton = this;
            Speakers = new SpeakerManager(this);
            CustomCassieReader.Create(this, Speakers);

            RegisterConfiguredClips();

            // FIX: gdy patchowanie się nie uda, plugin nie zostaje w stanie "pół-włączonym".
            try
            {
                Patcher.Apply();
            }
            catch (Exception ex)
            {
                Logger.Error($"[CassieReplacement] Harmony patching failed: {ex}");
                Disable();
                return;
            }

            ServerEvents.WaitingForPlayers += OnWaitingForPlayers;
            ServerEvents.RoundStarted += OnRoundStarted;
            ServerEvents.RoundRestarted += OnRoundRestarted;

            // Serwer mógł już wystartować (plugin włączony w trakcie działania) - zbuduj głośniki po chwili.
            startupHandle = Timing.CallDelayed(StartupSpeakerDelaySeconds, () => Speakers?.EnsureReady());
        }

        public override void Disable()
        {
            // FIX: wyrejestrowanie WSZYSTKICH eventów i zatrzymanie WSZYSTKICH coroutine (MEC).
            ServerEvents.WaitingForPlayers -= OnWaitingForPlayers;
            ServerEvents.RoundStarted -= OnRoundStarted;
            ServerEvents.RoundRestarted -= OnRoundRestarted;

            Timing.KillCoroutines(startupHandle);
            Patcher.Remove();

            CustomCassieReader.Singleton?.Dispose();
            Speakers?.Dispose();
            Speakers = null;
            Singleton = null;
        }

        private const float StartupSpeakerDelaySeconds = 2f;

        private void RegisterConfiguredClips()
        {
            CustomCassieReader reader = CustomCassieReader.Singleton;

            // OPTYMALIZACJA: konfiguracji użytkownika nie mutujemy (poprzednio Enable() nadpisywał Config.BaseDirectories).
            string defaultDirectory = CassiePaths.DefaultAudioDirectory;
            bool defaultPresent = false;

            foreach (CassieDirectorySerializable directory in Config.BaseDirectories)
            {
                if (directory == null)
                {
                    continue;
                }

                string resolved = CassiePaths.Resolve(directory.Path);
                defaultPresent |= string.Equals(resolved, defaultDirectory, StringComparison.OrdinalIgnoreCase);
                reader.ClipDatabase.RegisterFolder(directory, createIfMissing: true);
            }

            if (!defaultPresent)
            {
                reader.ClipDatabase.RegisterFolder(new CassieDirectorySerializable { Path = defaultDirectory }, createIfMissing: true);
            }
        }

        private void OnWaitingForPlayers()
        {
            // FIX: stan między rundami - zawsze czyścimy kolejkę i budujemy głośniki od zera.
            CustomCassieReader.Singleton?.CancelAll();
            Speakers?.Rebuild();
        }

        private void OnRoundStarted()
        {
            // Pozycje głośników 079 mogą być znane dopiero po wygenerowaniu mapy - odśwież.
            Speakers?.Rebuild();
        }

        private void OnRoundRestarted()
        {
            // FIX: po restarcie rundy stare AudioPlayery są niszczone przez grę - nie zostawiamy do nich referencji.
            CustomCassieReader.Singleton?.CancelAll();
            Speakers?.Destroy();
        }
    }
}
