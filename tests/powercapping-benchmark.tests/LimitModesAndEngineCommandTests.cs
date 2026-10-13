using client.Services;
using server.Models;
using Xunit;

namespace powercapping_benchmark.tests;

public class LimitModesTests
{
    [Theory]
    [InlineData(null, LimitModes.TimeControl)]
    [InlineData("", LimitModes.TimeControl)]
    [InlineData(LimitModes.TimeControl, LimitModes.TimeControl)]
    [InlineData(LimitModes.TimePerMove, LimitModes.TimePerMove)]
    [InlineData(LimitModes.NodesPerMove, LimitModes.NodesPerMove)]
    public void Normalize_maps_missing_to_time_control_and_keeps_known_modes(string? input, string expected)
        => Assert.Equal(expected, LimitModes.Normalize(input));

    [Theory]
    [InlineData("timecontrol")]
    [InlineData("time-per-move")]
    [InlineData("TIME_CONTROL")]
    public void Normalize_rejects_unknown_modes(string input)
        => Assert.Throws<ArgumentException>(() => LimitModes.Normalize(input));

    [Theory]
    [InlineData(null, true)]
    [InlineData(LimitModes.TimeControl, true)]
    [InlineData(LimitModes.TimePerMove, false)]
    [InlineData(LimitModes.NodesPerMove, false)]
    public void UsesClock_only_for_time_control(string? mode, bool expected)
        => Assert.Equal(expected, LimitModes.UsesClock(mode));

    [Theory]
    [InlineData(LimitModes.TimeControl, 60000L, 0L, null, null, true)]
    [InlineData(LimitModes.TimeControl, 0L, 0L, null, null, false)]
    [InlineData(LimitModes.TimeControl, null, null, null, null, false)]
    [InlineData(LimitModes.TimeControl, 60000L, -1L, null, null, false)]
    [InlineData(LimitModes.TimePerMove, null, null, 500L, null, true)]
    [InlineData(LimitModes.TimePerMove, 60000L, 0L, null, null, false)]
    [InlineData(LimitModes.NodesPerMove, null, null, null, 1000L, true)]
    [InlineData(LimitModes.NodesPerMove, null, null, null, 0L, false)]
    [InlineData("bogus", 60000L, 0L, 500L, 1000L, false)]
    public void HasValidLimits_requires_the_limit_of_the_mode(string mode, long? tc, long? ti, long? tpm, long? npm, bool expected)
        => Assert.Equal(expected, LimitModes.HasValidLimits(mode, tc, ti, tpm, npm));
}

public class BuildGoCommandTests
{
    private static readonly MoveMadeModel Move = new()
    {
        GameId = "g", CurrentMoves = "position startpos", WhiteTimeLeft = 59000, BlackTimeLeft = 58000, Increment = 1000
    };

    [Fact]
    public void Time_control_keeps_the_original_command_byte_for_byte()
        => Assert.Equal("go wtime 59000 btime 58000 winc 1000 binc 1000",
            EngineCommunication.BuildGoCommand(LimitModes.TimeControl, null, null, Move));

    [Fact]
    public void Time_per_move_uses_movetime_and_no_clock_arguments()
    {
        var command = EngineCommunication.BuildGoCommand(LimitModes.TimePerMove, 500, null, Move);

        Assert.Equal("go movetime 500", command);
    }

    [Fact]
    public void Nodes_per_move_uses_nodes_and_no_clock_arguments()
    {
        var command = EngineCommunication.BuildGoCommand(LimitModes.NodesPerMove, null, 5_000_000_000L, Move);

        Assert.Equal("go nodes 5000000000", command);
    }

    [Fact]
    public void Unknown_mode_throws()
        => Assert.Throws<ArgumentException>(() => EngineCommunication.BuildGoCommand("bogus", 1, 1, Move));
}
