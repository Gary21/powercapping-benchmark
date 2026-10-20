using SQLite;

namespace server.Models;

public class GamesScheduledModel
{
    [PrimaryKey]
    public string Id { get; set; }
    public string LimitMode { get; set; } = LimitModes.TimeControl;
    public int? TimeControl { get; set; }
    public int? TimeIncrement { get; set; }
    public int? TimePerMove { get; set; } // MS
    public long? NodesPerMove { get; set; }
    public double PowerCap { get; set; } // hardware power cap percent; in nodes_per_move the percent of NodesPerMove given to the capped side (>0 to 100, fractions allowed)
    public int NumberOfGamesPerSide { get; set; }
    public bool IsGpu { get; set; }
    public string InitialMoves { get; set; } = "";
}