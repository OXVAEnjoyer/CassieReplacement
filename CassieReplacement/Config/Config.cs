namespace CassieReplacement.Config
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using CassieReplacement.Reader.Models;

    public class CassieConfig
    {
        public bool UseGlobalSpeaker { get; set; } = true;

        public bool UseSpatialSpeakers { get; set; } = false;

        [Description("Also removes spatial speakers from Surface Zone")]
        public bool GlobalForSurfaceOnly { get; set; } = false;

        public float SpatialSpeakerMaxDistance { get; set; } = 40f;

        public float SpatialSpeakerMinDistance { get; set; } = 20f;

        public float SpatialSpeakerVolume { get; set; } = 1f;

        public float GlobalSpeakerVolume { get; set; } = 1.5f;

        public float GlobalSpeakerVolumeMultiplier { get; set; } = 1f;

        [Description("The prefix to use when writing CASSIE messages to allow CASSIE replacer to take over.")]
        public string CustomCassiePrefix { get; set; } = "customcassie";

        [Description("Folders with audio clips (any format SecretLabNAudio can read). Default is LabAPI/configs/CASSIE Replacement. Use {labapi_configs} for the LabAPI configs root, or an absolute path.")]
        public List<CassieDirectorySerializable> BaseDirectories { get; set; } = new List<CassieDirectorySerializable>
        {
            new CassieDirectorySerializable(),
        };

        [Description("Upper limit, in megabytes, for decoded audio kept in memory. When it is reached the cache is emptied and clips are decoded again as needed.")]
        public int MaxCacheMegabytes { get; set; } = 128;

        public Dictionary<string, string> WordsToBasegameOverride { get; set; } = new Dictionary<string, string>();

        [Description("This is the volume of the speaker making CASSIE's words. Please adjust so that words spoken are loud enough to overpower the PA noise, but not so loud it clips or hurts to listen to.")]
        public float CassieVolume { get; set; } = 1f;

        public CassieOverrideConfigs CassieOverrideConfig { get; set; } = new CassieOverrideConfigs();
    }
}
