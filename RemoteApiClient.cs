using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using MuaythaiApp.Security;

namespace MuaythaiApp;

public static class RemoteApiClient
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool IsEnabled => RemoteApiSettingsService.IsRemoteApiEnabled();

    public static bool TryLogin(AppRole role, string password, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            var response = PostAsync<LoginRequest, LoginResponse>(
                "/api/auth/login",
                new LoginRequest(role == AppRole.Admin ? "admin" : "user", password))
                .GetAwaiter()
                .GetResult();

            if (!response.Success)
                errorMessage = string.IsNullOrWhiteSpace(response.ErrorMessage) ? "Invalid password." : response.ErrorMessage;

            return response.Success;
        }
        catch (Exception ex)
        {
            errorMessage = $"Server login failed: {ex.Message}";
            return false;
        }
    }

    public static async Task<(bool Success, string ErrorMessage)> TryLoginAsync(AppRole role, string password)
    {
        try
        {
            var response = await PostAsync<LoginRequest, LoginResponse>(
                "/api/auth/login",
                new LoginRequest(role == AppRole.Admin ? "admin" : "user", password));

            var errorMessage = response.Success
                ? string.Empty
                : string.IsNullOrWhiteSpace(response.ErrorMessage) ? "Invalid password." : response.ErrorMessage;

            return (response.Success, errorMessage);
        }
        catch (TaskCanceledException)
        {
            return (false, "Server login timed out. Check the API address and server status.");
        }
        catch (Exception ex)
        {
            return (false, $"Server login failed: {ex.Message}");
        }
    }

    public static bool UsesDefaultPassword(AppRole role)
    {
        var defaultPassword = role == AppRole.Admin
            ? AuthService.DefaultAdminPassword
            : AuthService.DefaultUserPassword;

        return TryLogin(role, defaultPassword, out _);
    }

    public static bool TestConnection(out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            var response = HttpClient.GetAsync(BuildUri("/health")).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                errorMessage = $"Server returned {(int)response.StatusCode}.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static List<Club> GetClubs()
        => GetAsync<List<ClubDto>>("/api/clubs").GetAwaiter().GetResult().ConvertAll(MapClub);

    public static void CreateClub(Club club)
        => PostAsync<SaveClubRequest, object?>("/api/clubs", new SaveClubRequest(
            club.Name?.Trim() ?? string.Empty,
            club.Coach?.Trim() ?? string.Empty,
            club.City?.Trim() ?? string.Empty,
            club.Country?.Trim() ?? string.Empty)).GetAwaiter().GetResult();

    public static void UpdateClub(Club club)
        => PutAsync($"/api/clubs/{club.Id}", new SaveClubRequest(
            club.Name?.Trim() ?? string.Empty,
            club.Coach?.Trim() ?? string.Empty,
            club.City?.Trim() ?? string.Empty,
            club.Country?.Trim() ?? string.Empty)).GetAwaiter().GetResult();

    public static void DeleteClub(int id)
        => DeleteAsync($"/api/clubs/{id}").GetAwaiter().GetResult();

    public static List<Fighter> GetFighters()
        => GetAsync<List<FighterDto>>("/api/fighters").GetAwaiter().GetResult().ConvertAll(MapFighter);

    public static void CreateFighter(Fighter fighter)
        => PostAsync<SaveFighterRequest, object?>("/api/fighters", MapSaveFighterRequest(fighter)).GetAwaiter().GetResult();

    public static void UpdateFighter(Fighter fighter)
        => PutAsync($"/api/fighters/{fighter.Id}", MapSaveFighterRequest(fighter)).GetAwaiter().GetResult();

    public static void DeleteFighter(int id)
        => DeleteAsync($"/api/fighters/{id}").GetAwaiter().GetResult();

    public static List<Category> GetCategories()
        => GetAsync<List<CategoryDto>>("/api/categories").GetAwaiter().GetResult().ConvertAll(MapCategory);

    private static async Task<T> GetAsync<T>(string relativeUrl)
    {
        using var response = await HttpClient.GetAsync(BuildUri(relativeUrl));
        await EnsureSuccessAsync(response);
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return payload ?? throw new InvalidOperationException("Server returned an empty response.");
    }

    private static async Task<TResponse> PostAsync<TRequest, TResponse>(string relativeUrl, TRequest payload)
    {
        using var response = await HttpClient.PostAsJsonAsync(BuildUri(relativeUrl), payload);
        await EnsureSuccessAsync(response);

        if (typeof(TResponse) == typeof(object) || response.Content.Headers.ContentLength == 0)
            return default!;

        var model = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
        return model ?? throw new InvalidOperationException("Server returned an empty response.");
    }

    private static async Task PutAsync<TRequest>(string relativeUrl, TRequest payload)
    {
        using var response = await HttpClient.PutAsJsonAsync(BuildUri(relativeUrl), payload);
        await EnsureSuccessAsync(response);
    }

    private static async Task DeleteAsync(string relativeUrl)
    {
        using var response = await HttpClient.DeleteAsync(BuildUri(relativeUrl));
        await EnsureSuccessAsync(response);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
            ? $"Server returned {(int)response.StatusCode}."
            : body);
    }

    private static Uri BuildUri(string relativeUrl)
    {
        var baseUrl = RemoteApiSettingsService.GetConfiguredApiBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("Remote API address is not configured.");

        return new Uri(new Uri(baseUrl + "/", UriKind.Absolute), relativeUrl.TrimStart('/'));
    }

    private static Club MapClub(ClubDto dto)
        => new()
        {
            Id = dto.Id,
            Name = dto.Name,
            Coach = dto.Coach,
            City = dto.City,
            Country = dto.Country
        };

    private static Fighter MapFighter(FighterDto dto)
        => new()
        {
            Id = dto.Id,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Weight = dto.Weight,
            Age = dto.Age,
            ClubId = dto.ClubId,
            BirthYear = dto.BirthYear,
            Gender = dto.Gender,
            AgeCategory = dto.AgeCategory,
            WeightCategory = dto.WeightCategory,
            ClubName = dto.ClubName
        };

    private static SaveFighterRequest MapSaveFighterRequest(Fighter fighter)
        => new(
            fighter.FirstName,
            fighter.LastName,
            fighter.Weight,
            fighter.Age,
            fighter.ClubId,
            fighter.BirthYear,
            fighter.Gender,
            fighter.AgeCategory,
            fighter.WeightCategory);

    private static Category MapCategory(CategoryDto dto)
        => new()
        {
            Id = dto.Id,
            Division = dto.Division,
            Gender = dto.Gender,
            AgeMin = dto.AgeMin,
            AgeMax = dto.AgeMax,
            WeightMax = dto.WeightMax,
            IsOpenWeight = dto.IsOpenWeight,
            SortOrder = dto.SortOrder,
            CategoryName = dto.CategoryName,
            RoundCount = dto.RoundCount,
            RoundDurationSeconds = dto.RoundDurationSeconds,
            BreakDurationSeconds = dto.BreakDurationSeconds
        };

    private sealed record LoginRequest(string Role, string Password);

    private sealed record LoginResponse(bool Success, string Role, string ErrorMessage);

    private sealed record ClubDto(int Id, string Name, string Coach, string City, string Country);

    private sealed record SaveClubRequest(string Name, string Coach, string City, string Country);

    private sealed record FighterDto(
        int Id,
        string FirstName,
        string LastName,
        double Weight,
        int Age,
        int ClubId,
        int BirthYear,
        string Gender,
        string AgeCategory,
        string WeightCategory,
        string ClubName);

    private sealed record SaveFighterRequest(
        string FirstName,
        string LastName,
        double Weight,
        int Age,
        int ClubId,
        int BirthYear,
        string Gender,
        string AgeCategory,
        string WeightCategory);

    private sealed record CategoryDto(
        int Id,
        string Division,
        string Gender,
        int AgeMin,
        int AgeMax,
        double WeightMax,
        bool IsOpenWeight,
        int SortOrder,
        string CategoryName,
        int RoundCount,
        int RoundDurationSeconds,
        int BreakDurationSeconds);
}
