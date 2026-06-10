using System.Net.Http;
using GukLauncher.Models;

namespace GukLauncher.Services;

public class DownloadService
{
    private readonly HttpClient _http;
    private const int MaxRetries = 3;

    public DownloadService(HttpClient http)
    {
        _http = http;
    }

    public async Task DownloadAllAsync(
        List<PatchJob> queue,
        string installDir,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        long totalBytes      = queue.Sum(j => j.Size);
        long bytesDownloaded = 0;
        int  filesCompleted  = 0;

        foreach (var job in queue)
        {
            ct.ThrowIfCancellationRequested();

            progress?.Report(new DownloadProgress
            {
                FilesCompleted  = filesCompleted,
                FilesTotal      = queue.Count,
                BytesDownloaded = bytesDownloaded,
                BytesTotal      = totalBytes,
                CurrentFile     = job.RelativePath,
            });

            var localPath = Path.Combine(installDir,
                job.RelativePath.Replace('/', Path.DirectorySeparatorChar));

            // Ensure the destination folder exists (e.g. Maps\, uifiles\)
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

            await DownloadWithRetryAsync(job, localPath, ct);

            bytesDownloaded += job.Size;
            filesCompleted++;
        }

        // Final progress report at 100%
        progress?.Report(new DownloadProgress
        {
            FilesCompleted  = filesCompleted,
            FilesTotal      = queue.Count,
            BytesDownloaded = bytesDownloaded,
            BytesTotal      = totalBytes,
            CurrentFile     = "",
        });
    }

    private async Task DownloadWithRetryAsync(
        PatchJob job, string localPath, CancellationToken ct)
    {
        var tempPath       = localPath + ".tmp";
        Exception? lastEx  = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                // Stream to a .tmp file so a partial download never corrupts
                // the existing file sitting next to it
                using var response = await _http.GetAsync(
                    job.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct);
                response.EnsureSuccessStatusCode();

                using (var netStream  = await response.Content.ReadAsStreamAsync(ct))
                using (var fileStream = File.Create(tempPath))
                {
                    await netStream.CopyToAsync(fileStream, ct);
                }

                // Verify the downloaded file matches the manifest hash
                var actualHash = await FileHasher.ComputeAsync(tempPath);
                if (actualHash != job.ExpectedSha256)
                    throw new Exception(
                        $"Hash mismatch: {job.RelativePath}\n" +
                        $"  Expected: {job.ExpectedSha256}\n" +
                        $"  Got:      {actualHash}");

                // Atomic-ish replace — move temp over final destination
                File.Move(tempPath, localPath, overwrite: true);
                return;
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                throw;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                if (File.Exists(tempPath)) File.Delete(tempPath);

                if (attempt < MaxRetries)
                {
                    // Exponential backoff: 1s, 2s, 4s
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    await Task.Delay(delay, ct);
                }
            }
        }

        throw new Exception(
            $"Failed to download {job.RelativePath} after {MaxRetries} attempts.",
            lastEx);
    }
}
