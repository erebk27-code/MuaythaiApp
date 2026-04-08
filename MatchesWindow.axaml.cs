using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Data.Sqlite;
using MuaythaiApp.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using MuaythaiApp.Database;

namespace MuaythaiApp;

public partial class MatchesWindow : Window
{
    private List<Match> allMatches = new();
    private List<Fighter> allFighters = new();
    private readonly DatabaseAutoRefresh? databaseAutoRefresh;
    private int selectedDayNumber = 1;

    public MatchesWindow()
    {
        InitializeComponent();

        if (!AppSession.IsAdmin)
        {
            Opened += (_, __) => Close();
            return;
        }

        JudgesCombo.ItemsSource = new List<int> { 3, 5 };
        JudgesCombo.SelectedIndex = 1;

        DayFilterCombo.ItemsSource = Enumerable.Range(1, 4)
            .Select(day => $"Day {day}")
            .ToList();
        DayFilterCombo.SelectedIndex = 0;
        selectedDayNumber = 1;

        Opened += (_, __) => RefreshMatches();
        Activated += (_, __) => RefreshMatches();
        LocalizationService.LanguageChanged += ApplyLocalization;
        Closed += (_, __) => LocalizationService.LanguageChanged -= ApplyLocalization;

        NormalizeBoutNumbers();
        ApplyLocalization();
        LoadMatches();
        databaseAutoRefresh = new DatabaseAutoRefresh(this, RefreshMatches);
    }

    private void AutoMatchClick(object? sender, RoutedEventArgs e)
    {
        if (!CanRebuildOpeningRound(out var reason))
        {
            MatchesSummaryText.Text = reason;
            return;
        }

        LoadFighters();
        MakeMatches();
        RefreshMatches();
        selectedDayNumber = 1;
        DayFilterCombo.SelectedIndex = 0;
        LoadMatches();
        MatchesSummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? "Pierwszy dzien walk zostal wygenerowany ponownie."
            : "Day 1 matches were regenerated.";
    }

    private void RefreshMatches()
    {
        selectedDayNumber = ParseSelectedDayNumber();
        NormalizeBoutNumbers();
        LoadMatches();
    }

    private void LoadMatches()
    {
        selectedDayNumber = ParseSelectedDayNumber();
        var list = new List<Match>();

        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();

        var cmd = c.CreateCommand();

        cmd.CommandText =
        @"
        SELECT
        Id,
        OrderNo,
        Fighter1Name,
        Fighter2Name,
        AgeCategory,
        WeightCategory,
        Gender,
        JudgesCount,
        DayNumber
        FROM Matches
        WHERE DayNumber = @dayNumber
        ORDER BY OrderNo, Id
        ";
        cmd.Parameters.AddWithValue("@dayNumber", selectedDayNumber);

        using var r = cmd.ExecuteReader();

        while (r.Read())
        {
            var m = new Match();

            m.Id = r.GetInt32(0);
            m.OrderNo = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            m.Fighter1Name = r.IsDBNull(2) ? string.Empty : r.GetString(2);
            m.Fighter2Name = r.IsDBNull(3) ? string.Empty : r.GetString(3);
            m.AgeCategory = r.IsDBNull(4) ? string.Empty : r.GetString(4);
            m.WeightCategory = r.IsDBNull(5) ? string.Empty : r.GetString(5);
            m.Gender = r.IsDBNull(6) ? string.Empty : r.GetString(6);
            m.JudgesCount = r.IsDBNull(7) ? 0 : r.GetInt32(7);
            m.DayNumber = r.IsDBNull(8) ? 1 : r.GetInt32(8);

            list.Add(m);
        }

        allMatches = list;
        MatchesGrid.ItemsSource = allMatches;
        MatchesSummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? $"Wyswietlono {allMatches.Count} walk dla dnia {selectedDayNumber}"
            : $"{allMatches.Count} matches listed for Day {selectedDayNumber}";
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.T("Matches");
        MatchesTitleText.Text = LocalizationService.T("Matches");
        DayLabelText.Text = LocalizationService.T("Day");
        JudgesLabelText.Text = LocalizationService.T("Judges");
        AutoMatchButton.Content = LocalizationService.T("AutoMatchMaker");
        ScoreButton.Content = LocalizationService.T("Score");
        BoutHeaderText.Text = LocalizationService.T("Bout");
        RedHeaderText.Text = LocalizationService.T("Red");
        BlueHeaderText.Text = LocalizationService.T("Blue");
        CategoryHeaderText.Text = LocalizationService.T("Category");
        WeightHeaderText.Text = LocalizationService.T("Weight");
        GenderHeaderText.Text = LocalizationService.T("Gender");
        DayFilterCombo.ItemsSource = Enumerable.Range(1, 4)
            .Select(day => $"{LocalizationService.T("Day")} {day}")
            .ToList();
        DayFilterCombo.SelectedIndex = selectedDayNumber - 1;
    }

    private void NormalizeBoutNumbers()
    {
        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();

        for (int dayNumber = 1; dayNumber <= 4; dayNumber++)
        {
            var read = c.CreateCommand();
            read.CommandText =
            @"
            SELECT Id
            FROM Matches
            WHERE DayNumber = @dayNumber
            ORDER BY
                CASE
                    WHEN OrderNo IS NULL OR OrderNo <= 0 THEN 1
                    ELSE 0
                END,
                OrderNo,
                Id
            ";
            read.Parameters.AddWithValue("@dayNumber", dayNumber);

            var ids = new List<int>();

            using (var r = read.ExecuteReader())
            {
                while (r.Read())
                    ids.Add(r.GetInt32(0));
            }

            for (int i = 0; i < ids.Count; i++)
            {
                var update = c.CreateCommand();
                update.CommandText =
                @"
                UPDATE Matches
                SET OrderNo = @orderNo
                WHERE Id = @id
                ";

                update.Parameters.AddWithValue("@orderNo", i + 1);
                update.Parameters.AddWithValue("@id", ids[i]);
                update.ExecuteNonQuery();
            }
        }
    }

    private void LoadFighters()
    {
        allFighters.Clear();

        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();

        var cmd = c.CreateCommand();

        cmd.CommandText =
        @"
        SELECT
        Id,
        FirstName,
        AgeCategory,
        WeightCategory,
        Gender
        FROM Fighters
        ";

        using var r = cmd.ExecuteReader();

        while (r.Read())
        {
            var f = new Fighter();

            f.Id = r.GetInt32(0);
            f.FirstName = r.GetString(1);
            f.AgeCategory = r.GetString(2);
            f.WeightCategory = r.GetString(3);
            f.Gender = r.GetString(4);

            allFighters.Add(f);
        }
    }

    private void MakeMatches()
    {
        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();
        DeleteDayOneMatches(c);

        int order = 1;

        var groups =
            allFighters.GroupBy(x =>
                x.AgeCategory + "_" +
                x.WeightCategory + "_" +
                x.Gender);

        foreach (var g in groups)
        {
            var list = g.ToList();

            for (int i = 0; i < list.Count; i += 2)
            {
                var f1 = list[i];

                Fighter? f2 = null;

                if (i + 1 < list.Count)
                    f2 = list[i + 1];

                SaveMatch(c, f1, f2, order, g.Key);

                order++;
            }
        }
    }

    private void SaveMatch(
        SqliteConnection c,
        Fighter f1,
        Fighter? f2,
        int order,
        string group)
    {
        int judges = 5;

        if (JudgesCombo.SelectedItem != null)
            judges = Convert.ToInt32(JudgesCombo.SelectedItem);

        var cmd = c.CreateCommand();

        cmd.CommandText =
        @"
        INSERT INTO Matches
        (
        Fighter1Id,
        Fighter2Id,
        Fighter1Name,
        Fighter2Name,
        AgeCategory,
        WeightCategory,
        Gender,
        CategoryGroup,
        OrderNo,
        JudgesCount,
        DayNumber
        )
        VALUES
        (
        @f1id,
        @f2id,
        @n1,
        @n2,
        @ac,
        @wc,
        @g,
        @cg,
        @orderNo,
        @j,
        1
        )
        ";

        cmd.Parameters.AddWithValue("@f1id", f1.Id);
        cmd.Parameters.AddWithValue("@f2id", f2?.Id ?? 0);
        cmd.Parameters.AddWithValue("@n1", f1.FirstName);

        if (f2 == null)
            cmd.Parameters.AddWithValue("@n2", "BYE");
        else
            cmd.Parameters.AddWithValue("@n2", f2.FirstName);

        cmd.Parameters.AddWithValue("@ac", f1.AgeCategory);
        cmd.Parameters.AddWithValue("@wc", f1.WeightCategory);
        cmd.Parameters.AddWithValue("@g", f1.Gender);
        cmd.Parameters.AddWithValue("@cg", group);
        cmd.Parameters.AddWithValue("@orderNo", order);
        cmd.Parameters.AddWithValue("@j", judges);

        cmd.ExecuteNonQuery();
    }

    private bool CanRebuildOpeningRound(out string reason)
    {
        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var hasProgressedTournament = connection.CreateCommand();
        hasProgressedTournament.CommandText =
        @"
        SELECT EXISTS (
            SELECT 1
            FROM Matches
            WHERE DayNumber > 1
        )
        ";

        if (Convert.ToInt32(hasProgressedTournament.ExecuteScalar() ?? 0) == 1)
        {
            reason = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Auto Match Maker jest zablokowany, bo zostaly juz utworzone walki kolejnych dni."
                : "Auto Match Maker is blocked because later-day matches already exist.";
            return false;
        }

        var hasCompletedDayOneMatches = connection.CreateCommand();
        hasCompletedDayOneMatches.CommandText =
        @"
        SELECT EXISTS (
            SELECT 1
            FROM MatchResult mr
            INNER JOIN Matches m
                ON m.Id = mr.MatchId
            WHERE m.DayNumber = 1
        )
        ";

        if (Convert.ToInt32(hasCompletedDayOneMatches.ExecuteScalar() ?? 0) == 1)
        {
            reason = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Auto Match Maker jest zablokowany, bo dla dnia 1 zapisano juz wyniki walk."
                : "Auto Match Maker is blocked because Day 1 results have already been recorded.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void DeleteDayOneMatches(SqliteConnection connection)
    {
        var matchIds = new List<int>();

        var read = connection.CreateCommand();
        read.CommandText =
        @"
        SELECT Id
        FROM Matches
        WHERE DayNumber = 1
        ";

        using (var reader = read.ExecuteReader())
        {
            while (reader.Read())
                matchIds.Add(reader.GetInt32(0));
        }

        foreach (var matchId in matchIds)
        {
            var deleteScores = connection.CreateCommand();
            deleteScores.CommandText = "DELETE FROM JudgeScores WHERE MatchId = @matchId";
            deleteScores.Parameters.AddWithValue("@matchId", matchId);
            deleteScores.ExecuteNonQuery();

            var deleteResults = connection.CreateCommand();
            deleteResults.CommandText = "DELETE FROM MatchResult WHERE MatchId = @matchId";
            deleteResults.Parameters.AddWithValue("@matchId", matchId);
            deleteResults.ExecuteNonQuery();
        }

        var deleteMatches = connection.CreateCommand();
        deleteMatches.CommandText = "DELETE FROM Matches WHERE DayNumber = 1";
        deleteMatches.ExecuteNonQuery();
    }

    private void DayChanged(object? sender, SelectionChangedEventArgs e)
    {
        selectedDayNumber = ParseSelectedDayNumber();
        LoadMatches();
    }

    private int ParseSelectedDayNumber()
    {
        if (DayFilterCombo.SelectedIndex >= 0)
            return DayFilterCombo.SelectedIndex + 1;

        var text = DayFilterCombo.SelectedItem?.ToString();

        if (!string.IsNullOrWhiteSpace(text) &&
            text.StartsWith("Day ", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(text[4..], out var dayNumber))
        {
            return dayNumber;
        }

        return 1;
    }

    private void ScoreClick(object? sender, RoutedEventArgs e)
    {
        var m = MatchesGrid.SelectedItem as Match;

        if (m == null) return;

        var w = new ScoreWindow(m);

        w.Show();
    }
}
