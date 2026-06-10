namespace GukLauncher.Models;

public class PatchNote
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("date")]    public string Date    { get; set; } = "";
    [JsonPropertyName("body")]    public string Body    { get; set; } = "";
}
