using SQLite;

// Same table names as the real models, but with the schema the database had before limit modes existed.
namespace powercapping_benchmark.tests.Old
{
    public class FinishedGameModel
    {
        public string GameId { get; set; } = "";
        public long TimeControl { get; set; }
        public long TimeIncrement { get; set; }
        public double WhiteTimeLeft { get; set; }
        public double BlackTimeLeft { get; set; }
        public string Result { get; set; } = "";
    }

    public class GamesScheduledModel
    {
        [PrimaryKey] public string Id { get; set; } = "";
        public int TimeControl { get; set; }
        public int TimeIncrement { get; set; }
        public int PowerCap { get; set; }
        public int NumberOfGamesPerSide { get; set; }
        public bool IsGpu { get; set; }
        public string InitialMoves { get; set; } = "";
    }
}
