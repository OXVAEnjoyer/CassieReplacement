namespace CassieReplacement.Reader.Models
{
    public class CassieDirectorySerializable
    {
        public string Path { get; set; } = "{labapi_configs}/CASSIE Replacement";

        public string Prefix { get; set; } = string.Empty;

        public float BleedTime { get; set; } = 0f;

        public bool ShouldList { get; set; } = false;
    }
}
