using System.Collections.Concurrent;
using server.Models;

namespace server.Stores;

public class ClientSessionsStore
{
    public ConcurrentDictionary<string, ClientSessionModel> ClientSessions { get; set; } = new();
    
}