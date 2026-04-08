using MuaythaiApp.Database;
using System;
using System.IO;

namespace MuaythaiApp;

public static class DatabaseLocationService
{
    private const string ConfigFileName = "database-path.txt";
    private const string DatabasePathEnvironmentVariable = "MUAYTHAIAPP_DB_PATH";

    public static string GetCurrentDatabasePath() => AppPaths.GetDatabasePath();

    public static string? GetConfiguredDatabasePath()
    {
        var environmentValue = Environment.GetEnvironmentVariable(DatabasePathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return NormalizePath(environmentValue);

        var configPath = GetConfigPath();
        if (!File.Exists(configPath))
            return null;

        var configuredPath = File.ReadAllText(configPath).Trim();
        return string.IsNullOrWhiteSpace(configuredPath)
            ? null
            : NormalizePath(configuredPath);
    }

    public static string GetConfigurationHint()
        => $"Use the same shared network path on both computers. Example: \\\\SERVER\\Muaythai\\muaythai.db";

    public static void SwitchDatabase(string? configuredPath)
    {
        var currentPath = AppPaths.GetDatabasePath();
        var normalizedTarget = string.IsNullOrWhiteSpace(configuredPath)
            ? AppPaths.GetAppDataDatabasePath()
            : NormalizePath(configuredPath);

        SaveConfiguredDatabasePath(configuredPath);

        if (!string.Equals(currentPath, normalizedTarget, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(currentPath) &&
            !File.Exists(normalizedTarget))
        {
            var targetDirectory = Path.GetDirectoryName(normalizedTarget);

            if (!string.IsNullOrWhiteSpace(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            File.Copy(currentPath, normalizedTarget, overwrite: false);
        }

        var helper = new DatabaseHelper();
        helper.CreateDatabase();
    }

    private static void SaveConfiguredDatabasePath(string? configuredPath)
    {
        var configPath = GetConfigPath();

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            if (File.Exists(configPath))
                File.Delete(configPath);

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, NormalizePath(configuredPath));
    }

    private static string GetConfigPath()
        => Path.Combine(AppPaths.GetAppDataDirectory(), ConfigFileName);

    private static string NormalizePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        return Path.GetFullPath(expanded);
    }
}
