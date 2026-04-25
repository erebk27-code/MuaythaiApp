using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Data.Sqlite;
using MuaythaiApp.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MuaythaiApp.Database;

namespace MuaythaiApp;

public partial class ClubsWindow : Window
{
    private readonly List<Club> allClubs = new();
    private readonly List<Fighter> clubAthletes = new();
    private readonly DatabaseAutoRefresh? databaseAutoRefresh;
    private Club? selectedClub;
    private bool isLoadingClubAthletes;

    public ClubsWindow()
    {
        InitializeComponent();

        if (!AppSession.IsAdmin)
        {
            Opened += (_, __) => Close();
            return;
        }

        LocalizationService.LocalizeControlTree(this);
        Opened += async (_, __) => await LoadClubsAsync();

        if (!RemoteApiClient.IsEnabled)
            databaseAutoRefresh = new DatabaseAutoRefresh(this, LoadClubs);
    }

    private void LoadClubs()
    {
        allClubs.Clear();
        allClubs.AddRange(LoadClubsCore());
        ApplySearch();
        LoadSelectedClubAthletes();
    }

    private async Task LoadClubsAsync()
    {
        StartupLogger.Log("ClubsWindow.LoadClubsAsync started");
        ClubStatusText.Text = "Loading clubs...";

        try
        {
            var clubs = await Task.Run(LoadClubsCore);
            allClubs.Clear();
            allClubs.AddRange(clubs);
            ApplySearch();
            LoadSelectedClubAthletes();
            ClubStatusText.Text = string.Empty;
            StartupLogger.Log($"ClubsWindow.LoadClubsAsync completed with {clubs.Count} clubs");
        }
        catch (Exception ex)
        {
            ClubStatusText.Text = "Clubs could not be loaded.";
            Console.WriteLine(ex.ToString());
            StartupLogger.Log(ex, "ClubsWindow.LoadClubsAsync failed");
        }
    }

    private List<Club> LoadClubsCore()
    {
        var clubs = new List<Club>();

        if (RemoteApiClient.IsEnabled)
        {
            clubs.AddRange(RemoteApiClient.GetClubs());
            return clubs;
        }

        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();

        var cmd = c.CreateCommand();
        cmd.CommandText =
        @"
        SELECT
            c.Id,
            c.Name,
            c.Coach,
            c.City,
            c.Country,
            COUNT(f.Id)
        FROM Clubs c
        LEFT JOIN Fighters f
            ON f.ClubId = c.Id
        GROUP BY c.Id, c.Name, c.Coach, c.City, c.Country
        ORDER BY c.Name
        ";

        using var r = cmd.ExecuteReader();

        while (r.Read())
        {
            clubs.Add(new Club
            {
                Id = r.GetInt32(0),
                Name = r.IsDBNull(1) ? string.Empty : r.GetString(1),
                Coach = r.IsDBNull(2) ? string.Empty : r.GetString(2),
                City = r.IsDBNull(3) ? string.Empty : r.GetString(3),
                Country = r.IsDBNull(4) ? string.Empty : r.GetString(4),
                AthleteCount = r.IsDBNull(5) ? 0 : r.GetInt32(5)
            });
        }

        return clubs;
    }

    private async void AddClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            StartupLogger.Log("ClubsWindow.AddClick started");
            if (!TryBuildClubForm(out var formData))
                return;

            if (RemoteApiClient.IsEnabled)
            {
                await Task.Run(() => RemoteApiClient.CreateClub(formData));
                ClubStatusText.Text = "Club added.";
                ClearFormInternal();
                await LoadClubsAsync();
                StartupLogger.Log("ClubsWindow.AddClick completed via remote API");
                return;
            }

            using var c =
                DatabaseHelper.CreateConnection();

            c.Open();

            var cmd = c.CreateCommand();

            cmd.CommandText =
            @"
            INSERT INTO Clubs
            (
                Name,
                Coach,
                City,
                Country
            )
            VALUES
            (
                @n,
                @coach,
                @city,
                @country
            )
            ";

            FillClubParameters(cmd, formData);

            cmd.ExecuteNonQuery();

            ClubStatusText.Text = "Club added.";
            ClearFormInternal();
            LoadClubs();
            StartupLogger.Log("ClubsWindow.AddClick completed via local database");
        }
        catch (Exception ex)
        {
            ClubStatusText.Text = "Club could not be added.";
            Console.WriteLine(ex.ToString());
            StartupLogger.Log(ex, "ClubsWindow.AddClick failed");
        }
    }

    private async void UpdateClick(object? sender, RoutedEventArgs e)
    {
        if (selectedClub == null)
        {
            ClubStatusText.Text = "Select a club to update.";
            return;
        }

        try
        {
            StartupLogger.Log("ClubsWindow.UpdateClick started");
            if (!TryBuildClubForm(out var formData))
                return;

            if (RemoteApiClient.IsEnabled)
            {
                formData.Id = selectedClub.Id;
                await Task.Run(() => RemoteApiClient.UpdateClub(formData));
                ClubStatusText.Text = "Club updated.";
                ClearFormInternal();
                await LoadClubsAsync();
                StartupLogger.Log("ClubsWindow.UpdateClick completed via remote API");
                return;
            }

            using var c =
                DatabaseHelper.CreateConnection();

            c.Open();

            var cmd = c.CreateCommand();
            cmd.CommandText =
            @"
            UPDATE Clubs
            SET
                Name = @n,
                Coach = @coach,
                City = @city,
                Country = @country
            WHERE Id = @id
            ";

            FillClubParameters(cmd, formData);
            cmd.Parameters.AddWithValue("@id", selectedClub.Id);
            cmd.ExecuteNonQuery();

            ClubStatusText.Text = "Club updated.";
            ClearFormInternal();
            LoadClubs();
            StartupLogger.Log("ClubsWindow.UpdateClick completed via local database");
        }
        catch (Exception ex)
        {
            ClubStatusText.Text = "Club could not be updated.";
            Console.WriteLine(ex.ToString());
            StartupLogger.Log(ex, "ClubsWindow.UpdateClick failed");
        }
    }

    private async void DeleteClick(object? sender, RoutedEventArgs e)
    {
        var club = ClubsList.SelectedItem as Club ?? selectedClub;

        if (club == null)
        {
            ClubStatusText.Text = "Select a club to delete.";
            return;
        }

        try
        {
            StartupLogger.Log("ClubsWindow.DeleteClick started");
            if (RemoteApiClient.IsEnabled)
            {
                await Task.Run(() => RemoteApiClient.DeleteClub(club.Id));
                ClubStatusText.Text = "Club deleted.";
                ClearFormInternal();
                await LoadClubsAsync();
                StartupLogger.Log("ClubsWindow.DeleteClick completed via remote API");
                return;
            }

            using var c =
                DatabaseHelper.CreateConnection();

            c.Open();

            var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM Clubs WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", club.Id);
            cmd.ExecuteNonQuery();

            ClubStatusText.Text = "Club deleted.";
            ClearFormInternal();
            LoadClubs();
            StartupLogger.Log("ClubsWindow.DeleteClick completed via local database");
        }
        catch (Exception ex)
        {
            ClubStatusText.Text = "Club could not be deleted.";
            Console.WriteLine(ex.ToString());
            StartupLogger.Log(ex, "ClubsWindow.DeleteClick failed");
        }
    }

    private void ClearClick(object? sender, RoutedEventArgs e)
    {
        ClearFormInternal();
        ClubStatusText.Text = "Form cleared.";
    }

    private void SearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySearch();
    }

    private async void ClubSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        selectedClub = ClubsList.SelectedItem as Club;

        if (selectedClub == null)
        {
            LoadSelectedClubAthletes();
            return;
        }

        ClubNameBox.Text = selectedClub.Name ?? string.Empty;
        CoachBox.Text = selectedClub.Coach ?? string.Empty;
        CityBox.Text = selectedClub.City ?? string.Empty;
        CountryBox.Text = selectedClub.Country ?? string.Empty;
        AthleteCountBox.Text = selectedClub.AthleteCount.ToString();
        ClubStatusText.Text = $"Selected club #{selectedClub.Id}";
        await LoadSelectedClubAthletesAsync();
    }

    private void ApplySearch()
    {
        var search = SearchBox.Text?.Trim().ToLowerInvariant();
        IEnumerable<Club> query = allClubs;

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                (x.Name ?? string.Empty).ToLowerInvariant().Contains(search) ||
                (x.Coach ?? string.Empty).ToLowerInvariant().Contains(search) ||
                (x.City ?? string.Empty).ToLowerInvariant().Contains(search) ||
                (x.Country ?? string.Empty).ToLowerInvariant().Contains(search));
        }

        var result = query
            .OrderBy(x => x.Name)
            .ToList();

        ClubsList.ItemsSource = null;
        ClubsList.ItemsSource = result;
        ListInfoText.Text = $"{result.Count} clubs listed";

        if (selectedClub != null)
        {
            ClubsList.SelectedItem = result.FirstOrDefault(x => x.Id == selectedClub.Id);
        }
    }

    private bool TryBuildClubForm(out Club formData)
    {
        formData = new Club
        {
            Name = ClubNameBox.Text?.Trim() ?? string.Empty,
            Coach = CoachBox.Text?.Trim() ?? string.Empty,
            City = CityBox.Text?.Trim() ?? string.Empty,
            Country = CountryBox.Text?.Trim() ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(formData.Name))
        {
            ClubStatusText.Text = "Club name is required.";
            return false;
        }

        return true;
    }

    private void FillClubParameters(SqliteCommand cmd, Club club)
    {
        cmd.Parameters.AddWithValue("@n", club.Name ?? string.Empty);
        cmd.Parameters.AddWithValue("@coach", club.Coach ?? string.Empty);
        cmd.Parameters.AddWithValue("@city", club.City ?? string.Empty);
        cmd.Parameters.AddWithValue("@country", club.Country ?? string.Empty);
    }

    private void ClearFormInternal()
    {
        selectedClub = null;
        ClubNameBox.Text = string.Empty;
        CoachBox.Text = string.Empty;
        CityBox.Text = string.Empty;
        CountryBox.Text = string.Empty;
        AthleteCountBox.Text = string.Empty;
        ClubsList.SelectedItem = null;
        clubAthletes.Clear();
        ClubAthletesList.ItemsSource = null;
        ClubAthletesSummaryText.Text = "Select a club to see athlete details.";
    }

    private void LoadSelectedClubAthletes()
    {
        if (selectedClub == null)
        {
            clubAthletes.Clear();
            ClubAthletesList.ItemsSource = null;
            ClubAthletesSummaryText.Text = "Select a club to see athlete details.";
            return;
        }

        if (RemoteApiClient.IsEnabled)
        {
            try
            {
                clubAthletes.Clear();
                clubAthletes.AddRange(LoadSelectedClubAthletesCore(selectedClub.Id));
                ClubAthletesList.ItemsSource = null;
                ClubAthletesList.ItemsSource = clubAthletes;
                AthleteCountBox.Text = clubAthletes.Count.ToString();
                ClubAthletesSummaryText.Text = $"{selectedClub.Name} | {selectedClub.City} | Coach: {selectedClub.Coach} | {clubAthletes.Count} athlete(s)";
                return;
            }
            catch (Exception ex)
            {
                ClubAthletesList.ItemsSource = null;
                AthleteCountBox.Text = selectedClub.AthleteCount.ToString();
                ClubAthletesSummaryText.Text = "Club athletes could not be loaded from the server.";
                StartupLogger.Log(ex, "ClubsWindow.LoadSelectedClubAthletes remote load failed");
                return;
            }
        }

        clubAthletes.Clear();
        clubAthletes.AddRange(LoadSelectedClubAthletesCore(selectedClub.Id));
        ClubAthletesList.ItemsSource = null;
        ClubAthletesList.ItemsSource = clubAthletes;
        AthleteCountBox.Text = clubAthletes.Count.ToString();
        ClubAthletesSummaryText.Text = $"{selectedClub.Name} | {selectedClub.City} | Coach: {selectedClub.Coach} | {clubAthletes.Count} athlete(s)";
    }

    private async Task LoadSelectedClubAthletesAsync()
    {
        if (selectedClub == null)
        {
            LoadSelectedClubAthletes();
            return;
        }

        if (isLoadingClubAthletes)
            return;

        try
        {
            isLoadingClubAthletes = true;
            ClubAthletesSummaryText.Text = "Loading club athletes...";

            if (RemoteApiClient.IsEnabled)
            {
                var clubId = selectedClub.Id;
                var selectedClubSnapshot = selectedClub;

                var fighters = await Task.Run(() => LoadSelectedClubAthletesCore(clubId));

                if (selectedClub == null || selectedClub.Id != clubId)
                    return;

                clubAthletes.Clear();
                clubAthletes.AddRange(fighters);
                ClubAthletesList.ItemsSource = null;
                ClubAthletesList.ItemsSource = clubAthletes;
                AthleteCountBox.Text = clubAthletes.Count.ToString();
                ClubAthletesSummaryText.Text = $"{selectedClubSnapshot.Name} | {selectedClubSnapshot.City} | Coach: {selectedClubSnapshot.Coach} | {clubAthletes.Count} athlete(s)";
                return;
            }

            var localClubId = selectedClub.Id;
            var localSelectedClubSnapshot = selectedClub;
            var localFighters = await Task.Run(() => LoadSelectedClubAthletesCore(localClubId));
            if (selectedClub == null || selectedClub.Id != localClubId)
                return;

            clubAthletes.Clear();
            clubAthletes.AddRange(localFighters);
            ClubAthletesList.ItemsSource = null;
            ClubAthletesList.ItemsSource = clubAthletes;
            AthleteCountBox.Text = clubAthletes.Count.ToString();
            ClubAthletesSummaryText.Text = $"{localSelectedClubSnapshot.Name} | {localSelectedClubSnapshot.City} | Coach: {localSelectedClubSnapshot.Coach} | {clubAthletes.Count} athlete(s)";
        }
        catch (Exception ex)
        {
            ClubAthletesList.ItemsSource = null;
            ClubAthletesSummaryText.Text = "Club athletes could not be loaded.";
            StartupLogger.Log(ex, "ClubsWindow.LoadSelectedClubAthletesAsync failed");
        }
        finally
        {
            isLoadingClubAthletes = false;
        }
    }

    private List<Fighter> LoadSelectedClubAthletesCore(int clubId)
    {
        if (RemoteApiClient.IsEnabled)
        {
            return RemoteApiClient.GetFighters()
                .Where(x => x.ClubId == clubId)
                .OrderBy(x => x.AgeCategory)
                .ThenBy(x => x.WeightCategory)
                .ThenBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ToList();
        }

        var fighters = new List<Fighter>();
        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            Id,
            FirstName,
            LastName,
            IFNULL(Age, 0),
            IFNULL(Gender, ''),
            IFNULL(AgeCategory, ''),
            IFNULL(WeightCategory, ''),
            IFNULL(Weight, 0)
        FROM Fighters
        WHERE ClubId = @clubId
        ORDER BY AgeCategory, WeightCategory, LastName, FirstName
        ";
        command.Parameters.AddWithValue("@clubId", clubId);

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            fighters.Add(new Fighter
            {
                Id = reader.GetInt32(0),
                FirstName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                LastName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Age = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Gender = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                AgeCategory = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                WeightCategory = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                Weight = reader.IsDBNull(7) ? 0 : reader.GetDouble(7)
            });
        }

        return fighters;
    }
}
