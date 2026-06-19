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
    
    public void MoveMade(SubmitMoveModel message)
    {
        try
        {
            var moveMessage = message.Move;
            var avgPower = message.AvgPower;
            var avgNps = message.AvgNps;
            Console.WriteLine($"[Hub] Move: {moveMessage} from: {Context.ConnectionId}");
            var timeElapsed = DateTime.UtcNow - gameService.GetGameState(Context.ConnectionId)!.LastMoveTimestamp;
            Console.WriteLine($"[Hub] Time elapsed since last move: {timeElapsed.TotalMilliseconds} milliseconds.");
            var opponentId = gameService.GetOpponent(Context.ConnectionId);
            var gameState = gameService.GetGameState(Context.ConnectionId)!;
            var currentPlayerColor = gameState.Board.Turn.AsChar;
            if (currentPlayerColor == 'w')
            {
                gameState.WhiteAvgNps = avgNps;
                gameState.WhiteAvgPower = avgPower;
                gameState.WhiteTimeLeft -= (long)timeElapsed.TotalMilliseconds;
                gameState.WhiteTimeLeft += gameState.TimeIncrement;
            }
            else
            {
                gameState.BlackAvgNps = avgNps;
                gameState.BlackAvgPower = avgPower;
                gameState.BlackTimeLeft -= (long)timeElapsed.TotalMilliseconds;
                gameState.BlackTimeLeft += gameState.TimeIncrement;
            }
            Console.WriteLine($"[Hub] Time left - White: {gameState.WhiteTimeLeft} ms, Black: {gameState.BlackTimeLeft} ms.");
            var move = new Move(moveMessage[..2], moveMessage[2..4]);
            if(!gameState.Board.Move(move))
            {
                Console.WriteLine($"[Hub] Move {moveMessage} is illegal.");
            }
            if (gameState.Board.IsEndGame || gameState.WhiteTimeLeft <= 0 || gameState.BlackTimeLeft <= 0)
            {
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
                    CurrentMoves = GetStockfishPositionCommand(gameState.Board),
                    WhiteTimeLeft = gameState.WhiteTimeLeft,
                    BlackTimeLeft = gameState.BlackTimeLeft,
                    Increment = gameState.TimeIncrement
                });
        }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        
    }
    
    private string GetStockfishPositionCommand(ChessBoard board)
    {
        var uciMoves = board.ExecutedMoves.Select(m => 
        {
            string moveString = $"{m.OriginalPosition.ToString().ToLower()}{m.NewPosition.ToString().ToLower()}";
            
            if (m.Promotion != null)
            {
                moveString += m.Promotion.Type.AsChar;
            }
        
            return moveString;
        });
        
        string movesString = string.Join(" ", uciMoves);
        
        if (string.IsNullOrEmpty(movesString))
        {
            return "position startpos";
        }

        return $"position startpos moves {movesString}";
    }
}