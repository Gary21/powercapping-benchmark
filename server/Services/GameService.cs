using System.Collections.Concurrent;
using Chess;
using Microsoft.AspNetCore.SignalR;
using server.Models;
using server.Stores;

namespace server.Services;

public class GameService(IHubContext<SignalRService, IChessClient> hub, ClientSessionsStore clientSessionsStore, DatabaseHandler databaseHandler)
{
    private ConcurrentDictionary<string, GameStateModel> _games = new ConcurrentDictionary<string, GameStateModel>();
    private ConcurrentDictionary<string, ClientSessionModel> _clientSessions = clientSessionsStore.ClientSessions;
    private PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
    private const string StartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    
    public async Task InitGame(NewGameModel newGame)
    {
            var (player1, player2) = await FindTwoPlayers();
            Console.WriteLine($"Starting game {newGame.GameId} between {player1} and {player2}");
            AlignGameToClients(newGame.GameId, [player1, player2]);
            var currentState = new GameStateModel
            {
                GameId = newGame.GameId,
                WhiteTimeLeft = newGame.TimeControl,
                BlackTimeLeft = newGame.TimeControl,
                Engine = newGame.Engine,
                PowerCap = newGame.PowerCap,
                TimeControl = newGame.TimeControl,
                TimeIncrement = newGame.TimeIncrement,
                PowerCapColor = newGame.PowerCapColor,
                WhitePlayer = player1,
                BlackPlayer = player2,
                Board = ChessBoard.LoadFromFen(StartingFen, AutoEndgameRules.All),
                InitialMoves = newGame.InitialMoves
            };
            _games.TryAdd(currentState.GameId, currentState);
            currentState.LastMoveTimestamp = DateTime.UtcNow;
            await hub.Clients.Clients(player1).GameStarted(newGame, true);
            await hub.Clients.Clients(player2).GameStarted(newGame, false);
            Console.WriteLine($"[Hub] Game starting...\n");
            Console.WriteLine($"[Hub] Time left - White: {currentState.WhiteTimeLeft} ms, Black: {currentState.BlackTimeLeft} ms.");
            Console.WriteLine($"[Hub] Current position: \n{currentState.Board.ToAscii()}\n");
    }
    
    public async Task FinishGame(string GameId)
    {
        if (_games.TryRemove(GameId, out var gameState))
        {
            Console.WriteLine($"Finishing game {GameId} between {gameState.WhitePlayer} and {gameState.BlackPlayer}.");
            await hub.Clients.Group(GameId).GameFinished();
            FreeClientsFromGame(gameState.GameId, [gameState.WhitePlayer, gameState.BlackPlayer]);
            string winner;
            if (gameState.WhiteTimeLeft <= 0)
            {
                winner = "b";
            }
            else if (gameState.BlackTimeLeft <= 0)
            {
                winner = "w";
            }
            else
            {
                winner = gameState.Board.EndGame.WonSide != null
                    ? $"{gameState.Board.EndGame.WonSide.AsChar}"
                    : "d";
            }
            Console.WriteLine($"[Hub] Game finishing... {winner}, type: {gameState.Board.EndGame.EndgameType}");
            var finishedGame = new FinishedGameModel
            {
                GameId = gameState.GameId,
                Engine = gameState.Engine,
                PowerCap = gameState.PowerCap,
                TimeControl = gameState.TimeControl,
                TimeIncrement = gameState.TimeIncrement,
                InitialPosition = gameState.InitialMoves,
                PowerCapColor = gameState.PowerCapColor,
                Result = winner,
                EndgameType = gameState.Board.EndGame.EndgameType.ToString(),
            };
            databaseHandler.InsertResult(finishedGame);
        }
        else
        {
            Console.WriteLine($"Could not find game {GameId} to finish.");
        }
    }
    
    public string? GetOpponent(string playerId)
    {
        /*Console.WriteLine("Player " + playerId + " is asking for opponent.");
        foreach (var gameState in _games)
        {
            Console.WriteLine($"Game {gameState.Key}: White: {gameState.Value.WhitePlayer}, Black: {gameState.Value.BlackPlayer}");
        }

        foreach (var clientSession in _clientSessions)
        {
            Console.WriteLine("Client " + clientSession.Key + ": IsIdle: " + clientSession.Value.IsIdle + ", GameId: " + clientSession.Value.GameId);
        }*/
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
    
    public GameStateModel? GetGameState(string playerId)
    {
        if (_clientSessions.TryGetValue(playerId, out var session) && session.GameId != null)
        {
            var gameId = session.GameId;
            if (_games.TryGetValue(gameId, out var gameState))
            {
                return gameState;
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
    
    private void AlignGameToClients(string gameId, List<string> clientIds)
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