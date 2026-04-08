using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace MuaythaiApp;

public static class AutoUpdateService
{
    public static async Task<string> CheckForUpdatesAndMaybeApplyAsync(Action<int>? progress = null)
    {
        if (!AutoUpdateSettingsService.IsAutoUpdateSupported())
            return "Automatic updates are currently enabled only on Windows.";

        var repoUrl = AutoUpdateSettingsService.GetConfiguredRepoUrl();
        if (string.IsNullOrWhiteSpace(repoUrl))
            return "Auto update is not configured.";

        try
        {
            StartupLogger.Log($"Checking for updates from {repoUrl}");

            var manager = new UpdateManager(new GithubSource(repoUrl, null, false));
            var updateInfo = await manager.CheckForUpdatesAsync();

            if (updateInfo == null)
            {
                StartupLogger.Log("No updates available");
                return "You are using the latest version.";
            }

            StartupLogger.Log($"Update found: {updateInfo.TargetFullRelease.Version}");
            await manager.DownloadUpdatesAsync(updateInfo, progress);
            StartupLogger.Log("Update downloaded successfully; applying restart");
            manager.ApplyUpdatesAndRestart(updateInfo);
            return $"Update {updateInfo.TargetFullRelease.Version} downloaded. Restarting...";
        }
        catch (TaskCanceledException)
        {
            StartupLogger.Log("Auto update check timed out");
            return "Update check timed out.";
        }
        catch (Exception ex)
        {
            StartupLogger.Log(ex, "Auto update check failed");
            return $"Update check failed: {ex.Message}";
        }
    }
}
