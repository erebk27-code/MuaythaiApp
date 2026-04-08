using System;
using System.IO;

namespace MuaythaiApp;

public static class RemoteApiSettingsService
{
    private const string ConfigFileName = "api-base-url.txt";
    private const string ApiUrlEnvironmentVariable = "MUAYTHAIAPP_API_URL";

    public static bool IsRemoteApiEnabled()
        => !string.IsNullOrWhiteSpace(GetConfiguredApiBaseUrl());

    public static string? GetConfiguredApiBaseUrl()
    {
        var environmentValue = Environment.GetEnvironmentVariable(ApiUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return NormalizeBaseUrl(environmentValue);

        var configPath = GetConfigPath();
        if (!File.Exists(configPath))
            return null;

        var configuredValue = File.ReadAllText(configPath).Trim();
        return string.IsNullOrWhiteSpace(configuredValue)
            ? null
            : NormalizeBaseUrl(configuredValue);
    }

    public static void SaveApiBaseUrl(string apiBaseUrl)
    {
        var normalizedUrl = NormalizeBaseUrl(apiBaseUrl);
        Directory.CreateDirectory(Path.GetDirectoryName(GetConfigPath())!);
        File.WriteAllText(GetConfigPath(), normalizedUrl);
    }

    public static void ClearApiBaseUrl()
    {
        var configPath = GetConfigPath();
        if (File.Exists(configPath))
            File.Delete(configPath);
    }

    public static string GetConfigurationHint()
        => "Use the same server API address on both computers. Example: http://209.38.240.93";

    private static string GetConfigPath()
        => Path.Combine(AppPaths.GetAppDataDirectory(), ConfigFileName);

    private static string NormalizeBaseUrl(string apiBaseUrl)
    {
        var trimmed = apiBaseUrl.Trim().Trim('"');
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "http://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("API address is not valid.");

        return uri.ToString().TrimEnd('/');
    }
}
