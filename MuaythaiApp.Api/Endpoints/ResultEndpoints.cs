using MuaythaiApp.Api.Contracts;
using MuaythaiApp.Api.Data;

namespace MuaythaiApp.Api.Endpoints;

public static class ResultEndpoints
{
    public static IEndpointRouteBuilder MapResultEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/results", GetResultsAsync);
        app.MapPost("/api/results", SaveResultAsync);
        return app;
    }

    private static async Task<IResult> GetResultsAsync(int? dayNumber, PgConnectionFactory connectionFactory)
    {
        var results = new List<MatchResultDto>();
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT
            mr.match_id,
            m.day_number,
            m.order_no,
            m.fighter1_name,
            m.fighter2_name,
            mr.winner,
            m.age_category,
            m.weight_category,
            mr.method,
            mr.round
        FROM match_results mr
        INNER JOIN matches m
            ON m.id = mr.match_id
        WHERE (@dayNumber IS NULL OR m.day_number = @dayNumber)
        ORDER BY m.day_number, m.order_no, mr.match_id
        """;
        command.Parameters.AddWithValue("@dayNumber", (object?)dayNumber ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new MatchResultDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetInt32(9)));
        }

        return Results.Ok(results);
    }

    private static async Task<IResult> SaveResultAsync(SaveMatchResultRequest request, PgConnectionFactory connectionFactory)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM match_results WHERE match_id = @matchId";
        delete.Parameters.AddWithValue("@matchId", request.MatchId);
        await delete.ExecuteNonQueryAsync();

        var insert = connection.CreateCommand();
        insert.CommandText =
        """
        INSERT INTO match_results
        (
            match_id,
            winner,
            method,
            round,
            judge_red,
            judge_blue
        )
        VALUES
        (
            @matchId,
            @winner,
            @method,
            @round,
            @judgeRed,
            @judgeBlue
        )
        """;
        insert.Parameters.AddWithValue("@matchId", request.MatchId);
        insert.Parameters.AddWithValue("@winner", request.Winner.Trim());
        insert.Parameters.AddWithValue("@method", request.Method.Trim());
        insert.Parameters.AddWithValue("@round", request.Round);
        insert.Parameters.AddWithValue("@judgeRed", request.JudgeRed);
        insert.Parameters.AddWithValue("@judgeBlue", request.JudgeBlue);
        await insert.ExecuteNonQueryAsync();

        return Results.Ok();
    }
}
