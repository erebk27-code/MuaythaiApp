using Avalonia.Controls;
using Avalonia.Interactivity;
using MuaythaiApp.Security;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MuaythaiApp;

public partial class LoginWindow : Window
{
    private readonly AuthService authService = new();
    private readonly Action<AppRole, LoginWindow> onLoginSuccess;
    private readonly Action onCancelled;
    private bool loginSucceeded;

    public LoginWindow()
        : this((_, _) => { }, () => { })
    {
    }

    public LoginWindow(Action<AppRole, LoginWindow> onLoginSuccess, Action onCancelled)
    {
        StartupLogger.Log("LoginWindow constructor started");
        InitializeComponent();
        StartupLogger.Log("LoginWindow InitializeComponent completed");

        this.onLoginSuccess = onLoginSuccess;
        this.onCancelled = onCancelled;

        RoleCombo.ItemsSource = new List<RoleOption>
        {
            new("Administrator", AppRole.Admin),
            new("User", AppRole.User)
        };
        RoleCombo.SelectedIndex = 0;
        ConnectionModeText.Text = RemoteApiSettingsService.IsRemoteApiEnabled()
            ? $"Connected to server API: {RemoteApiSettingsService.GetConfiguredApiBaseUrl()}"
            : $"Using local database: {AppPaths.GetDatabasePath()}";
        StartupLogger.Log("LoginWindow controls initialized");

        Opened += (_, __) =>
        {
            WindowState = WindowState.Normal;
            Activate();
        };

        Closed += (_, __) =>
        {
            if (!loginSucceeded)
                this.onCancelled();
        };

        UpdateDefaultPasswordInfo();
        StartupLogger.Log("LoginWindow constructor completed");
    }

    private void RoleChanged(object? sender, SelectionChangedEventArgs e)
    {
        StatusText.Text = string.Empty;
        UpdateDefaultPasswordInfo();
    }

    private async void LoginClick(object? sender, RoutedEventArgs e)
    {
        var role = GetSelectedRole();
        var password = this.FindControl<TextBox>("PasswordBox")?.Text ?? string.Empty;
        var loginButton = this.FindControl<Button>("LoginButton");

        try
        {
            StartupLogger.Log($"LoginClick started for role {role}");
            IsEnabled = false;
            if (loginButton is not null)
                loginButton.IsEnabled = false;

            StatusText.Text = RemoteApiClient.IsEnabled
                ? "Signing in to server..."
                : string.Empty;

            var (success, errorMessage) = await authService.TryLoginAsync(role, password);
            if (!success)
            {
                StatusText.Text = errorMessage;
                StartupLogger.Log($"LoginClick failed for role {role}: {errorMessage}");
                return;
            }

            StatusText.Text = string.Empty;
            loginSucceeded = true;
            StartupLogger.Log($"LoginClick succeeded for role {role}");
            onLoginSuccess(role, this);
        }
        catch (Exception ex)
        {
            loginSucceeded = false;
            StatusText.Text = $"Login failed: {ex.Message}";
            StartupLogger.Log(ex, "LoginClick failed after authentication");
        }
        finally
        {
            if (!loginSucceeded)
            {
                IsEnabled = true;
                if (loginButton is not null)
                    loginButton.IsEnabled = true;
            }
        }
    }

    private void UpdateDefaultPasswordInfo()
    {
        if (RemoteApiClient.IsEnabled)
        {
            DefaultPasswordText.Text = "Passwords are validated by the server API in remote mode.";
            return;
        }

        var role = GetSelectedRole();

        if (!authService.UsesDefaultPassword(role))
        {
            DefaultPasswordText.Text = role == AppRole.Admin
                ? "Administrator password has already been customized."
                : "User password has already been customized.";
            return;
        }

        DefaultPasswordText.Text = role == AppRole.Admin
            ? $"Default administrator password: {AuthService.DefaultAdminPassword}"
            : $"Default user password: {AuthService.DefaultUserPassword}";
    }

    private AppRole GetSelectedRole()
    {
        return (RoleCombo.SelectedItem as RoleOption)?.Role ?? AppRole.Admin;
    }

    private sealed record RoleOption(string Label, AppRole Role)
    {
        public override string ToString() => Label;
    }
}
