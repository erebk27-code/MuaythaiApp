using MuaythaiApp.Api.Contracts;
using MuaythaiApp.Api.Data;

namespace MuaythaiApp.Api.Endpoints;

public static class ClubEndpoints
{
    public static IEndpointRouteBuilder MapClubEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/clubs", GetClubsAsync);
        app.MapPost("/api/clubs", CreateClubAsync);
        app.MapPut("/api/clubs/{id:int}", UpdateClubAsync);
        app.MapDelete("/api/clubs/{id:int}", DeleteClubAsync);
        return app;
    }

    private static async Task<IResult> GetClubsAsync(PgConnectionFactory connectionFactory)
    {
        var result = new List<ClubDto>();
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, coach, city, country FROM clubs ORDER BY name";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new ClubDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                reader.IsDBNull(4) ? string.Empty : reader.GetString(4)));
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> CreateClubAsync(SaveClubRequest request, PgConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        INSERT INTO clubs (name, coach, city, country)
        VALUES (@name, @coach, @city, @country)
        RETURNING id
        """;
        command.Parameters.AddWithValue("@name", request.Name.Trim());
        command.Parameters.AddWithValue("@coach", request.Coach?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("@city", request.City?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("@country", request.Country?.Trim() ?? string.Empty);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        return Results.Created($"/api/clubs/{id}", new { id });
    }

    private static async Task<IResult> UpdateClubAsync(int id, SaveClubRequest request, PgConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        UPDATE clubs
        SET name = @name,
            coach = @coach,
            city = @city,
            country = @country
        WHERE id = @id
        """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", request.Name.Trim());
        command.Parameters.AddWithValue("@coach", request.Coach?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("@city", request.City?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("@country", request.Country?.Trim() ?? string.Empty);

        await command.ExecuteNonQueryAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteClubAsync(int id, PgConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM clubs WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();

        return Results.NoContent();
    }
}
