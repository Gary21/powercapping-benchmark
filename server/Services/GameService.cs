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
    
    public async Task InitGame(NewGameModel newGame)
    {
        try
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
            await hub.Clients.Group(currentState.GameId).GameStarted(currentState);
            _games.TryAdd(newGame.GameId, currentState);
        }
        catch (Exception e)
        {
            throw; // TODO handle exception
        }
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
    
    public async Task Ping(string message)
        => await hub.Clients.All.Ping(message);
}