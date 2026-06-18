using server.Models;

namespace server.Services;

public class GameSchedulerService(DatabaseHandler dbHandler, GameService gameService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var idlePlayersCount = await gameService.GetIdlePlayersCount();
            if (idlePlayersCount < 2)
            {
                Console.WriteLine("Could not find 2 idle players. Waiting 10 seconds. Idle players count: " + idlePlayersCount);
                continue;
            }
            
            var scheduledGames = dbHandler.GetScheduledGames();
            if(scheduledGames.Count == 0)
                continue;
            
            var newGameSettings = scheduledGames[0];
            var newGame = new NewGameModel
            {
                GameId = Guid.NewGuid().ToString(),
                Engine = "stockfish",
                PowerCap = newGameSettings.PowerCap,
                TimeControl = newGameSettings.TimeControl,
                TimeIncrement = newGameSettings.TimeIncrement,
                PowerCapColor = "white",
                InitialMoves = ""
            };
            await gameService.InitGame(newGame, stoppingToken);
            newGame = new NewGameModel
            {
                GameId = Guid.NewGuid().ToString(),
                Engine = "stockfish",
                PowerCap = newGameSettings.PowerCap,
                TimeControl = newGameSettings.TimeControl,
                TimeIncrement = newGameSettings.TimeIncrement,
                PowerCapColor = "black",
                InitialMoves = ""
            };
            await gameService.InitGame(newGame, stoppingToken);
            dbHandler.RemoveScheduledGame(newGameSettings);
        }
    }
}