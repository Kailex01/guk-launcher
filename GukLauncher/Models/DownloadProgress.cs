namespace GukLauncher.Models;

public class DownloadProgress
{
    public int    FilesCompleted   { get; init; }
    public int    FilesTotal       { get; init; }
    public long   BytesDownloaded  { get; init; }
    public long   BytesTotal       { get; init; }
    public string CurrentFile      { get; init; } = "";

    public double Percentage =>
        BytesTotal > 0 ? (double)BytesDownloaded / BytesTotal * 100 : 0;

    public string BytesDownloadedMb =>
        $"{BytesDownloaded / 1024.0 / 1024.0:F1} MB";

    public string BytesTotalMb =>
        $"{BytesTotal / 1024.0 / 1024.0:F1} MB";
}
