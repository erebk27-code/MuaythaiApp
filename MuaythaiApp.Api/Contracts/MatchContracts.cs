namespace MuaythaiApp.Api.Contracts;

public sealed record MatchDto(
    int Id,
    int Fighter1Id,
    int Fighter2Id,
    string Fighter1Name,
    string Fighter2Name,
    string AgeCategory,
    string WeightCategory,
    string Gender,
    string CategoryGroup,
    int OrderNo,
    int JudgesCount,
    int DayNumber);

public sealed record SaveMatchRequest(
    int Fighter1Id,
    int Fighter2Id,
    string Fighter1Name,
    string Fighter2Name,
    string AgeCategory,
    string WeightCategory,
    string Gender,
    string CategoryGroup,
    int OrderNo,
    int JudgesCount,
    int DayNumber);
