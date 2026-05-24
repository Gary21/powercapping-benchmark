using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using server.Models;

namespace server.Services;

public class GameService
{
    private ConcurrentDictionary<string, GameStateModel> Games = new ConcurrentDictionary<string, GameStateModel>();
    private readonly IHubContext<SignalRService> _hub;

    public GameService(IHubContext<SignalRService> hub)
    {
        _hub = hub;
    }

    public async void InitGame(NewGameModel newGame)
    {
        try
        {
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
                PowerCapColor = newGame.PowerCapColor
            };
        
            Games.TryAdd(newGame.GameId, currentState);
            await _hub.Clients.Group(currentState.GameId).SendAsync("GameStarted", currentState);
        }
        catch (Exception e)
        {
            throw; // TODO handle exception
        }
    }
    
    public async Task Ping(string message)
        => await _hub.Clients.All.SendAsync("ReceiveMessage", message);
}