using System.Collections.Concurrent;
using Chess;
using Microsoft.AspNetCore.SignalR;
using server.Models;
using server.Stores;

namespace server.Services;

public class SignalRService(ClientSessionsStore clientSessionsStore, GameService gameService)
    : Hub<IChessClient>
{
    private ConcurrentDictionary<string, ClientSessionModel> _clientSessions = clientSessionsStore.ClientSessions;

    public override Task OnConnectedAsync()
    {
        Console.WriteLine($"[Hub] Connected: {Context.ConnectionId}");
        var newSession = new ClientSessionModel
        {
            IsIdle = true,
            GameId = null
        };
        _clientSessions.TryAdd(Context.ConnectionId, newSession);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"[Hub] Disconnected: {Context.ConnectionId} ({exception?.Message ?? "ok"})");
        _clientSessions.TryRemove(Context.ConnectionId, out _);
        return base.OnDisconnectedAsync(exception);
    }
    
    public void MoveMade(string message)
    {
        Console.WriteLine($"[Hub] Move: {message} from: {Context.ConnectionId}");
        var opponentId = gameService.GetOpponent(Context.ConnectionId);
        var gameState = gameService.GetGameState(Context.ConnectionId)!;
        var move = new Move(message[..2], message[2..]);
        if(!gameState.Board.Move(move))
        {
            Console.WriteLine($"[Hub] Move {message} is illegal.");
        }
        Console.WriteLine($"[Hub] Current position: \n{gameState.Board.ToAscii()}\n");
        if (gameState.Board.IsEndGame)
        {
            var winner = gameState.Board.EndGame.WonSide.AsChar;
            Console.WriteLine($"[Hub] Game finishing... {winner} won.");
            gameService.FinishGame(_clientSessions[Context.ConnectionId].GameId!);
            return;
        }
        Console.WriteLine($"[Hub] Sending move to opponent: {opponentId}");
        if (opponentId != null)
        {
            Clients.Client(opponentId).MoveMade(new MoveMadeModel
            {
                GameId = _clientSessions[Context.ConnectionId].GameId!,
                CurrentFen = gameState.Board.ToFen(),
                WhiteTimeLeft = 100f,
                BlackTimeLeft = 100f
            });
        }
    }
}