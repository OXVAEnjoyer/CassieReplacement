namespace CassieReplacement.Audio
{
    using System;
    using System.Collections.Generic;
    using CassieReplacement.Config;
    using Logger = LabApi.Features.Console.Logger;
    using MapGeneration;
    using PlayerRoles.PlayableScps.Scp079;
    using SecretLabNAudio.Core;
    using SecretLabNAudio.Core.Extensions;
    using UnityEngine;

    /// <summary>Owns every audio player used to voice CASSIE and decides who hears which speaker.</summary>
    public sealed class SpeakerManager : IDisposable
    {
        private readonly Plugin plugin;

        private readonly List<AudioPlayer> players = new List<AudioPlayer>();

        private readonly Dictionary<RoomIdentifier, List<Vector3>> speakersByRoom = new Dictionary<RoomIdentifier, List<Vector3>>();

        // The send-engine filters run for every listener and every audio packet, so the result is cached per frame.
        private readonly Dictionary<ReferenceHub, bool> rangeCache = new Dictionary<ReferenceHub, bool>();

        private int rangeCacheFrame = -1;

        public SpeakerManager(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public IReadOnlyList<AudioPlayer> Players => players;

        public AudioPlayer GlobalPlayer { get; private set; }

        public AudioPlayer SpatialPlayer { get; private set; }

        private CassieConfig Config => plugin.Config;

        public void Rebuild()
        {
            Destroy();

            if (Config.UseGlobalSpeaker)
            {
                CreateGlobalPlayer(Config.GlobalSpeakerVolume, filtered: true);
            }

            if (Config.UseSpatialSpeakers)
            {
                CreateSpatialPlayers();
            }
        }

        public void EnsureReady()
        {
            if (players.Count > 0)
            {
                return;
            }

            Rebuild();
            if (players.Count == 0)
            {
                CreateGlobalPlayer(Config.GlobalSpeakerVolume > 0f ? Config.GlobalSpeakerVolume : 1f, filtered: false);
            }
        }

        public void StopAll()
        {
            foreach (AudioPlayer player in players)
            {
                try
                {
                    player?.WithoutProvider();
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[CassieReplacement] Could not stop an audio player: {ex.Message}");
                }
            }
        }

        public void Destroy()
        {
            foreach (AudioPlayer player in players)
            {
                try
                {
                    player?.Destroy();
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[CassieReplacement] Could not destroy an audio player: {ex.Message}");
                }
            }

            players.Clear();
            speakersByRoom.Clear();
            rangeCache.Clear();
            GlobalPlayer = null;
            SpatialPlayer = null;
        }

        public void Dispose() => Destroy();

        private static bool IsValidListener(LabApi.Features.Wrappers.Player player)
        {
            return player != null && !player.IsHost && player.ReferenceHub != null;
        }

        private void CreateGlobalPlayer(float volume, bool filtered)
        {
            SpeakerSettings settings = SpeakerSettings.GloballyAudible with { Volume = volume };
            GlobalPlayer = AudioPlayer.Create(settings);

            if (filtered && Config.UseSpatialSpeakers)
            {
                GlobalPlayer.WithFilteredSendEngine(player => IsValidListener(player) && !IsInSpatialRange(player.ReferenceHub));
            }

            players.Add(GlobalPlayer);
        }

        private void CreateSpatialPlayers()
        {
            List<Vector3> positions = CollectSpeakerPositions();
            if (positions.Count == 0)
            {
                return;
            }

            SpeakerSettings settings = new SpeakerSettings
            {
                IsSpatial = true,
                Volume = Config.SpatialSpeakerVolume,
                MinDistance = Config.SpatialSpeakerMinDistance,
                MaxDistance = Config.SpatialSpeakerMaxDistance,
            };

            SpatialPlayer = AudioPlayer.Create(settings, positions[0])
                .WithFilteredSendEngine(player => IsValidListener(player) && (!Config.UseGlobalSpeaker || IsInSpatialRange(player.ReferenceHub)));

            if (positions.Count > 1)
            {
                SpatialPlayer.CloneOutput(settings, positions.GetRange(1, positions.Count - 1));
            }

            players.Add(SpatialPlayer);
        }

        private List<Vector3> CollectSpeakerPositions()
        {
            List<Vector3> positions = new List<Vector3>();

            foreach (Scp079InteractableBase interactable in Scp079Speaker.AllInstances)
            {
                if (interactable is not Scp079Speaker speaker)
                {
                    continue;
                }

                RoomIdentifier room = speaker.Room;
                if (room == null || (Config.GlobalForSurfaceOnly && room.Zone == FacilityZone.Surface))
                {
                    continue;
                }

                positions.Add(speaker.Position);

                if (!speakersByRoom.TryGetValue(room, out List<Vector3> inRoom))
                {
                    inRoom = new List<Vector3>();
                    speakersByRoom.Add(room, inRoom);
                }

                inRoom.Add(speaker.Position);
            }

            return positions;
        }

        // True when the listener is within reach of a spatial speaker in their room, false when the global speaker should be used.
        private bool IsInSpatialRange(ReferenceHub hub)
        {
            int frame = Time.frameCount;
            if (frame != rangeCacheFrame)
            {
                rangeCache.Clear();
                rangeCacheFrame = frame;
            }

            if (!rangeCache.TryGetValue(hub, out bool inRange))
            {
                inRange = ComputeInRange(hub);
                rangeCache.Add(hub, inRange);
            }

            return inRange;
        }

        private bool ComputeInRange(ReferenceHub hub)
        {
            if (!hub.TryGetCurrentRoom(out RoomIdentifier room)
                || !speakersByRoom.TryGetValue(room, out List<Vector3> positions))
            {
                return false;
            }

            Vector3 listenerPosition = hub.PlayerCameraReference.position;
            float maxDistanceSqr = Config.SpatialSpeakerMaxDistance * Config.SpatialSpeakerMaxDistance;

            foreach (Vector3 position in positions)
            {
                if ((listenerPosition - position).sqrMagnitude < maxDistanceSqr)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
