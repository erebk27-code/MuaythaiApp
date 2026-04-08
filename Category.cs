namespace MuaythaiApp;

public class Category
{
    public int Id { get; set; }
    public string Division { get; set; } = "";
    public string Gender { get; set; } = "";
    public int AgeMin { get; set; }
    public int AgeMax { get; set; }
    public double WeightMax { get; set; }
    public bool IsOpenWeight { get; set; }
    public int SortOrder { get; set; }
    public string CategoryName { get; set; } = "";
    public int RoundCount { get; set; }
    public int RoundDurationSeconds { get; set; }
    public int BreakDurationSeconds { get; set; }

    public string AgeRange => $"{AgeMin}-{AgeMax}";

    public string WeightDisplay =>
        IsOpenWeight
            ? $"+{FormatWeight(WeightMax)} kg"
            : $"-{FormatWeight(WeightMax)} kg";

    public string FullName =>
        $"{Division} | {Gender} | {WeightDisplay}";

    public string RoundInfo =>
        $"{RoundCount} x {FormatDuration(RoundDurationSeconds)}";

    public string BreakInfo =>
        FormatDuration(BreakDurationSeconds);

    private static string FormatWeight(double value)
    {
        return value % 1 == 0
            ? value.ToString("0")
            : value.ToString("0.##");
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds % 60 == 0)
            return $"{seconds / 60} min";

        return $"{seconds / 60.0:0.#} min";
    }
}

public class CategorySummary
{
    public string Division { get; set; } = "";
    public string Gender { get; set; } = "";
    public int AgeMin { get; set; }
    public int AgeMax { get; set; }
    public double MinWeight { get; set; }
    public double MaxWeight { get; set; }
    public bool HasOpenWeight { get; set; }
    public int WeightClassCount { get; set; }
    public int RoundCount { get; set; }
    public int RoundDurationSeconds { get; set; }
    public int BreakDurationSeconds { get; set; }

    public string AgeRange => $"{AgeMin}-{AgeMax}";

    public string WeightRange =>
        HasOpenWeight
            ? $"{FormatWeight(MinWeight)} kg - +{FormatWeight(MaxWeight)} kg"
            : $"{FormatWeight(MinWeight)} kg - {FormatWeight(MaxWeight)} kg";

    public string RoundInfo =>
        $"{RoundCount} x {FormatDuration(RoundDurationSeconds)}";

    public string BreakInfo =>
        FormatDuration(BreakDurationSeconds);

    public string WeightClassInfo =>
        $"{WeightClassCount} weight classes";

    private static string FormatWeight(double value)
    {
        return value % 1 == 0
            ? value.ToString("0")
            : value.ToString("0.##");
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds % 60 == 0)
            return $"{seconds / 60} min";

        return $"{seconds / 60.0:0.#} min";
    }
}
