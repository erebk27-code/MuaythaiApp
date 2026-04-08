using MuaythaiApp.Api.Contracts;
using MuaythaiApp.Api.Data;

namespace MuaythaiApp.Api.Endpoints;

public static class FighterEndpoints
{
    public static IEndpointRouteBuilder MapFighterEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/fighters", GetFightersAsync);
        app.MapPost("/api/fighters", CreateFighterAsync);
        app.MapPut("/api/fighters/{id:int}", UpdateFighterAsync);
        app.MapDelete("/api/fighters/{id:int}", DeleteFighterAsync);
        return app;
    }

    private static async Task<IResult> GetFightersAsync(PgConnectionFactory connectionFactory)
    {
        var fighters = new List<FighterDto>();
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT
            f.id,
            f.first_name,
            f.last_name,
            f.weight,
            f.age,
            f.club_id,
            f.birth_year,
            f.gender,
            f.age_category,
            f.weight_category,
            COALESCE(c.name, '')
        FROM fighters f
        LEFT JOIN clubs c
            ON c.id = f.club_id
        ORDER BY f.first_name, f.last_name
        """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            fighters.Add(new FighterDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDouble(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10)));
        }

        return Results.Ok(fighters);
    }

    private static async Task<IResult> CreateFighterAsync(SaveFighterRequest request, PgConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        INSERT INTO fighters
        (
            first_name,
            last_name,
            weight,
            age,
            club_id,
            birth_year,
            gender,
            age_category,
            weight_category
        )
        VALUES
        (
            @firstName,
            @lastName,
            @weight,
            @age,
            @clubId,
            @birthYear,
            @gender,
            @ageCategory,
            @weightCategory
        )
        RETURNING id
        """;
        BindFighterParameters(command, request);
        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        return Results.Created($"/api/fighters/{id}", new { id });
    }

    private static async Task<IResult> UpdateFighterAsync(int id, SaveFighterRequest request, PgConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        UPDATE fighters
        SET first_name = @firstName,
            last_name = @lastName,
            weight = @weight,
            age = @age,
            club_id = @clubId,
            birth_year = @birthYear,
            gender = @gender,
            age_category = @ageCategory,
            weight_category = @weightCategory
        WHERE id = @id
        """;
        command.Parameters.AddWithValue("@id", id);
        BindFighterParameters(command, request);
        await command.ExecuteNonQueryAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteFighterAsync(int id, PgConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM fighters WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();
        return Results.NoContent();
    }

    private static void BindFighterParameters(Npgsql.NpgsqlCommand command, SaveFighterRequest request)
    {
        command.Parameters.AddWithValue("@firstName", request.FirstName.Trim());
        command.Parameters.AddWithValue("@lastName", request.LastName.Trim());
        command.Parameters.AddWithValue("@weight", request.Weight);
        command.Parameters.AddWithValue("@age", request.Age);
        command.Parameters.AddWithValue("@clubId", request.ClubId);
        command.Parameters.AddWithValue("@birthYear", request.BirthYear);
        command.Parameters.AddWithValue("@gender", request.Gender.Trim());
        command.Parameters.AddWithValue("@ageCategory", request.AgeCategory.Trim());
        command.Parameters.AddWithValue("@weightCategory", request.WeightCategory.Trim());
    }
}
