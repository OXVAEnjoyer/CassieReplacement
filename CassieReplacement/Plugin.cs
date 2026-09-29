namespace CassieReplacement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CassieReplacement.Patches;
    using CassieReplacement.Reader;
    using CassieReplacement.Reader.Models;
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using MEC;
    using PlayerRoles.PlayableScps.Scp079;
    using SecretLabNAudio.Core;
    using SecretLabNAudio.Core.Extensions;
    using UnityEngine;
#if EXILED
    using Exiled.API.Features;
    using Exiled.CustomItems.API.Features;
#else
    using LabApi.Loader.Features.Plugins;
    using LabApi.Features;
#endif

    public class Plugin : Plugin<Config.Config>
    {
        public static AudioPlayer CassiePlayer { get; private set; }

        public static AudioPlayer CassiePlayerGlobal { get; private set; }

        public static Plugin Singleton;

        public override string Name => "CASSIE Replacement";

#if EXILED
        public override string Prefix => "cassie_replacement";

        public override Version RequiredExiledVersion => new Version(9, 14, 2);

        private CassieEventHandlers cassieEventHandlers { get; set; }
#else
        public override string Description => "CASSIE replacement plugin (SecretLabNAudio)";

        public override Version RequiredApiVersion => new(LabApiProperties.CompiledVersion);
#endif

        public override string Author => "icedchqi";

        public override Version Version => new(1, 9, 0);

        private IEnumerable<Scp079InteractableBase> allSpeakers;

        private readonly List<ReferenceHub> globalListenerHubs = new List<ReferenceHub>();

        internal List<AudioPlayer> CassieAudioPlayers { get; private set; } = new List<AudioPlayer>();

        public void InitSpeaker()
        {
            DestroySpeakers();
            CassieAudioPlayers.Clear();
            globalListenerHubs.Clear();
            allSpeakers = Scp079Speaker.AllInstances.Where(s => s is Scp079Speaker);

            if (Config.UseGlobalSpeaker)
            {
                SpeakerSettings globalSettings = SpeakerSettings.GloballyAudible with
                {
                    Volume = Config.GlobalSpeakerVolume,
                };
                CassiePlayerGlobal = AudioPlayer.Create(globalSettings);
                if (Config.UseSpatialSpeakers)
                {
                    CassiePlayerGlobal.WithFilteredSendEngine(player => ShouldHearGlobal(player));
                }

                CassieAudioPlayers.Add(CassiePlayerGlobal);
            }

            if (Config.UseSpatialSpeakers)
            {
                List<Vector3> positions = new List<Vector3>();
                foreach (Scp079InteractableBase speaker in allSpeakers)
                {
                    if (Config.GlobalForSurfaceOnly && speaker.Room.Zone == FacilityZone.Surface)
                    {
                        continue;
                    }

                    positions.Add(speaker.Position);
                }

                if (positions.Count > 0)
                {
                    SpeakerSettings spatialSettings = new SpeakerSettings
                    {
                        IsSpatial = true,
                        Volume = Config.SpatialSpeakerVolume,
                        MinDistance = Config.SpatialSpeakerMinDistance,
                        MaxDistance = Config.SpatialSpeakerMaxDistance,
                    };

                    Vector3 first = positions[0];
                    CassiePlayer = AudioPlayer.Create(spatialSettings, first)
                        .WithFilteredSendEngine(player => player != null && !player.IsHost && player.ReferenceHub != null && !globalListenerHubs.Contains(player.ReferenceHub));

                    if (positions.Count > 1)
                    {
                        CassiePlayer.CloneOutput(spatialSettings, positions.Skip(1));
                    }

                    CassieAudioPlayers.Add(CassiePlayer);
                }
            }

            if (CustomCassieReader.Singleton != null)
            {
                CustomCassieReader.Singleton.AudioPlayers = CassieAudioPlayers;
            }
        }

        public void EnsureSpeakers()
        {
            if (CassieAudioPlayers != null && CassieAudioPlayers.Count > 0 && CassieAudioPlayers.Any(p => p != null))
            {
                if (CustomCassieReader.Singleton != null)
                {
                    CustomCassieReader.Singleton.AudioPlayers = CassieAudioPlayers;
                }

                return;
            }

            InitSpeaker();
            if (CassieAudioPlayers.Count == 0)
            {
                CassiePlayerGlobal = AudioPlayer.Create(SpeakerSettings.GloballyAudible with
                {
                    Volume = Config.GlobalSpeakerVolume > 0f ? Config.GlobalSpeakerVolume : 1f,
                });
                CassieAudioPlayers.Add(CassiePlayerGlobal);
            }

            if (CustomCassieReader.Singleton != null)
            {
                CustomCassieReader.Singleton.AudioPlayers = CassieAudioPlayers;
            }
        }

        private bool ShouldHearGlobal(LabApi.Features.Wrappers.Player player)
        {
            if (player == null || player.IsHost || player.ReferenceHub == null)
            {
                if (player?.ReferenceHub != null)
                {
                    globalListenerHubs.Remove(player.ReferenceHub);
                }

                return false;
            }

            ReferenceHub hub = player.ReferenceHub;
            if (!Config.UseSpatialSpeakers || !hub.TryGetCurrentRoom(out RoomIdentifier room) || (Config.GlobalForSurfaceOnly && room.Zone == FacilityZone.Surface))
            {
                if (!globalListenerHubs.Contains(hub))
                {
                    globalListenerHubs.Add(hub);
                }

                return true;
            }

            IEnumerable<Scp079InteractableBase> speakers = allSpeakers.Where(s => LabApi.Features.Wrappers.Room.Get(s.Room) == LabApi.Features.Wrappers.Room.Get(room));
            bool hearGlobal = speakers.IsEmpty() || speakers.Any(s => Vector3.Distance(hub.PlayerCameraReference.position, s.Position) >= Config.SpatialSpeakerMaxDistance);

            if (hearGlobal && !globalListenerHubs.Contains(hub))
            {
                globalListenerHubs.Add(hub);
            }
            else if (!hearGlobal && globalListenerHubs.Contains(hub))
            {
                globalListenerHubs.Remove(hub);
            }

            return hearGlobal;
        }

        private void DestroySpeakers()
        {
            if (CassiePlayerGlobal != null)
            {
                CassiePlayerGlobal.Destroy();
                CassiePlayerGlobal = null;
            }

            if (CassiePlayer != null)
            {
                CassiePlayer.Destroy();
                CassiePlayer = null;
            }

            CassieAudioPlayers.Clear();
            globalListenerHubs.Clear();
        }

#if EXILED
        public override void OnEnabled()
        {
            base.OnEnabled();
            cassieEventHandlers = new();
            cassieEventHandlers.Register();
#if CUSTOMITEM
            CustomItem.RegisterItems();
#endif
#else
        public override void Enable()
        {
#endif
            Singleton = this;
            CustomCassieReader.Singleton = new CustomCassieReader();
            Patcher.DoPatching();

            string audioDir = CassiePaths.DefaultAudioDirectory;
            foreach (CassieDirectorySerializable configDir in Config.BaseDirectories)
            {
                if (string.IsNullOrWhiteSpace(configDir.Path)
                    || configDir.Path.Equals("C:/test", StringComparison.OrdinalIgnoreCase)
                    || configDir.Path.IndexOf(CassiePaths.Placeholder, StringComparison.OrdinalIgnoreCase) >= 0
                    || configDir.Path.IndexOf("{exiled_config}", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    configDir.Path = audioDir;
                }
            }
            if (!Config.BaseDirectories.Any(d => string.Equals(CassiePaths.Resolve(d.Path), audioDir, StringComparison.OrdinalIgnoreCase)))
            {
                Config.BaseDirectories.Insert(0, new CassieDirectorySerializable { Path = audioDir });
            }

            foreach (CassieDirectorySerializable configDir in Config.BaseDirectories)
            {
                CustomCassieReader.Singleton.ClipDatabase.RegisterFolder(configDir);
            }

            Timing.CallDelayed(2f, EnsureSpeakers);
            Timing.CallDelayed(10f, () =>
            {
                Timing.RunCoroutine(CustomCassieReader.CassieCheck());
            });

            LabApi.Events.Handlers.ServerEvents.WaitingForPlayers += EnsureSpeakers;
            LabApi.Events.Handlers.ServerEvents.RoundStarted += InitSpeaker;
        }

#if EXILED
        public override void OnDisabled()
        {
            base.OnDisabled();
            cassieEventHandlers.Unregister();
            cassieEventHandlers = null;

#if CUSTOMITEM
            CustomItem.UnregisterItems();
#endif
#else
        public override void Disable()
        {
#endif
            Patcher.DoUnpatch();
            LabApi.Events.Handlers.ServerEvents.WaitingForPlayers -= EnsureSpeakers;
            LabApi.Events.Handlers.ServerEvents.RoundStarted -= InitSpeaker;
            DestroySpeakers();
            Singleton = null;
            CustomCassieReader.Singleton = null;
        }
    }
}
