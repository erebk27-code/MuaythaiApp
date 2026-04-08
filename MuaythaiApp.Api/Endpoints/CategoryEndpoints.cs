using MuaythaiApp.Api.Contracts;
using MuaythaiApp.Api.Data;

namespace MuaythaiApp.Api.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories", GetCategoriesAsync);
        return app;
    }

    private static async Task<IResult> GetCategoriesAsync(PgConnectionFactory connectionFactory)
    {
        var categories = new List<CategoryDto>();
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT
            id,
            division,
            gender,
            age_min,
            age_max,
            weight_max,
            is_open_weight,
            sort_order,
            category_name,
            round_count,
            round_duration_seconds,
            break_duration_seconds
        FROM categories
        ORDER BY division, gender, sort_order
        """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            categories.Add(new CategoryDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetDouble(5),
                reader.GetBoolean(6),
                reader.GetInt32(7),
                reader.GetString(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetInt32(11)));
        }

        return Results.Ok(categories);
    }
}
