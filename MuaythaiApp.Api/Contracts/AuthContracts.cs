namespace MuaythaiApp.Api.Contracts;

public sealed record LoginRequest(string Role, string Password);

public sealed record LoginResponse(bool Success, string Role, string ErrorMessage);
