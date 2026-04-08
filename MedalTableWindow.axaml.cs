using Avalonia.Controls;
using MuaythaiApp.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MuaythaiApp;

public partial class MedalTableWindow : Window
{
    private List<MedalAward> allAwards = new();
    private readonly DatabaseAutoRefresh databaseAutoRefresh;

    public MedalTableWindow()
    {
        InitializeComponent();
        databaseAutoRefresh = new DatabaseAutoRefresh(this, LoadMedalTable);
        Opened += (_, __) => LoadMedalTable();
        Activated += (_, __) => LoadMedalTable();
        LocalizationService.LanguageChanged += ApplyLocalization;
        Closed += (_, __) => LocalizationService.LanguageChanged -= ApplyLocalization;
        ApplyLocalization();
    }

    private void LoadMedalTable()
    {
        allAwards = MedalTableService.BuildAwards();
        LoadFilters();
        ApplyFilters();
    }

    private void LoadFilters()
    {
        var categories = allAwards
            .Select(x => MedalTableService.BuildCategoryLabel(x.Category, x.WeightClass, x.Gender))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        categories.Insert(0, "All Categories");
        CategoryFilterCombo.ItemsSource = categories;

        if (CategoryFilterCombo.SelectedItem == null)
            CategoryFilterCombo.SelectedIndex = 0;

        var clubs = allAwards
            .Select(x => x.ClubName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        clubs.Insert(0, "All Clubs");
        ClubFilterCombo.ItemsSource = clubs;

        if (ClubFilterCombo.SelectedItem == null)
            ClubFilterCombo.SelectedIndex = 0;
    }

    private void FilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<MedalAward> query = allAwards;
        var selectedCategory = CategoryFilterCombo.SelectedItem?.ToString();
        var selectedClub = ClubFilterCombo.SelectedItem?.ToString();

        if (!string.IsNullOrWhiteSpace(selectedCategory) &&
            !string.Equals(selectedCategory, "All Categories", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x =>
                string.Equals(
                    MedalTableService.BuildCategoryLabel(x.Category, x.WeightClass, x.Gender),
                    selectedCategory,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(selectedClub) &&
            !string.Equals(selectedClub, "All Clubs", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x =>
                string.Equals(x.ClubName, selectedClub, StringComparison.OrdinalIgnoreCase));
        }

        var awards = query
            .OrderBy(x => x.Category)
            .ThenBy(x => x.WeightClass)
            .ThenBy(x => x.Medal)
            .ThenBy(x => x.FighterName)
            .ToList();

        var standings = awards
            .GroupBy(x => x.ClubName)
            .Select(group => new MedalStanding
            {
                ClubName = group.Key,
                Gold = group.Count(x => x.Medal == "Gold"),
                Silver = group.Count(x => x.Medal == "Silver"),
                Bronze = group.Count(x => x.Medal == "Bronze")
            })
            .OrderByDescending(x => x.Gold)
            .ThenByDescending(x => x.Silver)
            .ThenByDescending(x => x.Bronze)
            .ThenBy(x => x.ClubName)
            .ToList();

        StandingsList.ItemsSource = standings;
        AwardsList.ItemsSource = awards;

        var completedCategories = awards
            .Select(x => MedalTableService.BuildCategoryLabel(x.Category, x.WeightClass, x.Gender))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        SummaryText.Text = $"{standings.Count} clubs listed | {awards.Count} medals awarded | {completedCategories} completed categories";
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.T("MedalTable");
        MedalTableTitleText.Text = LocalizationService.T("MedalTable");
        MedalCategoryLabelText.Text = LocalizationService.T("Category");
        MedalClubLabelText.Text = LocalizationService.T("Club");
        StandingsClubHeaderText.Text = LocalizationService.T("Club");
        GoldHeaderText.Text = LocalizationService.T("Gold");
        SilverHeaderText.Text = LocalizationService.T("Silver");
        BronzeHeaderText.Text = LocalizationService.T("Bronze");
        MedalTotalHeaderText.Text = LocalizationService.T("Total");
        MedalHeaderText.Text = LocalizationService.T("Medal");
        FighterHeaderText.Text = LocalizationService.T("Fighter");
        AwardClubHeaderText.Text = LocalizationService.T("Club");
        AwardCategoryHeaderText.Text = LocalizationService.T("Category");
        AwardWeightHeaderText.Text = LocalizationService.T("Weight");
        AwardGenderHeaderText.Text = LocalizationService.T("Gender");
    }
}
