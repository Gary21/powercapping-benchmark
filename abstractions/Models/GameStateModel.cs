using Chess;

namespace server.Models;

public class GameStateModel
{
    public string GameId { get; set; }
    public long WhiteTimeLeft { get; set; } // MS
    public long BlackTimeLeft { get; set; } // MS
    public string Engine { get; set; }
    public int PowerCap { get; set; } // POWER CAP IS PERCENTAGE VALUE POWER = (MAX WATTS - MIN WATTS) * (POWER CAP / 100)) + MIN WATTS
    public long TimeControl { get; set; } // MS
    public long TimeIncrement { get; set; } // MS
    public string InitialPosition { get; set; }
    public string PowerCapColor { get; set; }
    public string WhitePlayer { get; set; }
    public string BlackPlayer { get; set; }
    public ChessBoard Board { get; set; }
    public DateTime LastMoveTimestamp { get; set; }
    public double WhiteAvgPower { get; set; }
    public double BlackAvgPower { get; set; }
}