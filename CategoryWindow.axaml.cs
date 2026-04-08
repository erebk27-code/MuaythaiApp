using Avalonia.Controls;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;
using MuaythaiApp.Database;

namespace MuaythaiApp;

public partial class CategoryWindow : Window
{
    private readonly List<Category> allCategories = new();
    private readonly List<CategorySummary> allSummaries = new();
    private readonly DatabaseAutoRefresh databaseAutoRefresh;

    public CategoryWindow()
    {
        InitializeComponent();
        databaseAutoRefresh = new DatabaseAutoRefresh(this, ReloadCategories);

        Opened += (_, __) =>
        {
            ReloadCategories();
        };
    }

    private void ReloadCategories()
    {
        LoadCategories();
        LoadFilters();
        ApplyFilters();
    }

    private void LoadCategories()
    {
        allCategories.Clear();

        if (RemoteApiClient.IsEnabled)
        {
            allCategories.AddRange(RemoteApiClient.GetCategories()
                .OrderBy(x => x.Division)
                .ThenBy(x => x.Gender)
                .ThenBy(x => x.SortOrder));
            return;
        }

        using var connection =
            DatabaseHelper.CreateConnection();

        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
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
        ORDER BY Division, Gender, SortOrder
        ";

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            allCategories.Add(new Category
            {
                Id = reader.GetInt32(0),
                Division = reader.GetString(1),
                Gender = reader.GetString(2),
                AgeMin = reader.GetInt32(3),
                AgeMax = reader.GetInt32(4),
                WeightMax = reader.GetDouble(5),
                IsOpenWeight = reader.GetInt32(6) == 1,
                SortOrder = reader.GetInt32(7),
                CategoryName = reader.GetString(8),
                RoundCount = reader.GetInt32(9),
                RoundDurationSeconds = reader.GetInt32(10),
                BreakDurationSeconds = reader.GetInt32(11)
            });
        }
    }

    private void LoadFilters()
    {
        BuildSummaries();

        var divisions = new List<string> { "All" };
        divisions.AddRange(allSummaries.Select(x => x.Division).Distinct().OrderBy(x => x));

        var genders = new List<string> { "All" };
        genders.AddRange(allSummaries.Select(x => x.Gender).Distinct().OrderBy(x => x));

        DivisionCombo.ItemsSource = divisions;
        DivisionCombo.SelectedIndex = 0;

        GenderCombo.ItemsSource = genders;
        GenderCombo.SelectedIndex = 0;
    }

    private void FilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var division = DivisionCombo.SelectedItem?.ToString() ?? "All";
        var gender = GenderCombo.SelectedItem?.ToString() ?? "All";

        IEnumerable<CategorySummary> filtered = allSummaries;

        if (division != "All")
            filtered = filtered.Where(x => x.Division == division);

        if (gender != "All")
            filtered = filtered.Where(x => x.Gender == gender);

        var result = filtered
            .OrderBy(x => x.Division)
            .ThenBy(x => x.Gender)
            .ToList();

        CategoryList.ItemsSource = null;
        CategoryList.ItemsSource = result;

        SummaryText.Text = $"{result.Count} category groups";
    }

    private void BuildSummaries()
    {
        allSummaries.Clear();

        var summaries = allCategories
            .GroupBy(x => new
            {
                x.Division,
                x.Gender,
                x.AgeMin,
                x.AgeMax,
                x.RoundCount,
                x.RoundDurationSeconds,
                x.BreakDurationSeconds
            })
            .Select(group =>
            {
                var closedWeights = group
                    .Where(x => !x.IsOpenWeight)
                    .Select(x => x.WeightMax)
                    .OrderBy(x => x)
                    .ToList();

                var openWeight = group
                    .Where(x => x.IsOpenWeight)
                    .Select(x => x.WeightMax)
                    .DefaultIfEmpty(closedWeights.LastOrDefault())
                    .Max();

                return new CategorySummary
                {
                    Division = group.Key.Division,
                    Gender = group.Key.Gender,
                    AgeMin = group.Key.AgeMin,
                    AgeMax = group.Key.AgeMax,
                    MinWeight = closedWeights.FirstOrDefault(),
                    MaxWeight = openWeight,
                    HasOpenWeight = group.Any(x => x.IsOpenWeight),
                    WeightClassCount = group.Count(),
                    RoundCount = group.Key.RoundCount,
                    RoundDurationSeconds = group.Key.RoundDurationSeconds,
                    BreakDurationSeconds = group.Key.BreakDurationSeconds
                };
            })
            .OrderBy(x => x.Division)
            .ThenBy(x => x.Gender);

        allSummaries.AddRange(summaries);
    }
}
