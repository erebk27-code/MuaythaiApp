using MuaythaiApp.Api.Contracts;
using MuaythaiApp.Api.Data;

namespace MuaythaiApp.Api.Endpoints;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/matches", GetMatchesAsync);
        app.MapPost("/api/matches", CreateMatchAsync);
        app.MapPut("/api/matches/{id:int}", UpdateMatchAsync);
        app.MapDelete("/api/matches/{id:int}", DeleteMatchAsync);
        return app;
    }

    private static async Task<IResult> GetMatchesAsync(int? dayNumber, PgConnectionFactory connectionFactory)
    {
        var matches = new List<MatchDto>();
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT
            id,
            fighter1_id,
            fighter2_id,
            fighter1_name,
            fighter2_name,
            age_category,
            weight_category,
            gender,
            category_group,
            order_no,
            judges_count,
            day_number
        FROM matches
        WHERE (@dayNumber IS NULL OR day_number = @dayNumber)
        ORDER BY day_number, order_no, id
        """;
        command.Parameters.AddWithValue("@dayNumber", (object?)dayNumber ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            matches.Add(new MatchDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetInt32(11)));
        }

        return Results.Ok(matches);
    }

    private static async Task<IResult> CreateMatchAsync(SaveMatchRequest request, PgConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        INSERT INTO matches
        (
            fighter1_id,
            fighter2_id,
            fighter1_name,
            fighter2_name,
            age_category,
            weight_category,
            gender,
            category_group,
            order_no,
            judges_count,
            day_number
        )
        VALUES
        (
            @fighter1Id,
            @fighter2Id,
            @fighter1Name,
            @fighter2Name,
            @ageCategory,
            @weightCategory,
            @gender,
            @categoryGroup,
            @orderNo,
            @judgesCount,
            @dayNumber
        )
        RETURNING id
        """;
        BindMatchParameters(command, request);
        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        return Results.Created($"/api/matches/{id}", new { id });
    }

    private static async Task<IResult> UpdateMatchAsync(int id, SaveMatchRequest request, PgConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        UPDATE matches
        SET fighter1_id = @fighter1Id,
            fighter2_id = @fighter2Id,
            fighter1_name = @fighter1Name,
            fighter2_name = @fighter2Name,
            age_category = @ageCategory,
            weight_category = @weightCategory,
            gender = @gender,
            category_group = @categoryGroup,
            order_no = @orderNo,
            judges_count = @judgesCount,
            day_number = @dayNumber
        WHERE id = @id
        """;
        command.Parameters.AddWithValue("@id", id);
        BindMatchParameters(command, request);
        await command.ExecuteNonQueryAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteMatchAsync(int id, PgConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var deleteScores = connection.CreateCommand();
        deleteScores.CommandText = "DELETE FROM judge_scores WHERE match_id = @id";
        deleteScores.Parameters.AddWithValue("@id", id);
        await deleteScores.ExecuteNonQueryAsync();

        var deleteResult = connection.CreateCommand();
        deleteResult.CommandText = "DELETE FROM match_results WHERE match_id = @id";
        deleteResult.Parameters.AddWithValue("@id", id);
        await deleteResult.ExecuteNonQueryAsync();

        var deleteMatch = connection.CreateCommand();
        deleteMatch.CommandText = "DELETE FROM matches WHERE id = @id";
        deleteMatch.Parameters.AddWithValue("@id", id);
        await deleteMatch.ExecuteNonQueryAsync();

        return Results.NoContent();
    }

    private static void BindMatchParameters(Npgsql.NpgsqlCommand command, SaveMatchRequest request)
    {
        command.Parameters.AddWithValue("@fighter1Id", request.Fighter1Id);
        command.Parameters.AddWithValue("@fighter2Id", request.Fighter2Id);
        command.Parameters.AddWithValue("@fighter1Name", request.Fighter1Name.Trim());
        command.Parameters.AddWithValue("@fighter2Name", request.Fighter2Name.Trim());
        command.Parameters.AddWithValue("@ageCategory", request.AgeCategory.Trim());
        command.Parameters.AddWithValue("@weightCategory", request.WeightCategory.Trim());
        command.Parameters.AddWithValue("@gender", request.Gender.Trim());
        command.Parameters.AddWithValue("@categoryGroup", request.CategoryGroup.Trim());
        command.Parameters.AddWithValue("@orderNo", request.OrderNo);
        command.Parameters.AddWithValue("@judgesCount", request.JudgesCount);
        command.Parameters.AddWithValue("@dayNumber", request.DayNumber);
    }
}
