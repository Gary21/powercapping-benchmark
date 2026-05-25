using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using server.Models;
using server.Stores;

namespace server.Services;

public class GameService(IHubContext<SignalRService, IChessClient> hub, ClientSessionsStore clientSessionsStore)
{
    private ConcurrentDictionary<string, GameStateModel> _games = new ConcurrentDictionary<string, GameStateModel>();
    private ConcurrentDictionary<string, ClientSessionModel> _clientSessions = clientSessionsStore.ClientSessions;
    private PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
    public int TempCounter = 0;
    
    public async Task InitGame(NewGameModel newGame)
    {
            var (player1, player2) = await FindTwoPlayers();
            Console.WriteLine($"Starting game {newGame.GameId} between {player1} and {player2}");
            AllignGameToClients(newGame.GameId, [player1, player2]);
            var currentState = new GameStateModel
            {
                GameId = newGame.GameId,
                CurrentPosition = newGame.InitialPosition,
                WhiteTimeLeft = newGame.TimeControl,
                BlackTimeLeft = newGame.TimeControl,
                Engine = newGame.Engine,
                PowerCap = newGame.PowerCap,
                TimeControl = newGame.TimeControl,
                TimeIncrement = newGame.TimeIncrement,
                InitialPosition = newGame.InitialPosition,
                PowerCapColor = newGame.PowerCapColor,
                WhitePlayer = player1,
                BlackPlayer = player2
            };
            _games.TryAdd(currentState.GameId, currentState);
            await hub.Clients.Clients(player1).GameStarted(newGame, true);
            await hub.Clients.Clients(player2).GameStarted(newGame, false);
    }
    
    public async Task FinishGame(string GameId)
    {
        if (_games.TryRemove(GameId, out var gameState))
        {
            Console.WriteLine($"Finishing game {GameId} between {gameState.WhitePlayer} and {gameState.BlackPlayer}.");
            await hub.Clients.Group(GameId).GameFinished();
            FreeClientsFromGame(gameState.GameId, [gameState.WhitePlayer, gameState.BlackPlayer]);
        }
        else
        {
            Console.WriteLine($"Could not find game {GameId} to finish.");
        }
    }
    
    public string? GetOpponent(string playerId)
    {
        if (_clientSessions.TryGetValue(playerId, out var session) && session.GameId != null)
        {
            var gameId = session.GameId;
            if (_games.TryGetValue(gameId, out var gameState))
            {
                if (gameState.WhitePlayer == playerId)
                    return gameState.BlackPlayer;
                if (gameState.BlackPlayer == playerId)
                    return gameState.WhitePlayer;
            }
        }
        return null;
    }
    
    private async Task<(string,string)> FindTwoPlayers()
    {
        while (true)
        {
            var idlePlayers = _clientSessions
                .Where(kvp => kvp.Value.IsIdle)
                .Take(2)
                .ToList();
            if (idlePlayers.Count == 2)
            {
                Console.WriteLine("Found 2 idle players: " + string.Join(", ", idlePlayers.Select(kvp => kvp.Key)));
                return (idlePlayers[0].Key, idlePlayers[1].Key);
            }

            Console.WriteLine("Could not find 2 idle players. Waiting 10 seconds. Idle count: " + idlePlayers.Count);
            await timer.WaitForNextTickAsync();
        }
    }
    
    private void AllignGameToClients(string gameId, List<string> clientIds)
    {
        foreach (var clientId in clientIds)
        {
            _clientSessions[clientId].IsIdle = false;
            _clientSessions[clientId].GameId = gameId;
            hub.Groups.AddToGroupAsync(clientId, gameId);
        }
    }
    
    private void FreeClientsFromGame(string gameId, List<string> clientIds)
    {
        foreach (var clientId in clientIds)
        {
            _clientSessions[clientId].IsIdle = true;
            _clientSessions[clientId].GameId = null;
            hub.Groups.RemoveFromGroupAsync(clientId, gameId);
        }
    }
    
    public async Task Ping(string message)
        => await hub.Clients.All.Ping(message);
}