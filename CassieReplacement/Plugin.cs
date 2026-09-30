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

    public sealed class Plugin : Plugin<CassieConfig>
    {
        private const float StartupSpeakerDelaySeconds = 2f;

        private CoroutineHandle startupHandle;

        public static Plugin Singleton { get; private set; }

        public static AudioPlayer CassiePlayerGlobal => Singleton?.Speakers?.GlobalPlayer;

        public static AudioPlayer CassiePlayer => Singleton?.Speakers?.SpatialPlayer;

        public override string Name => "CASSIE Replacement";

        public override string Description => "CASSIE replacement plugin (SecretLabNAudio)";

        public override string Author => "icedchqi";

        public override Version Version { get; } = new Version(1, 10, 0);

        public override Version RequiredApiVersion { get; } = new Version(LabApiProperties.CompiledVersion);

        public SpeakerManager Speakers { get; private set; }

        public override void Enable()
        {
            Singleton = this;
            Speakers = new SpeakerManager(this);
            CustomCassieReader.Create(this, Speakers);
            RegisterConfiguredClips();

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

            startupHandle = Timing.CallDelayed(StartupSpeakerDelaySeconds, () => Speakers?.EnsureReady());
        }

        public override void Disable()
        {
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

        private void RegisterConfiguredClips()
        {
            ClipDatabase database = CustomCassieReader.Singleton.ClipDatabase;
            string defaultDirectory = CassiePaths.DefaultAudioDirectory;
            bool defaultRegistered = false;

            foreach (CassieDirectorySerializable directory in Config.BaseDirectories)
            {
                if (directory == null)
                {
                    continue;
                }

                defaultRegistered |= string.Equals(CassiePaths.Resolve(directory.Path), defaultDirectory, StringComparison.OrdinalIgnoreCase);
                database.RegisterFolder(directory, createIfMissing: true);
            }

            if (!defaultRegistered)
            {
                database.RegisterFolder(new CassieDirectorySerializable { Path = defaultDirectory }, createIfMissing: true);
            }
        }

        private void OnWaitingForPlayers()
        {
            CustomCassieReader.Singleton?.CancelAll();
            Speakers?.Rebuild();
        }

        private void OnRoundStarted() => Speakers?.Rebuild();

        private void OnRoundRestarted()
        {
            CustomCassieReader.Singleton?.CancelAll();
            Speakers?.Destroy();
        }
    }
}
