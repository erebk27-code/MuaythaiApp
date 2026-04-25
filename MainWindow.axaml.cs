using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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
        SetButtonText(RingInfoButton, LocalizationService.T("RingInformation"));
        SetButtonText(CategoryInfoButton, LocalizationService.T("CategoryInformation"));
        SetButtonText(ClubInfoButton, LocalizationService.T("ClubInformation"));
        SetButtonText(AthleteInfoButton, LocalizationService.T("AthleteInformation"));
        SetButtonText(AthleteControlButton, LocalizationService.T("AthleteControl"));
        DefinitionsHeaderText.Text = LocalizationService.T("Definitions");
        ChampionshipProcessHeaderText.Text = LocalizationService.T("ChampionshipProcess");
        ReportsHeaderText.Text = LocalizationService.T("Reports");
        SetButtonText(ChampionshipInfoButton, LocalizationService.T("ChampionshipInformationEntry"));
        SetButtonText(AthleteScaleButton, LocalizationService.T("AthleteScale"));
        SetButtonText(GenderControlButton, LocalizationService.T("GenderControl"));
        SetButtonText(ScaleControlButton, LocalizationService.T("ScaleControl"));
        SetButtonText(MatchesButton, LocalizationService.T("Matches"));
        SetButtonText(FightResultsButton, LocalizationService.T("FightResults"));
        SetButtonText(MedalTableButton, LocalizationService.T("MedalTable"));
        SetButtonText(ReportsButton, LocalizationService.T("Reports"));
        SetButtonText(DatabaseSettingsButton, LocalizationService.T("DatabaseSync"));
        SetButtonText(ChangePasswordsButton, LocalizationService.T("ChangePasswords"));
        SetButtonText(LogoutButton, LocalizationService.T("Logout"));
        SetButtonText(CheckUpdatesButton, LocalizationService.T("CheckForUpdates"));
        SessionInfoText.Text = AppSession.IsAdmin
            ? LocalizationService.T("LoggedInAsAdministrator")
            : LocalizationService.T("LoggedInAsUser");
        AccessInfoText.Text = AppSession.IsAdmin
            ? LocalizationService.T("FullAccessEnabled")
            : LocalizationService.T("LimitedAccessEnabled");
        ChangePasswordsButton.IsVisible = AppSession.IsAdmin;
        MatchesButton.IsEnabled = AppSession.IsAdmin;
        RingInfoButton.IsEnabled = AppSession.IsAdmin;
        ClubInfoButton.IsEnabled = AppSession.IsAdmin;
        AthleteInfoButton.IsEnabled = AppSession.IsAdmin;
        AthleteControlButton.IsEnabled = AppSession.IsAdmin;
        RefreshDatabaseStatus();
        RefreshUpdateStatus();
        LocalizationService.LocalizeControlTree(this);

        isUpdatingLanguageCombo = false;
    }

    private static void SetButtonText(Button button, string text)
    {
        button.Content = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
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

        try
        {
            StartupLogger.Log("FightersClick started");
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    StartupLogger.Log("FightersClick show dispatch started");
                    var w = new FightersWindow();
                    w.Show();
                    w.Activate();
                    StartupLogger.Log("FightersClick window shown");
                }
                catch (Exception ex)
                {
                    AccessInfoText.Text = $"Athlete Information could not be opened: {ex.Message}";
                    StartupLogger.Log(ex, "FightersClick show dispatch failed");
                }
            }, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            AccessInfoText.Text = $"Athlete Information could not be opened: {ex.Message}";
            StartupLogger.Log(ex, "FightersClick failed");
        }
    }

    private void RingInfoClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            StartupLogger.Log("RingInfoClick started");
            var window = new ChampionshipSettingsWindow(
                RefreshDatabaseStatus,
                showInformationSection: false,
                showRingSection: true);
            window.Show();
            StartupLogger.Log("RingInfoClick window shown");
        }
        catch (Exception ex)
        {
            AccessInfoText.Text = $"Ring Information could not be opened: {ex.Message}";
            StartupLogger.Log(ex, "RingInfoClick failed");
        }
    }

    private void ClubsClick(object? sender, RoutedEventArgs e)
    {
        if (!AppSession.IsAdmin)
        {
            AccessInfoText.Text = LocalizationService.T("LimitedAccessEnabled");
            return;
        }

        try
        {
            StartupLogger.Log("ClubsClick started");
            var w = new ClubsWindow();
            w.Show();
            StartupLogger.Log("ClubsClick window shown");
        }
        catch (Exception ex)
        {
            AccessInfoText.Text = $"Club Information could not be opened: {ex.Message}";
            StartupLogger.Log(ex, "ClubsClick failed");
        }
    }

    private void CategoriesClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            StartupLogger.Log("CategoriesClick started");
            var w = new CategoryWindow();
            w.Show();
            StartupLogger.Log("CategoriesClick window shown");
        }
        catch (Exception ex)
        {
            AccessInfoText.Text = $"Category Information could not be opened: {ex.Message}";
            StartupLogger.Log(ex, "CategoriesClick failed");
        }
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

    private void ChampionshipSettingsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            StartupLogger.Log("ChampionshipSettingsClick started");
            var window = new ChampionshipSettingsWindow(
                RefreshDatabaseStatus,
                showInformationSection: true,
                showRingSection: false);
            window.Show();
            StartupLogger.Log("ChampionshipSettingsClick window shown");
        }
        catch (Exception ex)
        {
            AccessInfoText.Text = $"Championship settings could not be opened: {ex.Message}";
            StartupLogger.Log(ex, "ChampionshipSettingsClick failed");
        }
    }

    private void ChampionshipProcessClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            StartupLogger.Log("ChampionshipProcessClick started");
            var viewMode = sender switch
            {
                Button { Name: nameof(AthleteScaleButton) } => ChampionshipProcessViewMode.AthleteScale,
                Button { Name: nameof(GenderControlButton) } => ChampionshipProcessViewMode.GenderControl,
                Button { Name: nameof(ScaleControlButton) } => ChampionshipProcessViewMode.ScaleControl,
                _ => ChampionshipProcessViewMode.AthleteControl
            };
            var window = new ChampionshipProcessWindow(viewMode);
            window.Show();
            StartupLogger.Log("ChampionshipProcess window shown");
        }
        catch (Exception ex)
        {
            AccessInfoText.Text = $"Athlete Control could not be opened: {ex.Message}";
            StartupLogger.Log(ex, "ChampionshipProcessClick failed");
        }
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
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CheckUpdatesButton.IsEnabled = false;
            UpdateStatusText.Text = "Checking for updates...";
        });

        var message = await AutoUpdateService.CheckForUpdatesAndMaybeApplyAsync(progress =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateStatusText.Text = $"Downloading update... {progress}%";
            });
        });

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            UpdateStatusText.Text = message;
            CheckUpdatesButton.IsEnabled = AutoUpdateSettingsService.IsAutoUpdateSupported();
        });

        if (!isAutomatic)
            StartupLogger.Log($"Manual update check result: {message}");
    }
}
