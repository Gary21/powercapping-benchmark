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
        var timeElapsed = DateTime.UtcNow - gameService.GetGameState(Context.ConnectionId)!.LastMoveTimestamp;
        Console.WriteLine($"[Hub] Time elapsed since last move: {timeElapsed.TotalMilliseconds} milliseconds.");
        var opponentId = gameService.GetOpponent(Context.ConnectionId);
        var gameState = gameService.GetGameState(Context.ConnectionId)!;
        var currentPlayerColor = gameState.Board.Turn.AsChar;
        if (currentPlayerColor == 'w')
        {
            gameState.WhiteTimeLeft -= (long)timeElapsed.TotalMilliseconds;
            gameState.WhiteTimeLeft += gameState.TimeIncrement;
        }
        else
        {
            gameState.BlackTimeLeft -= (long)timeElapsed.TotalMilliseconds;
            gameState.BlackTimeLeft += gameState.TimeIncrement;
        }
        Console.WriteLine($"[Hub] Time left - White: {gameState.WhiteTimeLeft} ms, Black: {gameState.BlackTimeLeft} ms.");
        var move = new Move(message[..2], message[2..]);
        if(message.Length > 4)
        {
            move = new Move($"{message[2..4]}={message[4]}");
        }
        if(!gameState.Board.Move(move))
        {
            Console.WriteLine($"[Hub] Move {message} is illegal.");
        }
        if (gameState.Board.IsEndGame || gameState.WhiteTimeLeft <= 0 || gameState.BlackTimeLeft <= 0)
        {
            string winner;
            if (gameState.WhiteTimeLeft <= 0)
            {
                winner = "Black wins on time";
            }
            else if (gameState.BlackTimeLeft <= 0)
            {
                winner = "White wins on time";
            }
            else
            {
                winner = gameState.Board.EndGame.WonSide != null
                    ? $"{gameState.Board.EndGame.WonSide.AsChar} won"
                    : "draw";
            }
            Console.WriteLine($"[Hub] Game finishing... {winner}");
            gameService.FinishGame(_clientSessions[Context.ConnectionId].GameId!);
            return;
        }
        Console.WriteLine($"[Hub] Current position: \n{gameState.Board.ToAscii()}\n");
        Console.WriteLine($"[Hub] Sending move to opponent: {opponentId}");
        if (opponentId != null)
        {
            gameState.LastMoveTimestamp = DateTime.UtcNow;
            Clients.Client(opponentId).MoveMade(new MoveMadeModel
            {
                GameId = _clientSessions[Context.ConnectionId].GameId!,
                CurrentFen = gameState.Board.ToFen(),
                WhiteTimeLeft = gameState.WhiteTimeLeft,
                BlackTimeLeft = gameState.BlackTimeLeft,
                Increment = gameState.TimeIncrement
            });
        }
    }
}