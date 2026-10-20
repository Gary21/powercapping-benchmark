using server.Models;

namespace server.Services;

public static class ScheduleRequestValidator
{
    private const string AllowedCombinations =
        "Provide exactly one limit: (timeControl and timeIncrement) OR timePerMove OR nodesPerMove.";

    // Returns the limit mode inferred from the provided parameters, or an error message when the request is invalid.
    // powerCapPercent is only checked in nodes_per_move, where it scales the node budget; in the other modes it stays a hardware cap as before.
    public static (string? Error, string? Mode) Validate(int? timeControl, int? timeIncrement, int? timePerMove, long? nodesPerMove, double powerCapPercent)
    {
        if (timeControl.HasValue != timeIncrement.HasValue)
            return ($"timeControl and timeIncrement must be provided together. {AllowedCombinations}", null);

        var hasClock = timeControl.HasValue;
        var providedSets = (hasClock ? 1 : 0) + (timePerMove.HasValue ? 1 : 0) + (nodesPerMove.HasValue ? 1 : 0);
        if (providedSets != 1)
        {
            var received = new List<string>();
            if (hasClock) received.Add("timeControl+timeIncrement");
            if (timePerMove.HasValue) received.Add("timePerMove");
            if (nodesPerMove.HasValue) received.Add("nodesPerMove");
            var receivedText = received.Count == 0 ? "none" : string.Join(", ", received);
            return ($"{AllowedCombinations} Received: {receivedText}.", null);
        }

        if (hasClock)
        {
            if (timeControl <= 0)
                return ("timeControl must be greater than 0.", null);
            if (timeIncrement < 0)
                return ("timeIncrement must be greater than or equal to 0.", null);
            return (null, LimitModes.TimeControl);
        }

        if (timePerMove.HasValue)
            return timePerMove > 0
                ? (null, LimitModes.TimePerMove)
                : ("timePerMove must be greater than 0.", null);

        if (nodesPerMove <= 0)
            return ("nodesPerMove must be greater than 0.", null);
        if (!LimitModes.IsValidNodesPowerCap(powerCapPercent))
            return ("powerCapPercent must be greater than 0 and at most 100 in nodes_per_move mode (fractions like 0.5 are allowed) (it scales the node budget of the capped side).", null);
        return (null, LimitModes.NodesPerMove);
    }
}
