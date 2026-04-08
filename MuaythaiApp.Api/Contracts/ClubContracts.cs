namespace MuaythaiApp.Api.Contracts;

public sealed record ClubDto(int Id, string Name, string Coach, string City, string Country);

public sealed record SaveClubRequest(string Name, string Coach, string City, string Country);
