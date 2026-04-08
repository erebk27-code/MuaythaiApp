using System;
using System.IO;

namespace MuaythaiApp;

public static class AutoUpdateSettingsService
{
    private const string ConfigFileName = "update-repo-url.txt";
    private const string RepoUrlEnvironmentVariable = "MUAYTHAIAPP_UPDATE_REPO_URL";

    public static bool IsAutoUpdateSupported()
        => OperatingSystem.IsWindows();

    public static bool IsConfigured()
        => !string.IsNullOrWhiteSpace(GetConfiguredRepoUrl());

    public static string? GetConfiguredRepoUrl()
    {
        var environmentValue = Environment.GetEnvironmentVariable(RepoUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return NormalizeRepoUrl(environmentValue);

        foreach (var path in GetCandidatePaths())
        {
            if (!File.Exists(path))
                continue;

            var configuredValue = File.ReadAllText(path).Trim();
            if (!string.IsNullOrWhiteSpace(configuredValue))
                return NormalizeRepoUrl(configuredValue);
        }

        return null;
    }

    public static void SaveRepoUrl(string repoUrl)
    {
        var normalizedUrl = NormalizeRepoUrl(repoUrl);
        var configPath = GetWritableConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, normalizedUrl);
    }

    public static void ClearRepoUrl()
    {
        var configPath = GetWritableConfigPath();
        if (File.Exists(configPath))
            File.Delete(configPath);
    }

    public static string GetConfigurationHint()
        => "Use a GitHub repository URL such as https://github.com/your-name/your-repo";

    public static string GetBundledConfigPath()
        => Path.Combine(AppContext.BaseDirectory, ConfigFileName);

    private static string GetWritableConfigPath()
        => Path.Combine(AppPaths.GetAppDataDirectory(), ConfigFileName);

    private static string[] GetCandidatePaths()
        => new[]
        {
            GetBundledConfigPath(),
            GetWritableConfigPath()
        };

    private static string NormalizeRepoUrl(string repoUrl)
    {
        var trimmed = repoUrl.Trim().Trim('"').TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Update repository URL is not valid.");

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only GitHub repository URLs are supported.");

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            throw new InvalidOperationException("GitHub repository URL must include owner and repository name.");

        return $"https://github.com/{segments[0]}/{segments[1]}";
    }
}
