using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.IO;

namespace MuaythaiApp;

public sealed class DatabaseAutoRefresh : IDisposable
{
    private readonly Window window;
    private readonly Action refreshAction;
    private readonly DispatcherTimer timer;
    private string lastToken = string.Empty;
    private bool isRefreshing;

    public DatabaseAutoRefresh(Window window, Action refreshAction, TimeSpan? interval = null)
    {
        this.window = window;
        this.refreshAction = refreshAction;
        timer = new DispatcherTimer
        {
            Interval = interval ?? TimeSpan.FromSeconds(3)
        };

        timer.Tick += OnTimerTick;
        window.Opened += OnOpened;
        window.Activated += OnActivated;
        window.Closed += OnClosed;
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= OnTimerTick;
        window.Opened -= OnOpened;
        window.Activated -= OnActivated;
        window.Closed -= OnClosed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        lastToken = BuildDatabaseToken();
        timer.Start();
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        RefreshIfNeeded(force: true);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Dispose();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!window.IsVisible)
            return;

        RefreshIfNeeded(force: false);
    }

    private void RefreshIfNeeded(bool force)
    {
        if (isRefreshing)
            return;

        var currentToken = BuildDatabaseToken();

        if (!force && string.Equals(currentToken, lastToken, StringComparison.Ordinal))
            return;

        try
        {
            isRefreshing = true;
            refreshAction();
            lastToken = BuildDatabaseToken();
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private static string BuildDatabaseToken()
    {
        if (RemoteApiSettingsService.IsRemoteApiEnabled())
        {
            var apiBaseUrl = RemoteApiSettingsService.GetConfiguredApiBaseUrl() ?? "remote";
            var tickBucket = DateTime.UtcNow.Ticks / TimeSpan.FromSeconds(3).Ticks;
            return $"{apiBaseUrl}:{tickBucket}";
        }

        var databasePath = AppPaths.GetDatabasePath();

        return string.Join("|",
            BuildTokenPart(databasePath),
            BuildTokenPart(databasePath + "-wal"),
            BuildTokenPart(databasePath + "-shm"),
            BuildTokenPart(databasePath + "-journal"));
    }

    private static string BuildTokenPart(string path)
    {
        if (!File.Exists(path))
            return $"{path}:missing";

        var info = new FileInfo(path);
        return $"{path}:{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }
}
