using System.Text.Json.Serialization;

namespace GukLauncher.Models;

public class Manifest
{
    [JsonPropertyName("generated")]  public string             Generated { get; set; } = "";
    [JsonPropertyName("file_count")] public int                FileCount { get; set; }
    [JsonPropertyName("files")]      public List<ManifestFile> Files     { get; set; } = new();
}
