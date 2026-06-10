using System.Security.Cryptography;

namespace GukLauncher.Services;

public static class FileHasher
{
    public static async Task<string> ComputeAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await Task.Run(() => sha256.ComputeHash(stream));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
