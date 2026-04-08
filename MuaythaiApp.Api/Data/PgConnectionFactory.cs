using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MuaythaiApp.Api.Data;

public sealed class PgConnectionFactory
{
    private readonly string connectionString;

    public PgConnectionFactory(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is missing.");
    }

    public NpgsqlConnection Create() => new(connectionString);
}
