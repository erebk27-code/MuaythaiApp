using Avalonia.Controls;
using Microsoft.Data.Sqlite;
using MuaythaiApp.Database;
using System.Collections.Generic;
using System.Linq;

namespace MuaythaiApp;

public partial class FightResultsWindow : Window
{
    private readonly List<FightResult> allResults = new();
    private readonly DatabaseAutoRefresh databaseAutoRefresh;
    private int selectedDayNumber = 1;

    public FightResultsWindow()
    {
        InitializeComponent();
        databaseAutoRefresh = new DatabaseAutoRefresh(this, LoadResults);
        DayFilterCombo.ItemsSource = Enumerable.Range(1, 4)
            .Select(day => $"Day {day}")
            .ToList();
        DayFilterCombo.SelectedIndex = 0;
        Opened += (_, __) => LoadResults();
        Activated += (_, __) => LoadResults();
        LocalizationService.LanguageChanged += ApplyLocalization;
        Closed += (_, __) => LocalizationService.LanguageChanged -= ApplyLocalization;
        ApplyLocalization();
    }

    private void LoadResults()
    {
        allResults.Clear();

        using var c = DatabaseHelper.CreateConnection();
        c.Open();

        var cmd = c.CreateCommand();
        cmd.CommandText =
        @"
        SELECT
            MatchResult.MatchId,
            Matches.DayNumber,
            Matches.OrderNo,
            Matches.Fighter1Name,
            Matches.Fighter2Name,
            MatchResult.Winner,
            Matches.AgeCategory,
            Matches.WeightCategory,
            MatchResult.Method,
            MatchResult.Round
        FROM MatchResult
        INNER JOIN Matches
            ON MatchResult.MatchId = Matches.Id
        ORDER BY Matches.OrderNo, MatchResult.MatchId
        ";

        using var r = cmd.ExecuteReader();

        while (r.Read())
        {
            var winnerSide = r.IsDBNull(5) ? string.Empty : r.GetString(5);
            var redName = r.IsDBNull(3) ? string.Empty : r.GetString(3);
            var blueName = r.IsDBNull(4) ? string.Empty : r.GetString(4);

            allResults.Add(new FightResult
            {
                MatchId = r.GetInt32(0),
                DayNumber = r.IsDBNull(1) ? 1 : r.GetInt32(1),
                BoutNo = r.IsDBNull(2) ? 0 : r.GetInt32(2),
                RedName = redName,
                BlueName = blueName,
                WinnerSide = winnerSide,
                WinnerName = winnerSide == "Red"
                    ? redName
                    : winnerSide == "Blue"
                        ? blueName
                        : "Tie",
                Category = r.IsDBNull(6) ? string.Empty : r.GetString(6),
                WeightClass = r.IsDBNull(7) ? string.Empty : r.GetString(7),
                Method = r.IsDBNull(8) ? string.Empty : r.GetString(8),
                Round = r.IsDBNull(9) ? 0 : r.GetInt32(9)
            });
        }

        ApplySearch();
    }

    private void DayChanged(object? sender, SelectionChangedEventArgs e)
    {
        selectedDayNumber = ParseSelectedDayNumber();
        ApplySearch();
    }

    private void SearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySearch();
    }

    private void ApplySearch()
    {
        var search = SearchBox.Text?.Trim().ToLowerInvariant();
        IEnumerable<FightResult> query = allResults;

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.WinnerName.ToLowerInvariant().Contains(search) ||
                x.RedName.ToLowerInvariant().Contains(search) ||
                x.BlueName.ToLowerInvariant().Contains(search) ||
                x.Category.ToLowerInvariant().Contains(search) ||
                x.WeightClass.ToLowerInvariant().Contains(search));
        }

        query = query.Where(x => x.DayNumber == selectedDayNumber);

        var result = query
            .OrderBy(x => x.DayNumber)
            .ThenBy(x => x.BoutNo)
            .ThenBy(x => x.MatchId)
            .ToList();

        ResultsList.ItemsSource = null;
        ResultsList.ItemsSource = result;
        SummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? $"Wyswietlono {result.Count} zwyciezcow dla dnia {selectedDayNumber}"
            : $"{result.Count} winners listed for Day {selectedDayNumber}";
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.T("FightResults");
        FightResultsTitleText.Text = LocalizationService.T("FightResults");
        DayFilterLabelText.Text = LocalizationService.T("Day");
        SearchWinnerLabelText.Text = LocalizationService.T("SearchWinner");
        ResultDayHeaderText.Text = LocalizationService.T("Day");
        ResultBoutHeaderText.Text = LocalizationService.T("Bout");
        ResultRedHeaderText.Text = LocalizationService.T("Red");
        ResultBlueHeaderText.Text = LocalizationService.T("Blue");
        ResultWinnerHeaderText.Text = LocalizationService.T("Winner");
        ResultCategoryHeaderText.Text = LocalizationService.T("Category");
        ResultWeightHeaderText.Text = LocalizationService.T("Weight");
        ResultMethodHeaderText.Text = LocalizationService.T("Method");
        ResultRoundHeaderText.Text = LocalizationService.T("Round");
        DayFilterCombo.ItemsSource = Enumerable.Range(1, 4)
            .Select(day => $"{LocalizationService.T("Day")} {day}")
            .ToList();
        DayFilterCombo.SelectedIndex = selectedDayNumber - 1;
    }

    private int ParseSelectedDayNumber()
    {
        var text = DayFilterCombo.SelectedItem?.ToString();

        if (!string.IsNullOrWhiteSpace(text) &&
            text.StartsWith("Day ", System.StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(text[4..], out var dayNumber))
        {
            return dayNumber;
        }

        return 1;
    }
}
