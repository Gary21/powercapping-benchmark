using client.Services;
using Microsoft.AspNetCore.SignalR.Client;
using server.Models;

namespace client;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IEngineCommunicationFactory _engineFactory;
    private const string HubUrl = "http://des07.kask:5000/chessHub";
    private EngineCommunication engineHandler;

    public Worker(ILogger<Worker> logger, IEngineCommunicationFactory engineFactory)
    {
        _logger = logger;
        _engineFactory = engineFactory;
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
        
        connection.On<NewGameModel, bool>("GameStarted", (gameState,isWhite) =>
        {
            _logger.LogInformation("GameStarted Recieved");
            engineHandler = _engineFactory.Create(isWhite, gameState.PowerCap, gameState.PowerCapColor);
            if (isWhite)
            {
                var submitMove = engineHandler.MakeMove(new MoveMadeModel(gameState));
                connection.SendAsync("MoveMade", submitMove, stoppingToken);
            }
        });
        
        connection.On<MoveMadeModel>("MoveMade", moveMade =>
        {
            _logger.LogInformation("MoveMade Recieved");
            var submitMove = engineHandler.MakeMove(moveMade);
            connection.SendAsync("MoveMade", submitMove, stoppingToken);
        });
        
        connection.On("GameFinished", () =>
        {
            _logger.LogInformation("GameFinished Recieved");
            engineHandler.Dispose();
            engineHandler = null;
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