using Avalonia.Controls;
using Avalonia.Interactivity;
using MuaythaiApp.Security;

namespace MuaythaiApp;

public partial class ChangePasswordsWindow : Window
{
    private readonly AuthService authService = new();

    private TextBox? CurrentAdminPasswordInput => this.FindControl<TextBox>("CurrentAdminPasswordBox");
    private TextBox? NewAdminPasswordInput => this.FindControl<TextBox>("NewAdminPasswordBox");
    private TextBox? ConfirmAdminPasswordInput => this.FindControl<TextBox>("ConfirmAdminPasswordBox");
    private TextBox? NewUserPasswordInput => this.FindControl<TextBox>("NewUserPasswordBox");
    private TextBox? ConfirmUserPasswordInput => this.FindControl<TextBox>("ConfirmUserPasswordBox");

    public ChangePasswordsWindow()
    {
        InitializeComponent();

        if (!AppSession.IsAdmin)
        {
            Opened += (_, __) => Close();
        }
    }

    private void SaveClick(object? sender, RoutedEventArgs e)
    {
        if (!authService.TryUpdatePasswords(
                CurrentAdminPasswordInput?.Text ?? string.Empty,
                NewAdminPasswordInput?.Text,
                ConfirmAdminPasswordInput?.Text,
                NewUserPasswordInput?.Text,
                ConfirmUserPasswordInput?.Text,
                out var errorMessage))
        {
            StatusText.Text = errorMessage;
            StatusText.Foreground = Avalonia.Media.Brushes.Firebrick;
            return;
        }

        StatusText.Text = "Passwords saved successfully.";
        StatusText.Foreground = Avalonia.Media.Brushes.DarkGreen;
        if (CurrentAdminPasswordInput != null) CurrentAdminPasswordInput.Text = string.Empty;
        if (NewAdminPasswordInput != null) NewAdminPasswordInput.Text = string.Empty;
        if (ConfirmAdminPasswordInput != null) ConfirmAdminPasswordInput.Text = string.Empty;
        if (NewUserPasswordInput != null) NewUserPasswordInput.Text = string.Empty;
        if (ConfirmUserPasswordInput != null) ConfirmUserPasswordInput.Text = string.Empty;
    }
}
