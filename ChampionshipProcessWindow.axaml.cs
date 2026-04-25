using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MuaythaiApp;

public enum ChampionshipProcessViewMode
{
    AthleteControl,
    AthleteScale,
    GenderControl,
    ScaleControl
}

public partial class ChampionshipProcessWindow : Window
{
    private List<ChampionshipProcessEntry> entries = new();
    private List<ChampionshipProcessEntry> filteredEntries = new();
    private readonly ChampionshipProcessViewMode viewMode;
    private int selectedDayNumber = 1;
    private bool isInitializing;

    public ChampionshipProcessWindow()
        : this(ChampionshipProcessViewMode.AthleteControl)
    {
    }

    public ChampionshipProcessWindow(ChampionshipProcessViewMode viewMode)
    {
        this.viewMode = viewMode;

        try
        {
            isInitializing = true;
            InitializeComponent();
            StartupLogger.Log("ChampionshipProcessWindow constructor started");

            // Keep Athlete Control responsive even if championship settings are unavailable.
            var dayItems = Enumerable.Range(1, 4)
                .Select(day => $"Day {day}")
                .ToList();
            StartupLogger.Log($"ChampionshipProcessWindow prepared {dayItems.Count} day items");

            DayCombo.ItemsSource = dayItems;
            StartupLogger.Log("ChampionshipProcessWindow assigned DayCombo items");

            DayCombo.SelectedIndex = 0;
            StartupLogger.Log("ChampionshipProcessWindow selected first day");

            Opened += async (_, __) =>
            {
                try
                {
                    StartupLogger.Log("ChampionshipProcessWindow opened");
                    await LoadEntriesAsync();
                }
                catch (Exception ex)
                {
                    SummaryText.Text = $"Athlete Control could not load: {ex.Message}";
                    StartupLogger.Log(ex, "ChampionshipProcessWindow.LoadEntries failed on open");
                }
            };
            StartupLogger.Log("ChampionshipProcessWindow attached Opened handler");

            LocalizationService.LanguageChanged += ApplyLocalization;
            Closed += (_, __) => LocalizationService.LanguageChanged -= ApplyLocalization;
            StartupLogger.Log("ChampionshipProcessWindow attached localization handlers");

            ApplyLocalization();
            StartupLogger.Log("ChampionshipProcessWindow applied localization");

            isInitializing = false;
            StartupLogger.Log("ChampionshipProcessWindow constructor completed");
        }
        catch (Exception ex)
        {
            isInitializing = false;
            StartupLogger.Log(ex, "ChampionshipProcessWindow constructor failed");
            throw;
        }
    }

    private void LoadEntries()
    {
        selectedDayNumber = ParseSelectedDayNumber();
        entries = ChampionshipProcessService.LoadEntries(
            selectedDayNumber,
            UseMatchRosterForSelectedDay(),
            IncludeDocumentEvaluation(),
            IncludeGenderEvaluation());
        BindLists();
        SummaryText.Text = BuildLoadedSummary();
        StartupLogger.Log($"ChampionshipProcessWindow.LoadEntries completed with {entries.Count} athletes for day {selectedDayNumber}");
    }

    private async Task LoadEntriesAsync()
    {
        selectedDayNumber = ParseSelectedDayNumber();
            SummaryText.Text = IsDayOneOnlyView()
                ? "Loading athletes..."
                : $"Loading athletes for Day {selectedDayNumber}...";
        StartupLogger.Log($"ChampionshipProcessWindow.LoadEntriesAsync started for day {selectedDayNumber}");

        var useMatchRosterForDay = UseMatchRosterForSelectedDay();
        var includeDocuments = IncludeDocumentEvaluation();
        var includeGender = IncludeGenderEvaluation();
        var loadedEntries = await Task.Run(() => ChampionshipProcessService.LoadEntries(
            selectedDayNumber,
            useMatchRosterForDay,
            includeDocuments,
            includeGender));
        entries = loadedEntries;
        BindLists();
        SummaryText.Text = BuildLoadedSummary();
        StartupLogger.Log($"ChampionshipProcessWindow.LoadEntriesAsync completed with {entries.Count} athletes for day {selectedDayNumber}");
    }

    private void BindLists()
    {
        filteredEntries = FilterEntries();
        AthleteScaleListBox.ItemsSource = null;
        GenderControlListBox.ItemsSource = null;
        GenderOnlyControlListBox.ItemsSource = null;
        ScaleControlListBox.ItemsSource = null;
        AthleteScaleListBox.ItemsSource = filteredEntries;
        GenderControlListBox.ItemsSource = filteredEntries;
        GenderOnlyControlListBox.ItemsSource = filteredEntries;
        ScaleControlListBox.ItemsSource = filteredEntries;
    }

    private List<ChampionshipProcessEntry> FilterEntries()
    {
        var searchText = SearchFighterBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
            return entries;

        return entries
            .Where(entry =>
                entry.FighterName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                entry.ClubName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async void RefreshClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await LoadEntriesAsync();
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Refresh failed: {ex.Message}";
            StartupLogger.Log(ex, "ChampionshipProcessWindow.RefreshClick failed");
        }
    }

    private void SaveClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            ChampionshipProcessService.SaveEntries(selectedDayNumber, entries);
            entries.ForEach(ChampionshipProcessService.EvaluateEntry);
            BindLists();
            SummaryText.Text = IsDayOneOnlyView()
                ? $"{entries.Count} athlete(s) saved"
                : $"{entries.Count} athlete(s) saved for Day {selectedDayNumber}";
            StartupLogger.Log($"ChampionshipProcessWindow.SaveClick saved {entries.Count} athletes for day {selectedDayNumber}");
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Save failed: {ex.Message}";
            StartupLogger.Log(ex, "ChampionshipProcessWindow.SaveClick failed");
        }
    }

    private void EntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not ChampionshipProcessEntry entry)
            return;

        entry.MeasuredWeightText = textBox.Text?.Trim() ?? string.Empty;
    }

    private void SearchFighterTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (isInitializing)
            return;

        BindLists();
        SummaryText.Text = string.IsNullOrWhiteSpace(SearchFighterBox.Text)
            ? BuildLoadedSummary()
            : $"{filteredEntries.Count} athlete(s) found";
    }

    private void DocumentCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox ||
            checkBox.DataContext is not ChampionshipProcessEntry entry ||
            checkBox.Tag is not string fieldName)
        {
            return;
        }

        var isChecked = checkBox.IsChecked == true;
        switch (fieldName)
        {
            case nameof(ChampionshipProcessEntry.GenderConfirmed):
                entry.GenderConfirmed = isChecked;
                break;
            case nameof(ChampionshipProcessEntry.MedicalReportPresented):
                entry.MedicalReportPresented = isChecked;
                break;
            case nameof(ChampionshipProcessEntry.InsurancePresented):
                entry.InsurancePresented = isChecked;
                break;
            case nameof(ChampionshipProcessEntry.RegistrationFormPresented):
                entry.RegistrationFormPresented = isChecked;
                break;
            case nameof(ChampionshipProcessEntry.GuardianConsentPresented):
                entry.GuardianConsentPresented = isChecked;
                break;
            case nameof(ChampionshipProcessEntry.SeniorGuardianConsentPresented):
                entry.SeniorGuardianConsentPresented = isChecked;
                break;
            case nameof(ChampionshipProcessEntry.AmateurLicense2026Presented):
                entry.AmateurLicense2026Presented = isChecked;
                break;
            case nameof(ChampionshipProcessEntry.PolishCitizenshipConfirmed):
                entry.PolishCitizenshipConfirmed = isChecked;
                break;
        }

        ChampionshipProcessService.EvaluateEntry(entry);
    }

    private void SelectAllDocumentsClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not ChampionshipProcessEntry entry)
            return;

        entry.MedicalReportPresented = true;
        entry.InsurancePresented = true;
        entry.RegistrationFormPresented = true;
        entry.AmateurLicense2026Presented = true;
        entry.GenderConfirmed = true;

        if (entry.RequiresGuardianConsent)
            entry.GuardianConsentPresented = true;

        if (entry.RequiresSeniorGuardianConsent)
            entry.SeniorGuardianConsentPresented = true;

        if (entry.RequiresPolishCitizenship)
            entry.PolishCitizenshipConfirmed = true;

        ChampionshipProcessService.EvaluateEntry(entry);
    }

    private async void ApplyControlsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dayNumber = selectedDayNumber;
            ApplyButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            SummaryText.Text = IsDayOneOnlyView()
                ? "Applying controls..."
                : $"Applying controls for Day {dayNumber}...";

            var summary = await Task.Run(() =>
            {
                ChampionshipProcessService.SaveEntries(dayNumber, entries);
                return ChampionshipProcessService.ApplyDayControls(dayNumber, viewMode == ChampionshipProcessViewMode.ScaleControl);
            });

            await LoadEntriesAsync();
            SummaryText.Text = summary;
            StartupLogger.Log($"ChampionshipProcessWindow.ApplyControlsClick completed: {summary}");
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Apply controls failed: {ex.Message}";
            StartupLogger.Log(ex, "ChampionshipProcessWindow.ApplyControlsClick failed");
        }
        finally
        {
            ApplyButton.IsEnabled = true;
            SaveButton.IsEnabled = true;
        }
    }

    private async void DayChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (isInitializing)
            return;

        try
        {
            selectedDayNumber = ParseSelectedDayNumber();
            await LoadEntriesAsync();
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Day change failed: {ex.Message}";
            StartupLogger.Log(ex, "ChampionshipProcessWindow.DayChanged failed");
        }
    }

    private int ParseSelectedDayNumber()
    {
        if (viewMode is ChampionshipProcessViewMode.AthleteControl or ChampionshipProcessViewMode.AthleteScale or ChampionshipProcessViewMode.GenderControl)
            return 1;

        if (DayCombo.SelectedIndex >= 0)
            return DayCombo.SelectedIndex + 1;

        var text = DayCombo.SelectedItem?.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var digits = new string(text.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var day))
                return day;
        }

        return 1;
    }

    private string BuildLoadedSummary()
        => IsDayOneOnlyView()
            ? $"{entries.Count} athlete(s) loaded"
            : $"{entries.Count} athlete(s) loaded for Day {selectedDayNumber}";

    private bool IsDayOneOnlyView()
        => viewMode is ChampionshipProcessViewMode.AthleteControl or ChampionshipProcessViewMode.AthleteScale or ChampionshipProcessViewMode.GenderControl;

    private bool UseMatchRosterForSelectedDay()
        => viewMode == ChampionshipProcessViewMode.ScaleControl && selectedDayNumber > 1;

    private bool IncludeDocumentEvaluation()
        => viewMode == ChampionshipProcessViewMode.AthleteControl;

    private bool IncludeGenderEvaluation()
        => viewMode is ChampionshipProcessViewMode.AthleteControl or ChampionshipProcessViewMode.GenderControl;

    private void ApplyLocalization()
    {
        Title = GetLocalizedWindowTitle();
        WindowTitleText.Text = GetLocalizedWindowTitle();
        ProcessDayLabelText.Text = LocalizationService.T("Day");
        RefreshButton.Content = LocalizationService.T("Refresh");
        SaveButton.Content = LocalizationService.T("Save");
        ApplyButton.Content = LocalizationService.T("ApplyControls");
        SearchFighterLabelText.Text = LocalizationService.T("SearchFighter");
        AthleteScaleTitleText.Text = viewMode == ChampionshipProcessViewMode.ScaleControl
            ? LocalizationService.T("ScaleControl")
            : LocalizationService.T("AthleteScaleAndList");
        GenderControlTitleText.Text = LocalizationService.T("DocumentControl");
        GenderOnlyControlTitleText.Text = LocalizationService.T("GenderControl");
        ScaleControlTitleText.Text = LocalizationService.T("EligibilityStatus");
        LocalizationService.LocalizeControlTree(this);
        ApplyViewMode();

        if (entries.Count > 0)
            BindLists();
    }

    private string GetLocalizedWindowTitle()
    {
        return viewMode switch
        {
            ChampionshipProcessViewMode.AthleteScale => LocalizationService.T("AthleteScale"),
            ChampionshipProcessViewMode.GenderControl => LocalizationService.T("GenderControl"),
            ChampionshipProcessViewMode.ScaleControl => LocalizationService.T("ScaleControl"),
            _ => LocalizationService.T("AthleteControl")
        };
    }

    private void ApplyViewMode()
    {
        var showAthleteControl = viewMode == ChampionshipProcessViewMode.AthleteControl;
        var showDayOneOnly = viewMode is ChampionshipProcessViewMode.AthleteControl or ChampionshipProcessViewMode.AthleteScale or ChampionshipProcessViewMode.GenderControl;
        var showScale = viewMode is ChampionshipProcessViewMode.AthleteControl
            or ChampionshipProcessViewMode.AthleteScale
            or ChampionshipProcessViewMode.ScaleControl;
        var showGenderOnly = viewMode == ChampionshipProcessViewMode.GenderControl;
        var showDaySelector = !showDayOneOnly;

        ProcessDayLabelText.IsVisible = showDaySelector;
        DayCombo.IsVisible = showDaySelector;
        SearchFighterLabelText.IsVisible = showAthleteControl;
        SearchFighterBox.IsVisible = showAthleteControl;
        AthleteScaleSection.IsVisible = showScale;
        DocumentControlSection.IsVisible = showAthleteControl;
        GenderControlSection.IsVisible = showGenderOnly;
        EligibilityStatusSection.IsVisible = showAthleteControl;

        RootGrid.RowDefinitions = showAthleteControl
            ? new RowDefinitions("Auto,Auto,*,*,*,Auto")
            : showGenderOnly
                ? new RowDefinitions("Auto,Auto,0,*,0,Auto")
                : new RowDefinitions("Auto,Auto,*,0,0,Auto");
    }
}
