using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MuaythaiApp.Database;
using MuaythaiApp.Security;
using System;

namespace MuaythaiApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            StartupLogger.Log("App.OnFrameworkInitializationCompleted started");

            if (!RemoteApiSettingsService.IsRemoteApiEnabled())
            {
                var databaseHelper = new DatabaseHelper();
                databaseHelper.CreateDatabase();
            }
            else
            {
                StartupLogger.Log($"Remote API mode enabled: {RemoteApiSettingsService.GetConfiguredApiBaseUrl()}");
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                StartupLogger.Log("Classic desktop lifetime detected");
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                ShowLoginWindow(desktop);
            }
            else
            {
                StartupLogger.Log($"Unexpected application lifetime: {ApplicationLifetime?.GetType().FullName ?? "null"}");
            }

            base.OnFrameworkInitializationCompleted();
            StartupLogger.Log("App.OnFrameworkInitializationCompleted completed");
        }
        catch (Exception ex)
        {
            StartupLogger.Log(ex, "Fatal exception in App.OnFrameworkInitializationCompleted");
            throw;
        }
    }

    private void ShowLoginWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        StartupLogger.Log("ShowLoginWindow called");
        var loginWindow = new LoginWindow(
            (role, sourceWindow) =>
            {
                StartupLogger.Log($"Login success callback entered for role {role}");
                AppSession.Start(role);
                ShowMainWindow(desktop);
                sourceWindow.Close();
            },
            () => desktop.Shutdown());

        desktop.MainWindow = loginWindow;
        loginWindow.Show();
        StartupLogger.Log("Login window Show() called");
        Dispatcher.UIThread.Post(loginWindow.Activate, DispatcherPriority.Background);
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        StartupLogger.Log("ShowMainWindow called");
        var mainWindow = new MainWindow(() =>
        {
            var currentWindow = desktop.MainWindow;
            AppSession.End();
            ShowLoginWindow(desktop);
            currentWindow?.Close();
        });

        mainWindow.Closed += (_, __) =>
        {
            if (AppSession.IsLoggedIn)
                desktop.Shutdown();
        };

        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        StartupLogger.Log("Main window Show() called");
        Dispatcher.UIThread.Post(mainWindow.Activate, DispatcherPriority.Background);
    }
}
