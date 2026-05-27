using client.Services;
using Microsoft.AspNetCore.SignalR.Client;
using server.Models;

namespace client;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private const string HubUrl = "http://des07.kask:5000/chessHub";
    private EngineCommunication engineHandler;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger; 
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(HubUrl)
            .WithAutomaticReconnect()
            .Build();

        connection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "SignalR reconnecting...");
            return Task.CompletedTask;
        };

        connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected. ConnectionId={id}", connectionId);
            return Task.CompletedTask;
        };

        connection.Closed += error =>
        {
            _logger.LogWarning(error, "SignalR connection closed.");
            return Task.CompletedTask;
        };
        
        connection.On<string>("Ping", message =>
        {
            _logger.LogInformation("Ping: {message}", message);
            connection.SendAsync("Pong", $"ack: {message}", stoppingToken);
        });
        
        connection.On<GameStateModel, bool>("GameStarted", (gameState,isWhite) =>
        {
            _logger.LogInformation("GameStarted Recieved");
            engineHandler = new EngineCommunication();
            if (isWhite)
            {
                var moves = engineHandler.MakeMove(gameState.InitialPosition);
                connection.SendAsync("MoveMade", moves, stoppingToken);
            }
        });
        
        connection.On<MoveMadeModel>("MoveMade", moveMade =>
        {
            _logger.LogInformation("MoveMade Recieved");
            var move = engineHandler.MakeMove(moveMade.CurrentFen);
            connection.SendAsync("MoveMade", $"{move}", stoppingToken);
        });
        
        connection.On("GameFinished", () =>
        {
            _logger.LogInformation("GameFinished Recieved");
        });

        _logger.LogInformation("Connecting to: {ip}", HubUrl);
        await connection.StartAsync(stoppingToken);
        _logger.LogInformation("SignalR connected. ConnectionId={id}", connection.ConnectionId);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            await connection.StopAsync(CancellationToken.None);
            await connection.DisposeAsync();
        }
    }
}