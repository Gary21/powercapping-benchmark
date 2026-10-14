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
            
            var newGameSettings = scheduledGames.FirstOrDefault(IsPlayable);
            if (newGameSettings == null)
            {
                Console.WriteLine("No playable scheduled games found. Waiting 10 seconds.");
                continue;
            }

            await gameService.InitGame(BuildNewGame(newGameSettings, "white"), stoppingToken);
            await gameService.InitGame(BuildNewGame(newGameSettings, "black"), stoppingToken);
            dbHandler.RemoveScheduledGame(newGameSettings);
        }
    }

    // A row with an unknown mode or a missing/non-positive limit would start a broken game (or throw in the loop), so it is skipped
    private static bool IsPlayable(GamesScheduledModel row)
    {
        var playable = LimitModes.HasValidLimits(row.LimitMode, row.TimeControl, row.TimeIncrement, row.TimePerMove, row.NodesPerMove, row.PowerCap);
        if (!playable)
            Console.WriteLine($"Skipping scheduled games {row.Id}: invalid limit settings (mode '{row.LimitMode}', TC {row.TimeControl}, TI {row.TimeIncrement}, TPM {row.TimePerMove}, NPM {row.NodesPerMove}, PC {row.PowerCap}).");
        return playable;
    }

    private static NewGameModel BuildNewGame(GamesScheduledModel settings, string powerCapColor)
        => new NewGameModel
        {
            GameId = Guid.NewGuid().ToString(),
            Engine = "stockfish",
            PowerCap = settings.PowerCap,
            LimitMode = LimitModes.Normalize(settings.LimitMode),
            TimeControl = settings.TimeControl ?? 0,
            TimeIncrement = settings.TimeIncrement ?? 0,
            TimePerMove = settings.TimePerMove,
            NodesPerMove = settings.NodesPerMove,
            PowerCapColor = powerCapColor,
            InitialMoves = settings.InitialMoves,
            IsGpu = settings.IsGpu
        };
}