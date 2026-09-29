namespace CassieReplacement.Audio
{
    using System;
    using System.Collections.Generic;
    using CassieReplacement.Config;
    using LabApi.Features.Console;
    using MapGeneration;
    using PlayerRoles.PlayableScps.Scp079;
    using SecretLabNAudio.Core;
    using SecretLabNAudio.Core.Extensions;
    using UnityEngine;

    /// <summary>
    /// REFACTOR (SRP): cała logika głośników wyciągnięta z Plugin.cs.
    /// Właściciel wszystkich AudioPlayerów CASSIE.
    /// </summary>
    public sealed class SpeakerManager : IDisposable
    {
        private readonly Plugin plugin;

        private readonly List<AudioPlayer> players = new List<AudioPlayer>();

        // OPTYMALIZACJA: pokój -> pozycje głośników liczone RAZ przy budowie (zamiast LINQ + Room.Get w filtrze wywoływanym per pakiet audio).
        private readonly Dictionary<RoomIdentifier, List<Vector3>> speakersByRoom = new Dictionary<RoomIdentifier, List<Vector3>>();

        // OPTYMALIZACJA: wynik "czy gracz jest w zasięgu głośników przestrzennych" cache'owany na klatkę.
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
                CreateGlobalPlayer(Config.GlobalSpeakerVolume);
            }

            if (Config.UseSpatialSpeakers)
            {
                CreateSpatialPlayers();
            }
        }

        /// <summary>Gwarantuje, że istnieje przynajmniej jeden głośnik (dawniej EnsureSpeakers).</summary>
        public void EnsureReady()
        {
            if (players.Count > 0)
            {
                return;
            }

            Rebuild();
            if (players.Count == 0)
            {
                // Konfiguracja bez żadnego głośnika = CASSIE niema. Awaryjnie globalny.
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
                    Logger.Debug($"[CassieReplacement] StopAll failed (player destroyed?): {ex.Message}");
                }
            }
        }

        public void Destroy()
        {
            foreach (AudioPlayer player in players)
            {
                // FIX: obiekt mógł już zostać zniszczony przez grę przy restarcie rundy - nie wolno przerwać sprzątania.
                try
                {
                    player?.Destroy();
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[CassieReplacement] AudioPlayer.Destroy failed (already destroyed?): {ex.Message}");
                }
            }

            players.Clear();
            speakersByRoom.Clear();
            rangeCache.Clear();
            GlobalPlayer = null;
            SpatialPlayer = null;
        }

        public void Dispose() => Destroy();

        private void CreateGlobalPlayer(float volume, bool filtered = true)
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
            List<Vector3> all = new List<Vector3>();

            // FIX: poprzednio lazy IEnumerable po żywej kolekcji, enumerowane ponownie przy KAŻDYM wywołaniu filtra.
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

                all.Add(speaker.Position);

                if (!speakersByRoom.TryGetValue(room, out List<Vector3> inRoom))
                {
                    inRoom = new List<Vector3>(2);
                    speakersByRoom.Add(room, inRoom);
                }

                inRoom.Add(speaker.Position);
            }

            return all;
        }

        private static bool IsValidListener(LabApi.Features.Wrappers.Player player)
        {
            return player != null && !player.IsHost && player.ReferenceHub != null;
        }

        /// <summary>
        /// true = gracz słyszy głośniki przestrzenne, false = słyszy głośnik globalny.
        /// FIX: filtr globalny i przestrzenny korzystają z JEDNEJ funkcji (wcześniej przestrzenny zależał od
        /// listy wypełnianej jako efekt uboczny filtra globalnego - zależność od kolejności wywołań).
        /// FIX: poprzednia logika 'Any(dist >= max)' powodowała podwójne granie (global + spatial) gdy w pokoju były 2 głośniki.
        /// </summary>
        private bool IsInSpatialRange(ReferenceHub hub)
        {
            int frame = Time.frameCount;
            if (frame != rangeCacheFrame)
            {
                rangeCache.Clear();
                rangeCacheFrame = frame;
            }

            if (rangeCache.TryGetValue(hub, out bool cached))
            {
                return cached;
            }

            bool inRange = ComputeInRange(hub);
            rangeCache[hub] = inRange;
            return inRange;
        }

        private bool ComputeInRange(ReferenceHub hub)
        {
            if (!hub.TryGetCurrentRoom(out RoomIdentifier room)
                || !speakersByRoom.TryGetValue(room, out List<Vector3> positions))
            {
                return false;
            }

            Vector3 cameraPosition = hub.PlayerCameraReference.position;
            float maxDistanceSqr = Config.SpatialSpeakerMaxDistance * Config.SpatialSpeakerMaxDistance;

            for (int i = 0; i < positions.Count; i++)
            {
                if ((cameraPosition - positions[i]).sqrMagnitude < maxDistanceSqr)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
