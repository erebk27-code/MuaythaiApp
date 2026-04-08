namespace MuaythaiApp.Api.Contracts;

public sealed record MatchResultDto(
    int MatchId,
    int DayNumber,
    int BoutNo,
    string RedName,
    string BlueName,
    string Winner,
    string Category,
    string WeightClass,
    string Method,
    int Round);

public sealed record SaveMatchResultRequest(
    int MatchId,
    string Winner,
    string Method,
    int Round,
    int JudgeRed,
    int JudgeBlue);
