using GukLauncher.Models;

namespace GukLauncher.Services;

public class ServerStatusService
{
    private readonly HttpClient _http;

    public ServerStatusService(HttpClient http) => _http = http;

    public async Task<ServerStatus?> FetchAsync()
    {
        var json = await _http.GetStringAsync(AppConfig.ServerStatusUrl);
        return JsonSerializer.Deserialize<ServerStatus>(json);
    }
}
