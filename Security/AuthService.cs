using Microsoft.Data.Sqlite;
using MuaythaiApp.Database;
using System;
using System.Threading.Tasks;

namespace MuaythaiApp.Security;

public class AuthService
{
    public const string DefaultAdminPassword = "Admin123!";
    public const string DefaultUserPassword = "User123!";

    private const string AdminPasswordKey = "AdminPasswordHash";
    private const string UserPasswordKey = "UserPasswordHash";

    public bool TryLogin(AppRole role, string password, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "Password is required.";
            return false;
        }

        if (RemoteApiClient.IsEnabled)
            return RemoteApiClient.TryLogin(role, password, out errorMessage);

        var storedHash = GetPasswordHash(role);

        if (string.IsNullOrWhiteSpace(storedHash) || !PasswordHasher.Verify(password, storedHash))
        {
            errorMessage = "Invalid password.";
            return false;
        }

        return true;
    }

    public async Task<(bool Success, string ErrorMessage)> TryLoginAsync(AppRole role, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Password is required.");

        if (RemoteApiClient.IsEnabled)
            return await RemoteApiClient.TryLoginAsync(role, password);

        var storedHash = GetPasswordHash(role);

        if (string.IsNullOrWhiteSpace(storedHash) || !PasswordHasher.Verify(password, storedHash))
            return (false, "Invalid password.");

        return (true, string.Empty);
    }

    public bool UsesDefaultPassword(AppRole role)
    {
        if (RemoteApiClient.IsEnabled)
            return RemoteApiClient.UsesDefaultPassword(role);

        var storedHash = GetPasswordHash(role);
        var defaultHash = PasswordHasher.Hash(role == AppRole.Admin
            ? DefaultAdminPassword
            : DefaultUserPassword);

        return string.Equals(storedHash, defaultHash, StringComparison.OrdinalIgnoreCase);
    }

    public bool TryUpdatePasswords(
        string currentAdminPassword,
        string? newAdminPassword,
        string? confirmAdminPassword,
        string? newUserPassword,
        string? confirmUserPassword,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        if (RemoteApiClient.IsEnabled)
        {
            errorMessage = "Password change over the server API is not available yet.";
            return false;
        }

        if (!TryLogin(AppRole.Admin, currentAdminPassword, out _))
        {
            errorMessage = "Current administrator password is incorrect.";
            return false;
        }

        var wantsAdminUpdate = !string.IsNullOrWhiteSpace(newAdminPassword) ||
            !string.IsNullOrWhiteSpace(confirmAdminPassword);
        var wantsUserUpdate = !string.IsNullOrWhiteSpace(newUserPassword) ||
            !string.IsNullOrWhiteSpace(confirmUserPassword);

        if (!wantsAdminUpdate && !wantsUserUpdate)
        {
            errorMessage = "Enter at least one new password.";
            return false;
        }

        if (wantsAdminUpdate)
        {
            if (!ValidateNewPassword(newAdminPassword, confirmAdminPassword, "administrator", out errorMessage))
                return false;
        }

        if (wantsUserUpdate)
        {
            if (!ValidateNewPassword(newUserPassword, confirmUserPassword, "user", out errorMessage))
                return false;
        }

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        if (wantsAdminUpdate)
            SetSetting(connection, AdminPasswordKey, PasswordHasher.Hash(newAdminPassword!));

        if (wantsUserUpdate)
            SetSetting(connection, UserPasswordKey, PasswordHasher.Hash(newUserPassword!));

        return true;
    }

    private static bool ValidateNewPassword(
        string? newPassword,
        string? confirmPassword,
        string label,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            errorMessage = $"New {label} password is required.";
            return false;
        }

        if (newPassword.Length < 6)
        {
            errorMessage = $"{label[..1].ToUpperInvariant()}{label[1..]} password must be at least 6 characters.";
            return false;
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            errorMessage = $"{label[..1].ToUpperInvariant()}{label[1..]} passwords do not match.";
            return false;
        }

        return true;
    }

    private string GetPasswordHash(AppRole role)
    {
        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT Value
        FROM AppSettings
        WHERE Key = @key
        ";
        command.Parameters.AddWithValue("@key", role == AppRole.Admin ? AdminPasswordKey : UserPasswordKey);

        return command.ExecuteScalar()?.ToString() ?? string.Empty;
    }

    private static void SetSetting(SqliteConnection connection, string key, string value)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        @"
        INSERT INTO AppSettings (Key, Value)
        VALUES (@key, @value)
        ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value
        ";
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.ExecuteNonQuery();
    }
}
