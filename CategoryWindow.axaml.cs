using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MuaythaiApp.Database;

namespace MuaythaiApp;

public partial class CategoryWindow : Window
{
    private readonly List<Category> allCategories = new();
    private readonly HashSet<int> activeCategoryIds = new();
    private readonly DatabaseAutoRefresh? databaseAutoRefresh;

    public CategoryWindow()
    {
        InitializeComponent();

        if (!RemoteApiClient.IsEnabled)
            databaseAutoRefresh = new DatabaseAutoRefresh(this, ReloadCategories);

        LocalizationService.LocalizeControlTree(this);
        Opened += async (_, __) => await ReloadCategoriesAsync();
    }

    private void ReloadCategories()
    {
        allCategories.Clear();
        allCategories.AddRange(LoadCategoriesCore());
        LoadActiveCategorySelection();
        LoadFilters();
        ApplyFilters();
    }

    private async Task ReloadCategoriesAsync()
    {
        StartupLogger.Log("CategoryWindow.ReloadCategoriesAsync started");
        SummaryText.Text = "Loading categories...";

        try
        {
            var categories = await Task.Run(LoadCategoriesCore);
            allCategories.Clear();
            allCategories.AddRange(categories);
            LoadActiveCategorySelection();
            LoadFilters();
            ApplyFilters();
            StartupLogger.Log($"CategoryWindow.ReloadCategoriesAsync completed with {categories.Count} categories");
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Categories could not be loaded: {ex.Message}";
            StartupLogger.Log(ex, "CategoryWindow.ReloadCategoriesAsync failed");
        }
    }

    private List<Category> LoadCategoriesCore()
    {
        var categories = new List<Category>();

        if (RemoteApiClient.IsEnabled)
        {
            var remoteCategories = RemoteApiClient.GetCategories()
                .OrderBy(x => x.Division)
                .ThenBy(x => x.Gender)
                .ThenBy(x => x.SortOrder)
                .ToList();

            if (remoteCategories.Count > 0)
                return remoteCategories;

            StartupLogger.Log("CategoryWindow.LoadCategoriesCore remote API returned 0 categories, using built-in defaults");
            return BuildFallbackCategories();
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
            categories.Add(new Category
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

        return categories;
    }

    private List<Category> BuildFallbackCategories()
    {
        var categories = new List<Category>();

        AddFallbackDivision(categories, "U12", 10, 11, "Male",
            30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50, 52, 54, 56, 58, 60, 63.5, 67);
        AddFallbackDivision(categories, "U12", 10, 11, "Female",
            30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50, 52, 54, 56, 58, 60);

        AddFallbackDivision(categories, "U14", 12, 13, "Male",
            32, 34, 36, 38, 40, 42, 44, 46, 48, 50, 52, 54, 56, 58, 60, 63.5, 67, 71);
        AddFallbackDivision(categories, "U14", 12, 13, "Female",
            32, 34, 36, 38, 40, 42, 44, 46, 48, 50, 52, 54, 56, 58, 60, 63.5);

        AddFallbackDivision(categories, "U16", 14, 15, "Male",
            38, 40, 42, 45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75, 81);
        AddFallbackDivision(categories, "U16", 14, 15, "Female",
            36, 38, 40, 42, 45, 48, 51, 54, 57, 60, 63.5, 67, 71);

        AddFallbackDivision(categories, "U18", 16, 17, "Male",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75, 81, 86, 91);
        AddFallbackDivision(categories, "U18", 16, 17, "Female",
            42, 45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75);

        AddFallbackDivision(categories, "U24", 18, 23, "Male",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75, 81, 86, 91);
        AddFallbackDivision(categories, "U24", 18, 23, "Female",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75);

        AddFallbackDivision(categories, "Senior", 18, 40, "Male",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75, 81, 86, 91);
        AddFallbackDivision(categories, "Senior", 18, 40, "Female",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75);

        AddFallbackDivision(categories, "Masters", 41, 55, "Male",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75, 81, 86, 91);
        AddFallbackDivision(categories, "Masters", 41, 55, "Female",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75);

        return categories;
    }

    private void AddFallbackDivision(
        List<Category> categories,
        string division,
        int ageMin,
        int ageMax,
        string gender,
        params double[] weights)
    {
        var (roundCount, roundDurationSeconds, breakDurationSeconds) = GetFallbackRoundRule(division);

        for (int i = 0; i < weights.Length; i++)
        {
            var weight = weights[i];
            categories.Add(new Category
            {
                Division = division,
                Gender = gender,
                AgeMin = ageMin,
                AgeMax = ageMax,
                WeightMax = weight,
                IsOpenWeight = false,
                SortOrder = i + 1,
                CategoryName = $"-{FormatFallbackWeight(weight)} kg",
                RoundCount = roundCount,
                RoundDurationSeconds = roundDurationSeconds,
                BreakDurationSeconds = breakDurationSeconds
            });
        }

        if (weights.Length == 0)
            return;

        var lastWeight = weights[^1];
        categories.Add(new Category
        {
            Division = division,
            Gender = gender,
            AgeMin = ageMin,
            AgeMax = ageMax,
            WeightMax = lastWeight,
            IsOpenWeight = true,
            SortOrder = weights.Length + 1,
            CategoryName = $"+{FormatFallbackWeight(lastWeight)} kg",
            RoundCount = roundCount,
            RoundDurationSeconds = roundDurationSeconds,
            BreakDurationSeconds = breakDurationSeconds
        });
    }

    private static (int RoundCount, int RoundDurationSeconds, int BreakDurationSeconds) GetFallbackRoundRule(string division)
    {
        return division switch
        {
            "U24" => (3, 180, 60),
            "Senior" => (3, 180, 60),
            "Masters" => (3, 120, 60),
            "U18" => (3, 120, 60),
            "U16" => (3, 120, 60),
            "U14" => (3, 90, 60),
            "U12" => (3, 60, 60),
            _ => (3, 120, 60)
        };
    }

    private static string FormatFallbackWeight(double value)
        => value % 1 == 0 ? value.ToString("0") : value.ToString("0.##");

    private void LoadFilters()
    {
        var divisions = new List<string> { "All" };
        divisions.AddRange(allCategories.Select(x => x.Division).Distinct().OrderBy(x => x));

        var genders = new List<string> { "All" };
        genders.AddRange(allCategories.Select(x => x.Gender).Distinct().OrderBy(x => x));

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

        IEnumerable<Category> filtered = allCategories;

        if (division != "All")
            filtered = filtered.Where(x => x.Division == division);

        if (gender != "All")
            filtered = filtered.Where(x => x.Gender == gender);

        var result = filtered
            .OrderBy(x => x.AgeMin)
            .ThenBy(x => x.Division)
            .ThenBy(x => x.Gender)
            .ThenBy(x => x.SortOrder)
            .ToList();

        CategoryList.ItemsSource = null;
        CategoryList.ItemsSource = result;

        SummaryText.Text = $"{result.Count} categories listed";
    }

    private void LoadActiveCategorySelection()
    {
        activeCategoryIds.Clear();
        var settings = ChampionshipSettingsService.Load();

        if (settings.ActiveCategoryIds.Count == 0)
        {
            foreach (var category in allCategories.Where(x => x.Id > 0))
                activeCategoryIds.Add(category.Id);
        }
        else
        {
            foreach (var categoryId in settings.ActiveCategoryIds)
                activeCategoryIds.Add(categoryId);
        }

        foreach (var category in allCategories)
            category.IsActive = category.Id > 0 && activeCategoryIds.Contains(category.Id);
    }

    private void ActiveCategoryChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.DataContext is not Category category || category.Id <= 0)
            return;

        category.IsActive = checkBox.IsChecked == true;
        if (category.IsActive)
            activeCategoryIds.Add(category.Id);
        else
            activeCategoryIds.Remove(category.Id);

        SummaryText.Text = $"{activeCategoryIds.Count} active categories selected";
    }

    private void SaveActiveCategoriesClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            RebuildActiveCategoryIdsFromRows();
            ChampionshipSettingsService.SaveActiveCategoryIds(activeCategoryIds);
            SummaryText.Text = $"{activeCategoryIds.Count} active categories saved";
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Active categories could not be saved: {ex.Message}";
            StartupLogger.Log(ex, "CategoryWindow.SaveActiveCategoriesClick failed");
        }
    }

    private void RebuildActiveCategoryIdsFromRows()
    {
        activeCategoryIds.Clear();

        foreach (var category in allCategories.Where(x => x.Id > 0 && x.IsActive))
            activeCategoryIds.Add(category.Id);
    }
}
