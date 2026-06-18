using Microsoft.AspNetCore.SignalR;
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
    await gameService.Ping("dupa");
    return Results.Ok(new { ok = true });
});
app.MapGet("/newGame", async (GameService gameService, int timeControl, int timeIncrement, int powerCapPercent, int noOfGamesPerSide, string initialPosition) =>
{
    for (int i = 0; i < noOfGamesPerSide; i++)
    {
        var newGame = new NewGameModel
        {
            GameId = Guid.NewGuid().ToString(),
            Engine = "stockfish",
            PowerCap = powerCapPercent,
            TimeControl = timeControl,
            TimeIncrement = timeIncrement,
            InitialPosition = initialPosition,
            PowerCapColor = "white"
        };
        await gameService.InitGame(newGame);
        newGame = new NewGameModel
        {
            GameId = Guid.NewGuid().ToString(),
            Engine = "stockfish",
            PowerCap = powerCapPercent,
            TimeControl = timeControl,
            TimeIncrement = timeIncrement,
            InitialPosition = initialPosition,
            PowerCapColor = "black"
        };
        await gameService.InitGame(newGame);
    }
    return Results.Ok(new { ok = true });
});

app.Run();