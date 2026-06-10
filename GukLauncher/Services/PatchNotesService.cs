using GukLauncher.Models;

namespace GukLauncher.Services;

public class PatchNotesService
{
    private readonly HttpClient _http;

    public PatchNotesService(HttpClient http) => _http = http;

    public async Task<List<PatchNote>> FetchAsync()
    {
        var json = await _http.GetStringAsync(AppConfig.PatchNotesUrl);
        return JsonSerializer.Deserialize<List<PatchNote>>(json) ?? new();
    }
}
