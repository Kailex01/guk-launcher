using System.Net.Http;
using System.Text.Json;
using GukLauncher.Models;

namespace GukLauncher.Services;

public class ManifestService
{
    private readonly HttpClient _http;

    public ManifestService(HttpClient http)
    {
        _http = http;
    }

    public async Task<Manifest> FetchAsync()
    {
        var json = await _http.GetStringAsync(AppConfig.ManifestUrl);
        return JsonSerializer.Deserialize<Manifest>(json)
            ?? throw new Exception("Manifest was empty or invalid.");
    }
}
