using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;

namespace MuaythaiApp;

public partial class DatabaseSettingsWindow : Window
{
    private readonly Action onSettingsSaved;

    public DatabaseSettingsWindow()
        : this(() => { })
    {
    }

    public DatabaseSettingsWindow(Action onSettingsSaved)
    {
        InitializeComponent();
        this.onSettingsSaved = onSettingsSaved;

        LocalizationService.LanguageChanged += ApplyLocalization;
        Closed += (_, __) => LocalizationService.LanguageChanged -= ApplyLocalization;

        ApiUrlBox.Text = RemoteApiSettingsService.GetConfiguredApiBaseUrl() ?? string.Empty;
        DatabasePathBox.Text = DatabaseLocationService.GetConfiguredDatabasePath() ?? string.Empty;
        UpdateRepoUrlBox.Text = AutoUpdateSettingsService.GetConfiguredRepoUrl() ?? string.Empty;
        ApplyLocalization();
        RefreshCurrentPath();
    }

    private void SaveRemoteApiClick(object? sender, RoutedEventArgs e)
    {
        var apiUrl = ApiUrlBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            StatusText.Text = "Server API address is required.";
            return;
        }

        try
        {
            RemoteApiSettingsService.SaveApiBaseUrl(apiUrl);

            if (!RemoteApiClient.TestConnection(out var errorMessage))
            {
                RemoteApiSettingsService.ClearApiBaseUrl();
                StatusText.Text = $"Could not reach the server API: {errorMessage}";
                return;
            }

            StatusText.Text = "Remote API connection saved.";
            onSettingsSaved();
            RefreshCurrentPath();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Remote API could not be saved: {ex.Message}";
        }
    }

    private void SaveDatabasePathClick(object? sender, RoutedEventArgs e)
    {
        var path = DatabasePathBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText.Text = LocalizationService.T("SharedDatabasePathRequired");
            return;
        }

        if (!path.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = LocalizationService.T("DatabasePathMustEndWithDb");
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(directory))
            {
                StatusText.Text = LocalizationService.T("InvalidDatabasePath");
                return;
            }

            RemoteApiSettingsService.ClearApiBaseUrl();
            DatabaseLocationService.SwitchDatabase(path);
            StatusText.Text = LocalizationService.T("DatabaseSyncSaved");
            onSettingsSaved();
            RefreshCurrentPath();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"{LocalizationService.T("DatabaseSyncSaveFailed")}: {ex.Message}";
        }
    }

    private void UseLocalClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            RemoteApiSettingsService.ClearApiBaseUrl();
            DatabaseLocationService.SwitchDatabase(null);
            ApiUrlBox.Text = string.Empty;
            DatabasePathBox.Text = string.Empty;
            StatusText.Text = LocalizationService.T("UsingLocalDatabase");
            onSettingsSaved();
            RefreshCurrentPath();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"{LocalizationService.T("DatabaseSyncSaveFailed")}: {ex.Message}";
        }
    }

    private void SaveUpdateRepoClick(object? sender, RoutedEventArgs e)
    {
        var repoUrl = UpdateRepoUrlBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            StatusText.Text = "GitHub repository URL is required.";
            return;
        }

        try
        {
            AutoUpdateSettingsService.SaveRepoUrl(repoUrl);
            UpdateRepoUrlBox.Text = AutoUpdateSettingsService.GetConfiguredRepoUrl() ?? repoUrl;
            StatusText.Text = "Auto update source saved.";
            RefreshCurrentPath();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Auto update source could not be saved: {ex.Message}";
        }
    }

    private void ClearUpdateRepoClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            AutoUpdateSettingsService.ClearRepoUrl();
            UpdateRepoUrlBox.Text = string.Empty;
            StatusText.Text = "Auto update source cleared.";
            RefreshCurrentPath();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Auto update source could not be cleared: {ex.Message}";
        }
    }

    private void CancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.T("DatabaseSync");
        TitleText.Text = LocalizationService.T("DatabaseSync");
        HintText.Text = LocalizationService.T("DatabaseSyncHint");
        ApiLabelText.Text = LocalizationService.T("ServerApiAddress");
        ApiUrlBox.Watermark = "http://209.38.240.93";
        PathLabelText.Text = LocalizationService.T("SharedDatabasePath");
        DatabasePathBox.Watermark = @"\\SERVER\Folder\muaythai.db";
        UpdateRepoLabelText.Text = LocalizationService.T("AutoUpdateGithubRepository");
        UpdateRepoUrlBox.Watermark = "https://github.com/your-name/your-repo";
        SaveUpdateRepoButton.Content = LocalizationService.T("SaveUpdateSource");
        ClearUpdateRepoButton.Content = LocalizationService.T("ClearUpdateSource");
        InfoText.Text =
            $"Remote API: {RemoteApiSettingsService.GetConfigurationHint()}{Environment.NewLine}" +
            $"Shared file mode: {DatabaseLocationService.GetConfigurationHint()}{Environment.NewLine}" +
            $"Auto update: {AutoUpdateSettingsService.GetConfigurationHint()}";
        SaveButton.Content = LocalizationService.T("UseRemoteApi");
        SaveDatabaseButton.Content = LocalizationService.T("SaveDatabasePath");
        UseLocalButton.Content = LocalizationService.T("UseLocalDatabase");
        CancelButton.Content = LocalizationService.T("Cancel");
        LocalizationService.LocalizeControlTree(this);
        RefreshCurrentPath();
    }

    private void RefreshCurrentPath()
    {
        var currentSourceText = RemoteApiSettingsService.IsRemoteApiEnabled()
            ? $"Current source: Server API - {RemoteApiSettingsService.GetConfiguredApiBaseUrl()}"
            : $"{LocalizationService.T("CurrentDatabase")}: {DatabaseLocationService.GetCurrentDatabasePath()}";

        var updateInfo = AutoUpdateSettingsService.IsConfigured()
            ? $"Auto update source: {AutoUpdateSettingsService.GetConfiguredRepoUrl()}"
            : "Auto update source: not configured";

        CurrentPathText.Text = $"{currentSourceText}{Environment.NewLine}{updateInfo}";
    }
}
