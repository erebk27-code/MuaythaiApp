using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MuaythaiApp.Security;
using System;
using System.Threading.Tasks;

namespace MuaythaiApp;

public partial class MainWindow : Window
{
    private readonly Action onLogoutRequested;
    private bool isUpdatingLanguageCombo;

    public MainWindow()
        : this(() => { })
    {
    }

    public MainWindow(Action onLogoutRequested)
    {
        InitializeComponent();
        this.onLogoutRequested = onLogoutRequested;
        Opened += (_, __) =>
        {
            WindowState = WindowState.Normal;
            Activate();
        };
        Opened += async (_, __) => await RunStartupUpdateCheckAsync();
        LanguageCombo.ItemsSource = new[]
        {
            LocalizationService.T("English"),
            LocalizationService.T("Polish")
        };
        LanguageCombo.SelectedIndex = LocalizationService.CurrentLanguage == AppLanguage.Polish ? 1 : 0;
        LocalizationService.LanguageChanged += ApplyLocalization;
        Closed += (_, __) => LocalizationService.LanguageChanged -= ApplyLocalization;
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        isUpdatingLanguageCombo = true;

        Title = LocalizationService.T("MainTitle");
        LanguageLabel.Text = LocalizationService.T("Language");
        var selectedIndex = LocalizationService.CurrentLanguage == AppLanguage.Polish ? 1 : 0;
        LanguageCombo.ItemsSource = new[]
        {
            LocalizationService.T("English"),
            LocalizationService.T("Polish")
        };
        LanguageCombo.SelectedIndex = selectedIndex;
        FightersButton.Content = LocalizationService.T("Fighters");
        ClubsButton.Content = LocalizationService.T("Clubs");
        CategoriesButton.Content = LocalizationService.T("Categories");
        MatchesButton.Content = LocalizationService.T("Matches");
        FightResultsButton.Content = LocalizationService.T("FightResults");
        MedalTableButton.Content = LocalizationService.T("MedalTable");
        ReportsButton.Content = LocalizationService.T("Reports");
        DatabaseSettingsButton.Content = LocalizationService.T("DatabaseSync");
        ChangePasswordsButton.Content = LocalizationService.T("ChangePasswords");
        LogoutButton.Content = LocalizationService.T("Logout");
        CheckUpdatesButton.Content = "Check for Updates";
        SessionInfoText.Text = AppSession.IsAdmin
            ? LocalizationService.T("LoggedInAsAdministrator")
            : LocalizationService.T("LoggedInAsUser");
        AccessInfoText.Text = AppSession.IsAdmin
            ? LocalizationService.T("FullAccessEnabled")
            : LocalizationService.T("LimitedAccessEnabled");
        ChangePasswordsButton.IsVisible = AppSession.IsAdmin;
        FightersButton.IsEnabled = AppSession.IsAdmin;
        ClubsButton.IsEnabled = AppSession.IsAdmin;
        MatchesButton.IsEnabled = AppSession.IsAdmin;
        RefreshDatabaseStatus();
        RefreshUpdateStatus();

        isUpdatingLanguageCombo = false;
    }

    private void LanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingLanguageCombo)
            return;

        LocalizationService.SetLanguage(LanguageCombo.SelectedIndex == 1
            ? AppLanguage.Polish
            : AppLanguage.English);
    }

    private void FightersClick(object? sender, RoutedEventArgs e)
    {
        if (!AppSession.IsAdmin)
        {
            AccessInfoText.Text = LocalizationService.T("LimitedAccessEnabled");
            return;
        }

        var w = new FightersWindow();
        w.Show();
    }

    private void ClubsClick(object? sender, RoutedEventArgs e)
    {
        if (!AppSession.IsAdmin)
        {
            AccessInfoText.Text = LocalizationService.T("LimitedAccessEnabled");
            return;
        }

        var w = new ClubsWindow();
        w.Show();
    }

    private void CategoriesClick(object? sender, RoutedEventArgs e)
    {
        var w = new CategoryWindow();
        w.Show();
    }

    private void MatchesClick(object? sender, RoutedEventArgs e)
    {
        if (!AppSession.IsAdmin)
        {
            AccessInfoText.Text = LocalizationService.T("LimitedAccessEnabled");
            return;
        }

        var w = new MatchesWindow();
        w.Show();
    }

    private void FightResultsClick(object? sender, RoutedEventArgs e)
    {
        var w = new FightResultsWindow();
        w.Show();
    }

    private void MedalTableClick(object? sender, RoutedEventArgs e)
    {
        var w = new MedalTableWindow();
        w.Show();
    }

    private void ReportsClick(object? sender, RoutedEventArgs e)
    {
        var w = new ReportsWindow();
        w.Show();
    }

    private void ChangePasswordsClick(object? sender, RoutedEventArgs e)
    {
        if (!AppSession.IsAdmin)
            return;

        var window = new ChangePasswordsWindow();
        window.Show();
    }

    private void DatabaseSettingsClick(object? sender, RoutedEventArgs e)
    {
        var window = new DatabaseSettingsWindow(() =>
        {
            RefreshDatabaseStatus();
            RefreshUpdateStatus();
        });
        window.Show();
    }

    private void LogoutClick(object? sender, RoutedEventArgs e)
    {
        onLogoutRequested();
    }

    private async void CheckUpdatesClick(object? sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(isAutomatic: false);
    }

    private void RefreshDatabaseStatus()
    {
        if (RemoteApiSettingsService.IsRemoteApiEnabled())
        {
            DatabasePathText.Text = $"Using server API: {RemoteApiSettingsService.GetConfiguredApiBaseUrl()}";
            return;
        }

        var configuredPath = DatabaseLocationService.GetConfiguredDatabasePath();
        var statusKey = string.IsNullOrWhiteSpace(configuredPath)
            ? "UsingLocalDatabaseLabel"
            : "UsingSharedDatabaseLabel";
        DatabasePathText.Text = $"{LocalizationService.T(statusKey)}: {DatabaseLocationService.GetCurrentDatabasePath()}";
    }

    private void RefreshUpdateStatus()
    {
        if (!AutoUpdateSettingsService.IsAutoUpdateSupported())
        {
            UpdateStatusText.Text = "Automatic updates are active only on Windows.";
            CheckUpdatesButton.IsEnabled = false;
            return;
        }

        CheckUpdatesButton.IsEnabled = true;
        UpdateStatusText.Text = AutoUpdateSettingsService.IsConfigured()
            ? $"Auto update source: {AutoUpdateSettingsService.GetConfiguredRepoUrl()}"
            : "Auto update source is not configured.";
    }

    private async Task RunStartupUpdateCheckAsync()
    {
        if (!AutoUpdateSettingsService.IsAutoUpdateSupported() || !AutoUpdateSettingsService.IsConfigured())
            return;

        await CheckForUpdatesAsync(isAutomatic: true);
    }

    private async Task CheckForUpdatesAsync(bool isAutomatic)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking for updates...";

        var message = await AutoUpdateService.CheckForUpdatesAndMaybeApplyAsync(progress =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateStatusText.Text = $"Downloading update... {progress}%";
            });
        });

        UpdateStatusText.Text = message;
        CheckUpdatesButton.IsEnabled = AutoUpdateSettingsService.IsAutoUpdateSupported();

        if (!isAutomatic)
            StartupLogger.Log($"Manual update check result: {message}");
    }
}
