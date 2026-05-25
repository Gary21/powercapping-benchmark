using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using server.Models;

namespace server.Services;

public class SignalRService : Hub<IChessClient>
{
    private ConcurrentDictionary<string, ClientSessionModel> _clientSessions = new();
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
    
    public void Pong(string message)
    {
        Console.WriteLine($"[Hub] Pong from {Context.ConnectionId}: {message}");
    }
}