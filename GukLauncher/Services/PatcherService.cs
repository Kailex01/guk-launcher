using GukLauncher.Models;

namespace GukLauncher.Services;

public class PatcherService
{
    private readonly ManifestService _manifestService;
    private readonly string _installDir;

    public PatcherService(ManifestService manifestService, string installDir)
    {
        _manifestService = manifestService;
        _installDir      = installDir;
    }

    public async Task<List<PatchJob>> BuildQueueAsync(
        IProgress<string>? progress = null)
    {
        progress?.Report("Fetching manifest...");
        var manifest = await _manifestService.FetchAsync();
        var queue    = new List<PatchJob>();

        progress?.Report("Checking local files against manifest...");

        foreach (var entry in manifest.Files)
        {
            var localPath = Path.Combine(_installDir,
                entry.Path.Replace('/', Path.DirectorySeparatorChar));

            bool needsDownload;

            if (!File.Exists(localPath))
            {
                needsDownload = true;
            }
            else if (!entry.Overwrite)
            {
                // .ini/.opt files — never overwrite existing player settings
                needsDownload = false;
            }
            else
            {
                progress?.Report($"Checking {entry.Path}...");
                var localHash = await FileHasher.ComputeAsync(localPath);
                needsDownload = localHash != entry.Sha256;
            }

            if (needsDownload)
            {
                queue.Add(new PatchJob
                {
                    RelativePath   = entry.Path,
                    DownloadUrl    = $"{AppConfig.PatchBaseUrl}/{Uri.EscapeDataString(entry.Path).Replace("%2F", "/")}",
                    Size           = entry.Size,
                    ExpectedSha256 = entry.Sha256,
                    IsNewFile      = !File.Exists(localPath),
                });
            }
        }

        return queue;
    }

    public long GetTotalDownloadSize(List<PatchJob> queue)
        => queue.Sum(j => j.Size);
}
