using MuaythaiApp.Api.Data;
using MuaythaiApp.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<PgConnectionFactory>();
builder.Services.AddSingleton<DatabaseInitializer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "MuaythaiApp.Api",
    utc = DateTime.UtcNow
}));

app.MapAuthEndpoints();
app.MapClubEndpoints();
app.MapFighterEndpoints();
app.MapCategoryEndpoints();
app.MapMatchEndpoints();
app.MapResultEndpoints();

app.Run();
