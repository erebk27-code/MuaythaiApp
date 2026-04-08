namespace MuaythaiApp.Api.Contracts;

public sealed record CategoryDto(
    int Id,
    string Division,
    string Gender,
    int AgeMin,
    int AgeMax,
    double WeightMax,
    bool IsOpenWeight,
    int SortOrder,
    string CategoryName,
    int RoundCount,
    int RoundDurationSeconds,
    int BreakDurationSeconds);
