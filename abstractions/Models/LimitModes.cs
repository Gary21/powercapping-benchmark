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

    // the mode is known and the limit it needs is present and in range; used where a game is about to start
    public static bool HasValidLimits(string? mode, long? timeControl, long? timeIncrement, long? timePerMove, long? nodesPerMove)
        => IsValid(mode) && Normalize(mode) switch
        {
            TimeControl => timeControl > 0 && timeIncrement >= 0,
            TimePerMove => timePerMove > 0,
            NodesPerMove => nodesPerMove > 0,
            _ => false
        };

    // never throws; callers get an already normalized mode from the game start (GameService.InitGame)
    public static bool UsesClock(string? mode) => string.IsNullOrEmpty(mode) || mode == TimeControl;
}
