namespace GukLauncher.Models;

public class PatchJob
{
    public string RelativePath   { get; set; } = "";
    public string DownloadUrl    { get; set; } = "";
    public long   Size           { get; set; }
    public string ExpectedSha256 { get; set; } = "";
    public bool   IsNewFile      { get; set; }
}
