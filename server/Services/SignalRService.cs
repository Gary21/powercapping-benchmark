using Microsoft.AspNetCore.SignalR;

namespace server.Services;

public class SignalRService : Hub
{
    public override Task OnConnectedAsync()
    {
        Console.WriteLine($"[Hub] Connected: {Context.ConnectionId}");
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"[Hub] Disconnected: {Context.ConnectionId} ({exception?.Message ?? "ok"})");
        return base.OnDisconnectedAsync(exception);
    }
    
    public void Pong(string message)
    {
        Console.WriteLine($"[Hub] Pong from {Context.ConnectionId}: {message}");
    }
}