using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using server.Models;
using server.Stores;

namespace server.Services;

public class SignalRService : Hub<IChessClient>
{
    private ConcurrentDictionary<string, ClientSessionModel> _clientSessions;
    public SignalRService(ClientSessionsStore clientSessionsStore) => _clientSessions = clientSessionsStore.ClientSessions;
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