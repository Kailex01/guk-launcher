using System.Reflection;
using GukLauncher.Models;

namespace GukLauncher.Services;

public class UpdateService
{
    private readonly HttpClient _http;

    public UpdateService(HttpClient http) => _http = http;

    public async Task<UpdateInfo?> CheckAsync()
    {
        var json = await _http.GetStringAsync(AppConfig.VersionCheckUrl)
            .WaitAsync(TimeSpan.FromSeconds(5));

        using var doc  = JsonDocument.Parse(json);
        var root       = doc.RootElement;
        var tagName    = root.GetProperty("tag_name").GetString() ?? "";
        var versionStr = tagName.TrimStart('v');

        if (!Version.TryParse(versionStr, out var parsed))
            return null;

        // Normalize both to major.minor.patch so 1.0.0 == 1.0.0.0
        var remote  = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
        var asm     = Assembly.GetExecutingAssembly().GetName().Version;
        var current = new Version(asm?.Major ?? 1, asm?.Minor ?? 0, Math.Max(asm?.Build ?? 0, 0));

        if (remote <= current)
            return null;

        string exeUrl = "", checksumUrl = "";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            var url  = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (name.Equals("GukLauncher.exe",    StringComparison.OrdinalIgnoreCase)) exeUrl      = url;
            if (name.Equals("GukLauncher.sha256", StringComparison.OrdinalIgnoreCase)) checksumUrl = url;
        }

        if (string.IsNullOrEmpty(exeUrl) || string.IsNullOrEmpty(checksumUrl))
            return null;

        return new UpdateInfo(tagName, exeUrl, checksumUrl);
    }

    public async Task DownloadAndApplyAsync(UpdateInfo update)
    {
        var tempExe    = Path.Combine(Path.GetTempPath(), "GukLauncher_new.exe");
        var batPath    = Path.Combine(Path.GetTempPath(), "GukLauncher_update.bat");
        var currentExe = Environment.ProcessPath!;

        // Fetch expected hash
        var expectedHash = (await _http.GetStringAsync(update.ChecksumUrl)).Trim().ToLowerInvariant();

        // Download new exe
        using (var response = await _http.GetAsync(update.ExeUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            using var net  = await response.Content.ReadAsStreamAsync();
            using var file = File.Create(tempExe);
            await net.CopyToAsync(file);
        }

        // Verify
        var actualHash = await FileHasher.ComputeAsync(tempExe);
        if (actualHash != expectedHash)
        {
            File.Delete(tempExe);
            throw new Exception("Hash mismatch on downloaded update — aborting.");
        }

        // Write swap script: waits for this process to exit, replaces exe, relaunches, deletes itself
        File.WriteAllText(batPath,
            $"""
            @echo off
            timeout /t 2 /nobreak >nul
            move /y "{tempExe}" "{currentExe}"
            start "" "{currentExe}"
            del "%~f0"
            """);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = batPath,
            UseShellExecute = true,
            WindowStyle     = System.Diagnostics.ProcessWindowStyle.Hidden,
        });
    }
}
