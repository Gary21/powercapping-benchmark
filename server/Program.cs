using server.Models;
using server.Services;
using server.Stores;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<GameService>();
builder.Services.AddSingleton<ClientSessionsStore>();
builder.Services.AddSingleton<DatabaseHandler>();
builder.Services.AddHostedService<GameSchedulerService>();
builder.WebHost.UseUrls("http://0.0.0.0:5000");
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientCors", policy =>
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true));
});
builder.Services.AddSignalR();

var app = builder.Build();
app.UseRouting();
app.UseCors("ClientCors");

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapHub<SignalRService>("/chessHub");
app.MapGet("/ping", async (GameService gameService) =>
{
    await gameService.Ping("awesome-ping-text:)");
    return Results.Ok(new { ok = true });
});
app.MapGet("/scheduleGames", async (DatabaseHandler dbHandler, int timeControl, int timeIncrement, int powerCapPercent, int noOfGamesPerSide) =>
{
    var newScheduledGames = new GamesScheduledModel
    {
        Id = Guid.NewGuid().ToString(),
        NumberOfGamesPerSide = noOfGamesPerSide,
        TimeControl = timeControl,
        TimeIncrement = timeIncrement,
        PowerCap = powerCapPercent
    };
    dbHandler.ScheduleGames(newScheduledGames);
    return Results.Ok(new { ok = true });
});

app.Run();