using Avalonia.Controls;
using Avalonia.Interactivity;
using MuaythaiApp.Database;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace MuaythaiApp;

public partial class ChampionshipSettingsWindow : Window
{
    private readonly Action onSaved;
    private readonly bool showInformationSection;
    private readonly bool showRingSection;
    private readonly List<ChampionshipListItem> championships = new();
    private ChampionshipSettings loadedSettings = new();
    private readonly List<ChampionshipRingDefinition> ringDefinitions = new();
    private readonly HashSet<int> activeCategoryIds = new();
    private int availableCategoryCount;
    private bool isBindingChampionshipSelector;
    private bool isBindingRingForm;

    public ChampionshipSettingsWindow()
        : this(() => { })
    {
    }

    public ChampionshipSettingsWindow(
        Action onSaved,
        bool showInformationSection = true,
        bool showRingSection = true)
    {
        InitializeComponent();
        this.onSaved = onSaved;
        this.showInformationSection = showInformationSection;
        this.showRingSection = showRingSection;
        Opened += async (_, __) => await LoadDataAsync();
        LocalizationService.LanguageChanged += ApplyLocalization;
        Closed += (_, __) => LocalizationService.LanguageChanged -= ApplyLocalization;
        ChampionshipSettingsService.SettingsChanged += ChampionshipSettingsChanged;
        Closed += (_, __) => ChampionshipSettingsService.SettingsChanged -= ChampionshipSettingsChanged;
        ApplyLocalization();
    }

    private async void ChampionshipSettingsChanged()
    {
        if (!showRingSection)
            return;

        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        SummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? "Ladowanie ustawien mistrzostw..."
            : "Loading championship settings...";

        try
        {
            var activeId = await Task.Run(ChampionshipSettingsService.GetOrCreateActiveChampionshipId);
            var allChampionships = await Task.Run(() => ChampionshipSettingsService.GetChampionships().ToList());
            loadedSettings = await Task.Run(() => ChampionshipSettingsService.Load(activeId));

            championships.Clear();
            championships.AddRange(allChampionships);
            BindChampionships(activeId);

            ChampionshipNameBox.Text = loadedSettings.ChampionshipName;
            ChampionshipAddressBox.Text = loadedSettings.ChampionshipAddress;
            StartDateBox.Text = loadedSettings.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
            EndDateBox.Text = loadedSettings.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

            ringDefinitions.Clear();
            ringDefinitions.AddRange(loadedSettings.RingDefinitions.Count > 0
                ? loadedSettings.RingDefinitions.Select(CloneRingDefinition)
                : new[] { new ChampionshipRingDefinition { RingName = "RING A" } });

            activeCategoryIds.Clear();
            foreach (var categoryId in loadedSettings.ActiveCategoryIds)
                activeCategoryIds.Add(categoryId);

            BindRingDefinitions();
            await LoadCategoriesAsync();
            UpdateSummary();
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Championship settings could not be loaded: {ex.Message}";
            StartupLogger.Log(ex, "ChampionshipSettingsWindow.LoadData failed");
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await Task.Run(LoadCategoriesCore);
            availableCategoryCount = categories.Count(x => x.Id > 0);
            if (activeCategoryIds.Count == 0)
            {
                foreach (var category in categories)
                {
                    if (category.Id > 0)
                        activeCategoryIds.Add(category.Id);
                }
            }

            CategorySelectionPanel.Children.Clear();

            foreach (var category in categories)
            {
                var checkBox = new CheckBox
                {
                    Content = category.DisplayName,
                    IsChecked = activeCategoryIds.Contains(category.Id),
                    Tag = category.Id
                };
                checkBox.Checked += CategorySelectionChanged;
                checkBox.Unchecked += CategorySelectionChanged;
                CategorySelectionPanel.Children.Add(checkBox);
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log(ex, "ChampionshipSettingsWindow.LoadCategoriesAsync failed");
        }
    }

    private List<ChampionshipCategoryOption> LoadCategoriesCore()
    {
        List<Category> categories;

        if (RemoteApiClient.IsEnabled)
        {
            categories = RemoteApiClient.GetCategories()
                .OrderBy(x => x.AgeMin)
                .ThenBy(x => x.AgeMax)
                .ThenBy(x => x.SortOrder)
                .ToList();
        }
        else
        {
            categories = new List<Category>();

            using var connection = DatabaseHelper.CreateConnection();
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
            ORDER BY AgeMin, AgeMax, SortOrder
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
        }

        return categories
            .Select(x => new ChampionshipCategoryOption
            {
                Id = x.Id,
                Division = x.DivisionWithAgeDisplay,
                Gender = x.Gender,
                CategoryName = x.CategoryDisplay,
                IsActive = activeCategoryIds.Count == 0 || activeCategoryIds.Contains(x.Id)
            })
            .ToList();
    }

    private void SaveClick(object? sender, RoutedEventArgs e)
    {
        if (!TryBuildSettings(out var settings, out var error))
        {
            SummaryText.Text = error;
            return;
        }

        try
        {
            settings.Id = loadedSettings.Id;
            ChampionshipSettingsService.Save(settings);
            SummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Ustawienia mistrzostw zostaly zapisane."
                : "Championship settings saved.";
            onSaved();
            _ = LoadDataAsync();
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Championship settings could not be saved: {ex.Message}";
            StartupLogger.Log(ex, "ChampionshipSettingsWindow.SaveClick failed");
        }
    }

    private bool TryBuildSettings(out ChampionshipSettings settings, out string error)
    {
        settings = new ChampionshipSettings();
        error = string.Empty;

        var name = showInformationSection
            ? ChampionshipNameBox.Text?.Trim() ?? string.Empty
            : loadedSettings.ChampionshipName;

        if (string.IsNullOrWhiteSpace(name))
        {
            error = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Wpisz nazwe mistrzostw."
                : "Enter a championship name.";
            return false;
        }

        DateTime? startDate;
        DateTime? endDate;

        if (showInformationSection)
        {
            if (!TryParseDate(StartDateBox.Text, out startDate, allowEmpty: true, out error))
                return false;

            if (!TryParseDate(EndDateBox.Text, out endDate, allowEmpty: true, out error))
                return false;
        }
        else
        {
            startDate = loadedSettings.StartDate;
            endDate = loadedSettings.EndDate;
        }

        if (startDate.HasValue && endDate.HasValue && endDate < startDate)
        {
            error = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Data zakonczenia nie moze byc wczesniejsza niz data rozpoczecia."
                : "End date cannot be earlier than start date.";
            return false;
        }

        var sanitizedRingDefinitions = showRingSection
            ? BuildRingDefinitions()
            : loadedSettings.RingDefinitions.Select(CloneRingDefinition).ToList();

        if (sanitizedRingDefinitions.Count == 0)
            sanitizedRingDefinitions.Add(new ChampionshipRingDefinition { RingName = "RING A" });

        settings = new ChampionshipSettings
        {
            ChampionshipName = name,
            ChampionshipAddress = showInformationSection
                ? ChampionshipAddressBox.Text?.Trim() ?? string.Empty
                : loadedSettings.ChampionshipAddress,
            StartDate = startDate,
            EndDate = endDate,
            RingNames = sanitizedRingDefinitions.Select(x => x.RingName).ToList(),
            RingDefinitions = sanitizedRingDefinitions,
            ActiveCategoryIds = showRingSection
                ? new HashSet<int>(activeCategoryIds)
                : new HashSet<int>(loadedSettings.ActiveCategoryIds)
        };

        return true;
    }

    private List<ChampionshipRingDefinition> BuildRingDefinitions()
    {
        return ringDefinitions
            .Select(CloneRingDefinition)
            .Where(x => !string.IsNullOrWhiteSpace(x.RingName))
            .GroupBy(x => x.RingName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x =>
            {
                var definition = x.First();
                definition.RingName = definition.RingName.Trim();
                definition.JudgesText = definition.JudgesText?.Trim() ?? string.Empty;
                definition.DivisionNamesText = definition.DivisionNamesText?.Trim() ?? string.Empty;
                definition.GendersText = definition.GendersText?.Trim() ?? string.Empty;
                return definition;
            })
            .ToList();
    }

    private bool TryParseDate(string? value, out DateTime? date, bool allowEmpty, out string error)
    {
        error = string.Empty;
        date = null;

        if (string.IsNullOrWhiteSpace(value))
            return allowEmpty;

        if (DateTime.TryParseExact(
                value.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            date = parsed.Date;
            return true;
        }

        error = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? "Tarih formatini yyyy-MM-dd olarak girin."
            : "Use yyyy-MM-dd date format.";
        return false;
    }

    private void FormChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateSummary();
    }

    private void RingDefinitionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        BindSelectedRing();
    }

    private void AddRingClick(object? sender, RoutedEventArgs e)
    {
        var nextIndex = ringDefinitions.Count + 1;
        ringDefinitions.Add(new ChampionshipRingDefinition
        {
            RingName = $"RING {nextIndex}"
        });

        BindRingDefinitions(ringDefinitions.Count - 1);
        UpdateSummary();
    }

    private void RemoveRingClick(object? sender, RoutedEventArgs e)
    {
        var selectedIndex = RingDefinitionCombo.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= ringDefinitions.Count)
            return;

        ringDefinitions.RemoveAt(selectedIndex);

        if (ringDefinitions.Count == 0)
            ringDefinitions.Add(new ChampionshipRingDefinition { RingName = "RING A" });

        BindRingDefinitions(Math.Min(selectedIndex, ringDefinitions.Count - 1));
        UpdateSummary();
    }

    private void RingFieldChanged(object? sender, TextChangedEventArgs e)
    {
        if (isBindingRingForm)
            return;

        var selectedIndex = RingDefinitionCombo.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= ringDefinitions.Count)
            return;

        var definition = ringDefinitions[selectedIndex];
        definition.RingName = RingNameBox.Text?.Trim() ?? string.Empty;
        definition.JudgesText = JudgesBox.Text?.Trim() ?? string.Empty;
        definition.DivisionNamesText = DivisionNamesBox.Text?.Trim() ?? string.Empty;
        definition.GendersText = GendersBox.Text?.Trim() ?? string.Empty;

        UpdateSummary();
    }

    private void CategorySelectionChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.Tag is not int categoryId)
            return;

        if (checkBox.IsChecked == true)
            activeCategoryIds.Add(categoryId);
        else
            activeCategoryIds.Remove(categoryId);

        UpdateSummary();
    }

    private void BindChampionships(int activeChampionshipId)
    {
        isBindingChampionshipSelector = true;
        ChampionshipSelectorCombo.ItemsSource = null;
        ChampionshipSelectorCombo.ItemsSource = championships.Select(x => x.DisplayName).ToList();

        var selectedIndex = championships.FindIndex(x => x.Id == activeChampionshipId);
        ChampionshipSelectorCombo.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        isBindingChampionshipSelector = false;
    }

    private async void ChampionshipSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (isBindingChampionshipSelector)
            return;

        var selectedIndex = ChampionshipSelectorCombo.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= championships.Count)
            return;

        ChampionshipSettingsService.SetActiveChampionship(championships[selectedIndex].Id);
        await LoadDataAsync();
        onSaved();
    }

    private async void CreateChampionshipClick(object? sender, RoutedEventArgs e)
    {
        var name = NewChampionshipNameBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            SummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Wpisz nazwe nowego mistrzostwa."
                : "Enter a new championship name.";
            return;
        }

        try
        {
            ChampionshipSettingsService.CreateChampionship(name);
            NewChampionshipNameBox.Text = string.Empty;
            await LoadDataAsync();
            onSaved();
            SummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Nowe mistrzostwa zostaly utworzone."
                : "New championship created.";
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Championship could not be created: {ex.Message}";
            StartupLogger.Log(ex, "ChampionshipSettingsWindow.CreateChampionshipClick failed");
        }
    }

    private void BindRingDefinitions(int selectedIndex = 0)
    {
        RingDefinitionCombo.ItemsSource = null;
        RingDefinitionCombo.ItemsSource = ringDefinitions
            .Select((x, index) => string.IsNullOrWhiteSpace(x.RingName) ? $"Ring {index + 1}" : x.RingName)
            .ToList();

        RingDefinitionCombo.SelectedIndex = ringDefinitions.Count == 0
            ? -1
            : Math.Clamp(selectedIndex, 0, ringDefinitions.Count - 1);

        BindSelectedRing();
    }

    private void BindSelectedRing()
    {
        isBindingRingForm = true;

        var selectedIndex = RingDefinitionCombo.SelectedIndex;
        var hasSelection = selectedIndex >= 0 && selectedIndex < ringDefinitions.Count;
        var definition = hasSelection ? ringDefinitions[selectedIndex] : null;

        RingNameBox.Text = definition?.RingName ?? string.Empty;
        JudgesBox.Text = definition?.JudgesText ?? string.Empty;
        DivisionNamesBox.Text = definition?.DivisionNamesText ?? string.Empty;
        GendersBox.Text = definition?.GendersText ?? string.Empty;

        RingNameBox.IsEnabled = hasSelection;
        JudgesBox.IsEnabled = hasSelection;
        DivisionNamesBox.IsEnabled = hasSelection;
        GendersBox.IsEnabled = hasSelection;
        RemoveRingButton.IsEnabled = ringDefinitions.Count > 1 && hasSelection;

        isBindingRingForm = false;
    }

    private void ApplyLocalization()
    {
        var titleKey = showRingSection && !showInformationSection
            ? "ChampionshipRingInformation"
            : "ChampionshipDefinitions";

        Title = LocalizationService.T(titleKey);
        WindowTitleText.Text = LocalizationService.T(titleKey);

        ChampionshipInformationSection.IsVisible = showInformationSection;
        ChampionshipRingSection.IsVisible = showRingSection;

        ChampionshipSelectorLabelText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? "Mistrzostwa"
            : "Championship";
        NewChampionshipLabelText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? "Nowe mistrzostwa"
            : "New championship";
        NewChampionshipNameBox.Watermark = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? "Nazwa mistrzostw"
            : "Championship name";
        CreateChampionshipButton.Content = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? "Utworz"
            : "Create";
        ChampionshipInformationTitleText.Text = LocalizationService.T("ChampionshipInformation");
        ChampionshipNameLabelText.Text = LocalizationService.T("ChampionshipName");
        ChampionshipAddressLabelText.Text = LocalizationService.T("Address");
        StartDateLabelText.Text = LocalizationService.T("StartDate");
        EndDateLabelText.Text = LocalizationService.T("EndDate");

        ChampionshipRingTitleText.Text = LocalizationService.T("ChampionshipRingInformation");
        RingSelectionLabelText.Text = LocalizationService.T("Ring");
        RingNameLabelText.Text = LocalizationService.T("Ring");
        JudgesLabelText.Text = LocalizationService.T("Judges");
        DivisionsLabelText.Text = LocalizationService.T("Division");
        GendersLabelText.Text = LocalizationService.T("Gender");
        ActiveCategoriesLabelText.Text = LocalizationService.T("ActiveCategoriesAndWeights");
        AddRingButton.Content = LocalizationService.CurrentLanguage == AppLanguage.Polish ? "Dodaj ring" : "Add Ring";
        RemoveRingButton.Content = LocalizationService.CurrentLanguage == AppLanguage.Polish ? "Usun ring" : "Remove Ring";
        RingHintText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? "Pozostaw podzialy lub plec puste, aby dopuscic wszystkie."
            : "Leave divisions or genders empty to allow all.";
        SaveButton.Content = LocalizationService.T("Save");
        LocalizationService.LocalizeControlTree(this);

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        if (showRingSection && !showInformationSection)
        {
            var activeCategoryCount = GetActiveCategoryCountForSummary();
            SummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? $"{loadedSettings.ChampionshipName} | {ringDefinitions.Count} ringi | {activeCategoryCount} aktywnych kategorii"
                : $"{loadedSettings.ChampionshipName} | {ringDefinitions.Count} ring(s) | {activeCategoryCount} active categories";
            return;
        }

        var dayCount = 1;

        if (TryParseDate(StartDateBox.Text, out var startDate, allowEmpty: true, out _) &&
            TryParseDate(EndDateBox.Text, out var endDate, allowEmpty: true, out _) &&
            startDate.HasValue &&
            endDate.HasValue)
        {
            dayCount = Math.Max((endDate.Value.Date - startDate.Value.Date).Days + 1, 1);
        }

        SummaryText.Text = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? $"{loadedSettings.ChampionshipName} | {dayCount} dzien"
            : $"{loadedSettings.ChampionshipName} | {dayCount} day(s)";
    }

    private static ChampionshipRingDefinition CloneRingDefinition(ChampionshipRingDefinition definition)
    {
        return new ChampionshipRingDefinition
        {
            RingName = definition.RingName,
            JudgesText = definition.JudgesText,
            DivisionNamesText = definition.DivisionNamesText,
            GendersText = definition.GendersText
        };
    }

    private int GetActiveCategoryCountForSummary()
    {
        if (activeCategoryIds.Count > 0)
            return activeCategoryIds.Count;

        return availableCategoryCount;
    }
}
