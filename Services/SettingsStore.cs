using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinguaLoop.Domain;

namespace LinguaLoop.Services;

public sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore()
    {
        var dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LinguaLoop");
        Directory.CreateDirectory(dataFolder);
        _path = Path.Combine(dataFolder, "settings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_path)) return new AppSettings();
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream) ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, settings, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string Protect(string apiKey)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows DPAPI is required to store an API key.");
        return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey), null, DataProtectionScope.CurrentUser));
    }

    public static string? Unprotect(string? protectedApiKey)
    {
        if (string.IsNullOrWhiteSpace(protectedApiKey) || !OperatingSystem.IsWindows()) return null;
        try
        {
            var plain = ProtectedData.Unprotect(Convert.FromBase64String(protectedApiKey), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
