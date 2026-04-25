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
    private const string DefaultRingName = "RING A";
    private List<Match> allMatches = new();
    private List<Fighter> allFighters = new();
    private List<Category> activeCategories = new();
    private int selectedDayNumber = 1;
    private string selectedRingName = "All";

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

        DayFilterCombo.ItemsSource = Enumerable.Range(1, ChampionshipSettingsService.GetDayCount())
            .Select(day => $"Day {day}")
            .ToList();
        DayFilterCombo.SelectedIndex = 0;
        selectedDayNumber = 1;
        LoadRingFilter();

        Opened += (_, __) => RefreshMatches();
        LocalizationService.LanguageChanged += ApplyLocalization;
        Closed += (_, __) => LocalizationService.LanguageChanged -= ApplyLocalization;

        NormalizeBoutNumbers();
        ApplyLocalization();
        LoadMatches();
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
        selectedRingName = RingFilterCombo.SelectedItem?.ToString() ?? selectedRingName;
        NormalizeBoutNumbers();
        LoadMatches();
    }

    private void LoadMatches()
    {
        selectedDayNumber = ParseSelectedDayNumber();
        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();
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
        RingName,
        Fighter1Name,
        Fighter2Name,
        AgeCategory,
        WeightCategory,
        Gender,
        JudgesCount,
        DayNumber
        FROM Matches
        WHERE ChampionshipId = @championshipId
          AND DayNumber = @dayNumber
          AND (@allRings = 1 OR RingName = @ringName)
        ORDER BY OrderNo, Id
        ";
        cmd.Parameters.AddWithValue("@championshipId", championshipId);
        cmd.Parameters.AddWithValue("@dayNumber", selectedDayNumber);
        cmd.Parameters.AddWithValue("@ringName", selectedRingName);
        cmd.Parameters.AddWithValue("@allRings", IsAllRingsSelected() ? 1 : 0);

        using var r = cmd.ExecuteReader();

        while (r.Read())
        {
            var m = new Match();

            m.Id = r.GetInt32(0);
            m.OrderNo = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            m.RingName = r.IsDBNull(2) ? DefaultRingName : r.GetString(2);
            m.Fighter1Name = r.IsDBNull(3) ? string.Empty : r.GetString(3);
            m.Fighter2Name = r.IsDBNull(4) ? string.Empty : r.GetString(4);
            m.AgeCategory = r.IsDBNull(5) ? string.Empty : r.GetString(5);
            m.WeightCategory = r.IsDBNull(6) ? string.Empty : r.GetString(6);
            m.Gender = r.IsDBNull(7) ? string.Empty : r.GetString(7);
            m.JudgesCount = r.IsDBNull(8) ? 0 : r.GetInt32(8);
            m.DayNumber = r.IsDBNull(9) ? 1 : r.GetInt32(9);

            list.Add(m);
        }

        allMatches = list;
        MatchesGrid.ItemsSource = allMatches;
        var ringSummary = IsAllRingsSelected() ? LocalizationService.T("All") : selectedRingName;
        MatchesSummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? $"Wyswietlono {allMatches.Count} walk dla dnia {selectedDayNumber} | {ringSummary}"
            : $"{allMatches.Count} matches listed for Day {selectedDayNumber} | {ringSummary}";
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.T("Matches");
        MatchesTitleText.Text = LocalizationService.T("Matches");
        DayLabelText.Text = LocalizationService.T("Day");
        RingLabelText.Text = LocalizationService.T("Ring");
        JudgesLabelText.Text = LocalizationService.T("Judges");
        AutoMatchButton.Content = LocalizationService.T("AutoMatchMaker");
        DistributeRingsButton.Content = LocalizationService.T("DistributeRings");
        ScoreButton.Content = LocalizationService.T("Score");
        BoutHeaderText.Text = LocalizationService.T("Bout");
        RingHeaderText.Text = LocalizationService.T("Ring");
        RedHeaderText.Text = LocalizationService.T("Red");
        BlueHeaderText.Text = LocalizationService.T("Blue");
        CategoryHeaderText.Text = LocalizationService.T("Category");
        WeightHeaderText.Text = LocalizationService.T("Weight");
        GenderHeaderText.Text = LocalizationService.T("Gender");
        DayFilterCombo.ItemsSource = Enumerable.Range(1, ChampionshipSettingsService.GetDayCount())
            .Select(day => $"{LocalizationService.T("Day")} {day}")
            .ToList();
        DayFilterCombo.SelectedIndex = selectedDayNumber - 1;
        LoadRingFilter();
        LocalizationService.LocalizeControlTree(this);
    }

    private void NormalizeBoutNumbers()
    {
        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();
        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();

        var ringNames = ChampionshipSettingsService.GetRingNames();

        for (int dayNumber = 1; dayNumber <= ChampionshipSettingsService.GetDayCount(); dayNumber++)
        {
            foreach (var ringName in ringNames)
            {
                var read = c.CreateCommand();
                read.CommandText =
                @"
                SELECT Id
                FROM Matches
                WHERE ChampionshipId = @championshipId
                  AND DayNumber = @dayNumber
                  AND RingName = @ringName
                ORDER BY
                    CASE
                        WHEN OrderNo IS NULL OR OrderNo <= 0 THEN 1
                        ELSE 0
                    END,
                    OrderNo,
                    Id
                ";
                read.Parameters.AddWithValue("@championshipId", championshipId);
                read.Parameters.AddWithValue("@dayNumber", dayNumber);
                read.Parameters.AddWithValue("@ringName", ringName);

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
    }

    private void LoadFighters()
    {
        allFighters.Clear();
        activeCategories.Clear();

        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();

        var categoryCommand = c.CreateCommand();
        categoryCommand.CommandText =
        @"
        SELECT
        Id,
        Division,
        Gender,
        AgeMin,
        AgeMax,
        WeightMax,
        IsOpenWeight,
        SortOrder,
        CategoryName,
        RoundCount,
        RoundDurationSeconds,
        BreakDurationSeconds
        FROM Categories
        ";

        var categories = new List<Category>();
        using (var categoryReader = categoryCommand.ExecuteReader())
        {
            while (categoryReader.Read())
            {
                categories.Add(new Category
                {
                    Id = categoryReader.GetInt32(0),
                    Division = categoryReader.GetString(1),
                    Gender = categoryReader.GetString(2),
                    AgeMin = categoryReader.GetInt32(3),
                    AgeMax = categoryReader.GetInt32(4),
                    WeightMax = categoryReader.GetDouble(5),
                    IsOpenWeight = categoryReader.GetInt32(6) == 1,
                    SortOrder = categoryReader.GetInt32(7),
                    CategoryName = categoryReader.GetString(8),
                    RoundCount = categoryReader.GetInt32(9),
                    RoundDurationSeconds = categoryReader.GetInt32(10),
                    BreakDurationSeconds = categoryReader.GetInt32(11)
                });
            }
        }

        activeCategories = ChampionshipSettingsService.FilterActiveCategories(categories);

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

            if (IsCategoryAllowed(f.AgeCategory, f.WeightCategory, f.Gender))
                allFighters.Add(f);
        }
    }

    private void DistributeRingsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            selectedDayNumber = ParseSelectedDayNumber();
            var ringNames = ChampionshipSettingsService.GetRingNames().ToList();
            var counts = LoadMatchCountsByRing(selectedDayNumber, ringNames);
            var window = new RingDistributionWindow(selectedDayNumber, ringNames, counts);

            window.DistributionApplied += distribution =>
            {
                ApplyRingDistribution(selectedDayNumber, distribution);
                LoadRingFilter();
                LoadMatches();
            };

            window.Show(this);
        }
        catch (Exception ex)
        {
            MatchesSummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? $"Nie mozna otworzyc podzialu ringow: {ex.Message}"
                : $"Ring distribution could not be opened: {ex.Message}";
            StartupLogger.Log(ex, "MatchesWindow.DistributeRingsClick failed");
        }
    }

    private Dictionary<string, int> LoadMatchCountsByRing(int dayNumber, IReadOnlyList<string> ringNames)
    {
        var result = ringNames.ToDictionary(x => x, _ => 0, StringComparer.OrdinalIgnoreCase);
        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT IFNULL(RingName, ''), COUNT(*)
        FROM Matches
        WHERE ChampionshipId = @championshipId
          AND DayNumber = @dayNumber
        GROUP BY IFNULL(RingName, '')
        ";
        command.Parameters.AddWithValue("@championshipId", championshipId);
        command.Parameters.AddWithValue("@dayNumber", dayNumber);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var ringName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(ringName))
                result[ringName] = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
        }

        return result;
    }

    private void ApplyRingDistribution(int dayNumber, IReadOnlyDictionary<string, int> distribution)
    {
        var ringPlan = distribution
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
            .Select(x => (RingName: x.Key, Count: x.Value))
            .ToList();

        if (ringPlan.Count == 0)
        {
            MatchesSummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Wpisz liczbe walk dla co najmniej jednego ringu."
                : "Enter at least one ring match count.";
            return;
        }

        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();
        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var read = connection.CreateCommand();
        read.CommandText =
        @"
        SELECT Id
        FROM Matches
        WHERE ChampionshipId = @championshipId
          AND DayNumber = @dayNumber
        ORDER BY OrderNo, Id
        ";
        read.Parameters.AddWithValue("@championshipId", championshipId);
        read.Parameters.AddWithValue("@dayNumber", dayNumber);

        var matchIds = new List<int>();
        using (var reader = read.ExecuteReader())
        {
            while (reader.Read())
                matchIds.Add(reader.GetInt32(0));
        }

        var assignedCount = 0;
        foreach (var plan in ringPlan)
        {
            for (var i = 0; i < plan.Count && assignedCount < matchIds.Count; i++)
            {
                var update = connection.CreateCommand();
                update.CommandText =
                @"
                UPDATE Matches
                SET RingName = @ringName
                WHERE Id = @id
                ";
                update.Parameters.AddWithValue("@ringName", plan.RingName);
                update.Parameters.AddWithValue("@id", matchIds[assignedCount]);
                update.ExecuteNonQuery();
                assignedCount++;
            }
        }

        if (assignedCount < matchIds.Count)
        {
            var fallbackRingName = ringPlan[^1].RingName;
            while (assignedCount < matchIds.Count)
            {
                var update = connection.CreateCommand();
                update.CommandText =
                @"
                UPDATE Matches
                SET RingName = @ringName
                WHERE Id = @id
                ";
                update.Parameters.AddWithValue("@ringName", fallbackRingName);
                update.Parameters.AddWithValue("@id", matchIds[assignedCount]);
                update.ExecuteNonQuery();
                assignedCount++;
            }
        }

        NormalizeBoutNumbers();
        MatchesSummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? $"{matchIds.Count} walk rozdzielono dla dnia {dayNumber}."
            : $"{matchIds.Count} match(es) distributed for Day {dayNumber}.";
    }

    private bool IsCategoryAllowed(string division, string weightCategory, string gender)
    {
        if (activeCategories.Count == 0)
            return true;

        return activeCategories.Any(x =>
            string.Equals(x.Division, division, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.CategoryName, weightCategory, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Gender, gender, StringComparison.OrdinalIgnoreCase));
    }

    private void MakeMatches()
    {
        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();
        DeleteDayOneMatches(c);
        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();

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

                var ringName = ChampionshipSettingsService.ResolveRingName(f1.AgeCategory, f1.WeightCategory, f1.Gender, order - 1);
                SaveMatch(c, championshipId, f1, f2, order, g.Key, ringName);

                order++;
            }
        }
    }

    private void SaveMatch(
        SqliteConnection c,
        int championshipId,
        Fighter f1,
        Fighter? f2,
        int order,
        string group,
        string ringName)
    {
        int judges = 5;

        if (JudgesCombo.SelectedItem != null)
            judges = Convert.ToInt32(JudgesCombo.SelectedItem);

        var cmd = c.CreateCommand();

        cmd.CommandText =
        @"
        INSERT INTO Matches
        (
        ChampionshipId,
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
        DayNumber,
        RingName
        )
        VALUES
        (
        @championshipId,
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
        1,
        @ringName
        )
        ";

        cmd.Parameters.AddWithValue("@championshipId", championshipId);
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
        cmd.Parameters.AddWithValue("@ringName", ringName);

        cmd.ExecuteNonQuery();
    }

    private bool CanRebuildOpeningRound(out string reason)
    {
        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();
        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();

        var hasProgressedTournament = connection.CreateCommand();
        hasProgressedTournament.CommandText =
        @"
        SELECT EXISTS (
            SELECT 1
            FROM Matches
            WHERE ChampionshipId = @championshipId
              AND DayNumber > 1
        )
        ";
        hasProgressedTournament.Parameters.AddWithValue("@championshipId", championshipId);

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
            WHERE m.ChampionshipId = @championshipId
              AND m.DayNumber = 1
        )
        ";
        hasCompletedDayOneMatches.Parameters.AddWithValue("@championshipId", championshipId);

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
        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();
        var matchIds = new List<int>();

        var read = connection.CreateCommand();
        read.CommandText =
        @"
        SELECT Id
        FROM Matches
        WHERE ChampionshipId = @championshipId
          AND DayNumber = 1
        ";
        read.Parameters.AddWithValue("@championshipId", championshipId);

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
        deleteMatches.CommandText = "DELETE FROM Matches WHERE ChampionshipId = @championshipId AND DayNumber = 1";
        deleteMatches.Parameters.AddWithValue("@championshipId", championshipId);
        deleteMatches.ExecuteNonQuery();
    }

    private void DayChanged(object? sender, SelectionChangedEventArgs e)
    {
        selectedDayNumber = ParseSelectedDayNumber();
        LoadMatches();
    }

    private void RingChanged(object? sender, SelectionChangedEventArgs e)
    {
        selectedRingName = RingFilterCombo.SelectedItem?.ToString() ?? LocalizationService.T("All");
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

    private void LoadRingFilter()
    {
        var allLabel = LocalizationService.T("All");
        var rings = new List<string> { allLabel };
        rings.AddRange(ChampionshipSettingsService.GetRingNames());
        RingFilterCombo.ItemsSource = rings;

        if (IsAllRingsSelected())
            selectedRingName = allLabel;
        else if (!rings.Contains(selectedRingName))
            selectedRingName = allLabel;

        RingFilterCombo.SelectedItem = selectedRingName;
    }

    private bool IsAllRingsSelected()
    {
        return string.Equals(selectedRingName, "All", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(selectedRingName, LocalizationService.T("All"), StringComparison.OrdinalIgnoreCase);
    }

    private void ScoreClick(object? sender, RoutedEventArgs e)
    {
        var m = MatchesGrid.SelectedItem as Match;

        if (m == null)
        {
            MatchesSummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Najpierw wybierz wiersz walki albo uzyj przycisku Punktacja w wierszu walki."
                : "Select a match row first, or use the Score button on the match row.";
            return;
        }

        OpenScoreWindow(m);
    }

    private void ScoreRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not Match match)
        {
            MatchesSummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Nie mozna otworzyc tablicy wynikow: nie znaleziono wiersza walki."
                : "Scoreboard could not be opened: match row was not found.";
            return;
        }

        MatchesGrid.SelectedItem = match;
        OpenScoreWindow(match);
    }

    private void OpenScoreWindow(Match match)
    {
        if (string.Equals(match.Fighter1Name, "BYE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(match.Fighter2Name, "BYE", StringComparison.OrdinalIgnoreCase))
        {
            MatchesSummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Nie mozna otworzyc tablicy wynikow dla walki BYE."
                : "Scoreboard cannot be opened for a BYE match.";
            return;
        }

        try
        {
            var window = new ScoreWindow(match);
            window.Show(this);
            window.Activate();
        }
        catch (Exception ex)
        {
            MatchesSummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? $"Nie mozna otworzyc tablicy wynikow: {ex.Message}"
                : $"Scoreboard could not be opened: {ex.Message}";
            StartupLogger.Log(ex, "MatchesWindow.OpenScoreWindow failed");
        }
    }
}
