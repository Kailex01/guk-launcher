using System.Text.Json.Serialization;

namespace GukLauncher.Models;

public class ManifestFile
{
    [JsonPropertyName("path")]      public string Path      { get; set; } = "";
    [JsonPropertyName("size")]      public long   Size      { get; set; }
    [JsonPropertyName("sha256")]    public string Sha256    { get; set; } = "";
    [JsonPropertyName("overwrite")] public bool   Overwrite { get; set; }
}
