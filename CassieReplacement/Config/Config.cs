namespace CassieReplacement.Config
{
    using CassieReplacement.Reader.Models;
    using System.Collections.Generic;
    using System.ComponentModel;
#if EXILED
    using Exiled.API.Interfaces;

    public class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;

        public bool Debug { get; set; } = false;

#else

    public class Config
    {
#endif
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

        [Description("Folders with .ogg clips. Default is LabAPI/configs/CASSIE Replacement. Use {labapi_configs} for the LabAPI configs root, or an absolute path.")]
        public List<CassieDirectorySerializable> BaseDirectories { get; set; } = new List<CassieDirectorySerializable>
        {
            new CassieDirectorySerializable(),
        };

        public Dictionary<string, string> WordsToBasegameOverride { get; set; } = new Dictionary<string, string>();

        [Description("This is the volume of the speaker making CASSIE's words. Please adjust so that words spoken are loud enough to overpower the PA noise, but not so loud it clips or hurts to listen to.")]
        public float CassieVolume { get; set; } = 1f;

        public CassieOverrideConfigs CassieOverrideConfig { get; set; } = new CassieOverrideConfigs();
    }
}
