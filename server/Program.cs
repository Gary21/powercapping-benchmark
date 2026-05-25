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
app.MapGet("/newGame", async (GameService gameService) =>
{
    var newGame = new NewGameModel
    {
        GameId = "game1",
        Engine = "stockfish",
        PowerCap = 50,
        TimeControl = 300,
        TimeIncrement = 2,
        InitialPosition = "startpos",
        PowerCapColor = "white"
    };
    await gameService.InitGame(newGame);
    return Results.Ok(new { ok = true });
});

app.Run();