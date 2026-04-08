using System;
using System.Collections.Generic;
using System.IO;

namespace MuaythaiApp;

public static class AppPaths
{
    private const string AppFolderName = "MuaythaiApp";
    private const string DatabaseFileName = "muaythai.db";

    public static string GetDatabasePath()
    {
        var configuredDatabasePath = DatabaseLocationService.GetConfiguredDatabasePath();
        if (!string.IsNullOrWhiteSpace(configuredDatabasePath))
        {
            var configuredDirectory = Path.GetDirectoryName(configuredDatabasePath);

            if (!string.IsNullOrWhiteSpace(configuredDirectory))
                Directory.CreateDirectory(configuredDirectory);

            return configuredDatabasePath;
        }

        var projectDatabasePath = TryFindProjectDatabasePath();
        if (!string.IsNullOrWhiteSpace(projectDatabasePath) && File.Exists(projectDatabasePath))
            return projectDatabasePath;

        var appDataDatabasePath = GetAppDataDatabasePath();
        if (File.Exists(appDataDatabasePath))
            return appDataDatabasePath;

        foreach (var legacyPath in GetLegacyDatabaseCandidates())
        {
            if (!File.Exists(legacyPath))
                continue;

            File.Copy(legacyPath, appDataDatabasePath, overwrite: false);
            return appDataDatabasePath;
        }

        return appDataDatabasePath;
    }

    public static string GetAppDataDatabasePath()
    {
        var appDataDirectory = GetAppDataDirectory();
        Directory.CreateDirectory(appDataDirectory);
        return Path.Combine(appDataDirectory, DatabaseFileName);
    }

    public static string GetReportsDirectory()
    {
        var directory = Path.Combine(GetAppDataDirectory(), "Reports");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string GetLogsDirectory()
    {
        var directory = Path.Combine(GetAppDataDirectory(), "Logs");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string GetAppDataDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, AppFolderName);
    }

    private static string? TryFindProjectDatabasePath()
    {
        foreach (var searchRoot in GetSearchRoots())
        {
            var directory = new DirectoryInfo(Path.GetFullPath(searchRoot));

            while (directory != null)
            {
                var projectFile = Path.Combine(directory.FullName, "MuaythaiApp.csproj");
                var databaseFile = Path.Combine(directory.FullName, DatabaseFileName);

                if (File.Exists(projectFile))
                    return databaseFile;

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetLegacyDatabaseCandidates()
    {
        foreach (var searchRoot in GetSearchRoots())
            yield return Path.Combine(searchRoot, DatabaseFileName);

        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", DatabaseFileName));
    }

    public static IEnumerable<string> GetExistingLegacyDatabasePaths()
    {
        var appDataDatabasePath = GetAppDataDatabasePath();

        foreach (var path in GetLegacyDatabaseCandidates())
        {
            var fullPath = Path.GetFullPath(path);

            if (string.Equals(fullPath, appDataDatabasePath, StringComparison.OrdinalIgnoreCase))
                continue;

            if (File.Exists(fullPath))
                yield return fullPath;
        }
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        yield return Environment.CurrentDirectory;
        yield return AppContext.BaseDirectory;
    }
}
