namespace server.Models;

public class FinishedGameModel
{
    public string GameId { get;set; }
    public string Engine { get; set; }
    public int PowerCap { get; set; } // hardware power cap percent; in nodes_per_move the percent of NodesPerMove given to the PowerCapColor side: MAX(1, ROUND(NodesPerMove * PowerCap / 100.0))
    public string LimitMode { get; set; } = LimitModes.TimeControl;
    public long? TimeControl { get; set; }
    public long? TimeIncrement { get; set; }
    public int? TimePerMove { get; set; } // MS
    public long? NodesPerMove { get; set; }
    public string InitialPosition { get; set; }
    public string PowerCapColor { get; set; }
    public string Result { get; set; }
    public string EndgameType { get; set; }
    public double WhiteAvgPower { get; set; }
    public double BlackAvgPower { get; set; }
    public int TotalMoves { get; set; }
    public double? WhiteTimeLeft { get; set; }
    public double? BlackTimeLeft { get; set; }
    public double WhiteAvgNps { get; set; }
    public double BlackAvgNps { get; set; }
    public bool IsGpu { get; set; }
    public string Moves { get; set; }
}