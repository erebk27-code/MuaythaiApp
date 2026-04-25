using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MuaythaiApp;

public sealed class ChampionshipSettings
{
    public int Id { get; set; }
    public string ChampionshipName { get; set; } = "Muaythai Championship";
    public string ChampionshipAddress { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<string> RingNames { get; set; } = new() { "RING A" };
    public List<ChampionshipRingDefinition> RingDefinitions { get; set; } = new()
    {
        new ChampionshipRingDefinition { RingName = "RING A" }
    };
    public HashSet<int> ActiveCategoryIds { get; set; } = new();
}

public sealed class ChampionshipListItem
{
    public int Id { get; set; }
    public string ChampionshipName { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public string DisplayName
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(ChampionshipName)
                ? "Championship"
                : ChampionshipName;

            if (StartDate.HasValue && EndDate.HasValue)
                return $"{name} ({StartDate:yyyy-MM-dd} - {EndDate:yyyy-MM-dd})";

            if (StartDate.HasValue)
                return $"{name} ({StartDate:yyyy-MM-dd})";

            return name;
        }
    }
}

public sealed class ChampionshipRingDefinition : INotifyPropertyChanged
{
    private string ringName = "";
    private string judgesText = "";
    private string divisionNamesText = "";
    private string gendersText = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string RingName
    {
        get => ringName;
        set => SetField(ref ringName, value);
    }

    public string JudgesText
    {
        get => judgesText;
        set => SetField(ref judgesText, value);
    }

    public string DivisionNamesText
    {
        get => divisionNamesText;
        set
        {
            if (SetField(ref divisionNamesText, value))
                OnPropertyChanged(nameof(DivisionsDisplay));
        }
    }

    public string GendersText
    {
        get => gendersText;
        set
        {
            if (SetField(ref gendersText, value))
                OnPropertyChanged(nameof(GendersDisplay));
        }
    }

    public string DivisionsDisplay => string.IsNullOrWhiteSpace(DivisionNamesText)
        ? "Select divisions"
        : DivisionNamesText;

    public string GendersDisplay => string.IsNullOrWhiteSpace(GendersText)
        ? "Select genders"
        : GendersText;

    public List<string> GetDivisionNames()
        => SplitValues(DivisionNamesText);

    public List<string> GetGenders()
        => SplitValues(GendersText);

    public bool Supports(string division, string weightCategory, string gender)
    {
        var divisions = GetDivisionNames();
        var genders = GetGenders();
        var divisionDisplay = FormatDivisionDisplay(division, ExtractAgeRange(weightCategory));

        var divisionMatch = divisions.Count == 0 ||
                            divisions.Any(x =>
                                string.Equals(x, division, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(x, divisionDisplay, StringComparison.OrdinalIgnoreCase) ||
                                x.StartsWith(division + " (", StringComparison.OrdinalIgnoreCase));
        var genderMatch = genders.Count == 0 ||
                          genders.Any(x => string.Equals(x, gender, StringComparison.OrdinalIgnoreCase));

        return divisionMatch && genderMatch;
    }

    public static string FormatDivisionDisplay(string division, string ageRange)
    {
        var normalizedDivision = division?.Trim() ?? string.Empty;
        var normalizedRange = ageRange?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedDivision))
            return string.Empty;

        return string.IsNullOrWhiteSpace(normalizedRange)
            ? normalizedDivision
            : $"{normalizedDivision} ({normalizedRange})";
    }

    public static string ExtractAgeRange(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var start = value.IndexOf('(');
        var end = value.IndexOf(')');

        if (start < 0 || end <= start)
            return string.Empty;

        return value.Substring(start + 1, end - start - 1).Trim();
    }

    public static List<string> SplitValues(string? value)
    {
        return (value ?? string.Empty)
            .Split(new[] { ',', ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class ChampionshipCategoryOption
{
    public int Id { get; set; }
    public string Division { get; set; } = "";
    public string Gender { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public bool IsActive { get; set; }

    public string DisplayName => $"{Division} | {Gender} | {CategoryName}";
}
