namespace server.Models;

public static class LimitModes
{
    public const string TimeControl = "time_control";
    public const string TimePerMove = "time_per_move";
    public const string NodesPerMove = "nodes_per_move";

    // null/empty means "time_control": rows written before limit modes existed and clients/servers that don't send the field
    public static bool IsValid(string? mode) => string.IsNullOrEmpty(mode) || mode is TimeControl or TimePerMove or NodesPerMove;

    // an unknown value throws, so a typo can't silently select the no-clock path
    public static string Normalize(string? mode) => IsValid(mode)
        ? (string.IsNullOrEmpty(mode) ? TimeControl : mode)
        : throw new ArgumentException($"Unknown limit mode: '{mode}'", nameof(mode));

    // in nodes_per_move the power cap percent is not a hardware limit but the share of the node budget given to the capped side
    public static bool IsValidNodesPowerCap(int powerCapPercent) => powerCapPercent is >= 1 and <= 100;

    // the side whose colour is PowerCapColor (case-sensitive, like the hardware cap logic)
    public static bool IsCappedSide(bool isWhite, string? powerCapColor)
        => (isWhite && powerCapColor == "white") || (!isWhite && powerCapColor == "black");

    // Nodes per move for one side in nodes_per_move: the capped side gets round(nodes * percent / 100), at least 1; the other side the full budget.
    // decimal: exact and no overflow for any long (long * int would overflow, double loses precision above 2^53).
    // AwayFromZero: 2.5 -> 3 like ROUND() in SQLite, so the same value can be reproduced from NodesPerMove * PowerCap / 100.
    public static long EffectiveNodesPerMove(long nodesPerMove, int powerCapPercent, bool isCappedSide)
    {
        if (nodesPerMove <= 0) throw new ArgumentOutOfRangeException(nameof(nodesPerMove));
        if (!IsValidNodesPowerCap(powerCapPercent)) throw new ArgumentOutOfRangeException(nameof(powerCapPercent));
        if (!isCappedSide) return nodesPerMove;
        var scaled = Math.Round(nodesPerMove * (decimal)powerCapPercent / 100m, MidpointRounding.AwayFromZero);
        return Math.Max(1L, (long)scaled);
    }

    // the mode is known and the limit it needs is present and in range; used where a game is about to start
    public static bool HasValidLimits(string? mode, long? timeControl, long? timeIncrement, long? timePerMove, long? nodesPerMove, int powerCapPercent)
        => IsValid(mode) && Normalize(mode) switch
        {
            TimeControl => timeControl > 0 && timeIncrement >= 0,
            TimePerMove => timePerMove > 0,
            NodesPerMove => nodesPerMove > 0 && IsValidNodesPowerCap(powerCapPercent),
            _ => false
        };

    // never throws; callers get an already normalized mode from the game start (GameService.InitGame)
    public static bool UsesClock(string? mode) => string.IsNullOrEmpty(mode) || mode == TimeControl;
}
