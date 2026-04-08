namespace MuaythaiApp.Api.Contracts;

public sealed record FighterDto(
    int Id,
    string FirstName,
    string LastName,
    double Weight,
    int Age,
    int ClubId,
    int BirthYear,
    string Gender,
    string AgeCategory,
    string WeightCategory,
    string ClubName);

public sealed record SaveFighterRequest(
    string FirstName,
    string LastName,
    double Weight,
    int Age,
    int ClubId,
    int BirthYear,
    string Gender,
    string AgeCategory,
    string WeightCategory);
