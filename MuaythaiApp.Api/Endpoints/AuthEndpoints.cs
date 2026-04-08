using MuaythaiApp.Api.Contracts;
using MuaythaiApp.Api.Data;
using MuaythaiApp.Api.Security;

namespace MuaythaiApp.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", LoginAsync);
        return app;
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, PgConnectionFactory connectionFactory)
    {
        if (string.IsNullOrWhiteSpace(request.Role) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest(new LoginResponse(false, request.Role, "Role and password are required."));

        var key = string.Equals(request.Role, "admin", StringComparison.OrdinalIgnoreCase)
            ? "AdminPasswordHash"
            : "UserPasswordHash";

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_settings WHERE key = @key";
        command.Parameters.AddWithValue("@key", key);

        var storedHash = (await command.ExecuteScalarAsync())?.ToString() ?? string.Empty;
        var isValid = !string.IsNullOrWhiteSpace(storedHash) && PasswordHasher.Verify(request.Password, storedHash);

        return Results.Ok(new LoginResponse(
            isValid,
            request.Role,
            isValid ? string.Empty : "Invalid password."));
    }
}
