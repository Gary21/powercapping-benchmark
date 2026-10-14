using Chess;

namespace server.Models;

public class GameStateModel
{
    public string GameId { get; set; }
    public long WhiteTimeLeft { get; set; } // MS
    public long BlackTimeLeft { get; set; } // MS
    public string Engine { get; set; }
    public int PowerCap { get; set; } // POWER CAP IS PERCENTAGE VALUE POWER = (MAX WATTS - MIN WATTS) * (POWER CAP / 100)) + MIN WATTS; in nodes_per_move it is instead the percent of NodesPerMove given to the PowerCapColor side (both sides run at full power)
    public string LimitMode { get; set; } = LimitModes.TimeControl;
    public long TimeControl { get; set; } // MS, 0 when the mode has no clock
    public long TimeIncrement { get; set; } // MS, 0 when the mode has no clock
    public int? TimePerMove { get; set; } // MS
    public long? NodesPerMove { get; set; }
    public bool UsesClock => LimitModes.UsesClock(LimitMode);
    public string InitialMoves { get; set; }
    public string PowerCapColor { get; set; }
    public string WhitePlayer { get; set; }
    public string BlackPlayer { get; set; }
    public ChessBoard Board { get; set; }
    public DateTime LastMoveTimestamp { get; set; }
    public double WhiteAvgPower { get; set; }
    public double BlackAvgPower { get; set; }
    public double WhiteAvgNps { get; set; }
    public double BlackAvgNps { get; set; }
    public bool IsGpu { get; set; }
}