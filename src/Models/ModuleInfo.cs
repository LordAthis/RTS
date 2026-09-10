using System.Text.Json.Serialization;

namespace RTS.Models
{
    // A modules.json egy bejegyzesenek C# modellje.
    public class ModuleInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("repo")]
        public string Repo { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("entry_point")]
        public string EntryPoint { get; set; } = "";

        [JsonPropertyName("visibility")]
        public string Visibility { get; set; } = "public";

        [JsonPropertyName("tier")]
        public string Tier { get; set; } = "";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }
}
