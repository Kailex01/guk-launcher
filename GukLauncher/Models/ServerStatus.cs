namespace GukLauncher.Models;

public class ServerStatus
{
    [JsonPropertyName("online")]         public bool Online        { get; set; }
    [JsonPropertyName("players_online")] public int  PlayersOnline { get; set; }
}
