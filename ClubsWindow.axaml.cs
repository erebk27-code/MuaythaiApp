using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Data.Sqlite;
using MuaythaiApp.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using MuaythaiApp.Database;

namespace MuaythaiApp;

public partial class ClubsWindow : Window
{
    private readonly List<Club> allClubs = new();
    private readonly DatabaseAutoRefresh? databaseAutoRefresh;
    private Club? selectedClub;

    public ClubsWindow()
    {
        InitializeComponent();

        if (!AppSession.IsAdmin)
        {
            Opened += (_, __) => Close();
            return;
        }

        Opened += (_, __) => LoadClubs();
        databaseAutoRefresh = new DatabaseAutoRefresh(this, LoadClubs);
    }

    private void LoadClubs()
    {
        allClubs.Clear();

        if (RemoteApiClient.IsEnabled)
        {
            allClubs.AddRange(RemoteApiClient.GetClubs());
            ApplySearch();
            return;
        }

        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();

        var cmd = c.CreateCommand();
        cmd.CommandText =
        @"
        SELECT
            Id,
            Name,
            Coach,
            City,
            Country
        FROM Clubs
        ORDER BY Name
        ";

        using var r = cmd.ExecuteReader();

        while (r.Read())
        {
            allClubs.Add(new Club
            {
                Id = r.GetInt32(0),
                Name = r.IsDBNull(1) ? string.Empty : r.GetString(1),
                Coach = r.IsDBNull(2) ? string.Empty : r.GetString(2),
                City = r.IsDBNull(3) ? string.Empty : r.GetString(3),
                Country = r.IsDBNull(4) ? string.Empty : r.GetString(4)
            });
        }

        ApplySearch();
    }

    private void AddClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!TryBuildClubForm(out var formData))
                return;

            if (RemoteApiClient.IsEnabled)
            {
                RemoteApiClient.CreateClub(formData);
                ClubStatusText.Text = "Club added.";
                ClearFormInternal();
                LoadClubs();
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
        }
        catch (Exception ex)
        {
            ClubStatusText.Text = "Club could not be added.";
            Console.WriteLine(ex.ToString());
        }
    }

    private void UpdateClick(object? sender, RoutedEventArgs e)
    {
        if (selectedClub == null)
        {
            ClubStatusText.Text = "Select a club to update.";
            return;
        }

        try
        {
            if (!TryBuildClubForm(out var formData))
                return;

            if (RemoteApiClient.IsEnabled)
            {
                formData.Id = selectedClub.Id;
                RemoteApiClient.UpdateClub(formData);
                ClubStatusText.Text = "Club updated.";
                ClearFormInternal();
                LoadClubs();
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
        }
        catch (Exception ex)
        {
            ClubStatusText.Text = "Club could not be updated.";
            Console.WriteLine(ex.ToString());
        }
    }

    private void DeleteClick(object? sender, RoutedEventArgs e)
    {
        var club = ClubsList.SelectedItem as Club ?? selectedClub;

        if (club == null)
        {
            ClubStatusText.Text = "Select a club to delete.";
            return;
        }

        try
        {
            if (RemoteApiClient.IsEnabled)
            {
                RemoteApiClient.DeleteClub(club.Id);
                ClubStatusText.Text = "Club deleted.";
                ClearFormInternal();
                LoadClubs();
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
        }
        catch (Exception ex)
        {
            ClubStatusText.Text = "Club could not be deleted.";
            Console.WriteLine(ex.ToString());
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

    private void ClubSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        selectedClub = ClubsList.SelectedItem as Club;

        if (selectedClub == null)
            return;

        ClubNameBox.Text = selectedClub.Name ?? string.Empty;
        CoachBox.Text = selectedClub.Coach ?? string.Empty;
        CityBox.Text = selectedClub.City ?? string.Empty;
        CountryBox.Text = selectedClub.Country ?? string.Empty;
        ClubStatusText.Text = $"Selected club #{selectedClub.Id}";
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
        ClubsList.SelectedItem = null;
    }
}
