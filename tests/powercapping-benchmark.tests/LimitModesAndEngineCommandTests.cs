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
    [InlineData(LimitModes.TimeControl, 60000L, 0L, null, null, 50, true)]
    [InlineData(LimitModes.TimeControl, 0L, 0L, null, null, 50, false)]
    [InlineData(LimitModes.TimeControl, null, null, null, null, 50, false)]
    [InlineData(LimitModes.TimeControl, 60000L, -1L, null, null, 50, false)]
    [InlineData(LimitModes.TimeControl, 60000L, 0L, null, null, 0, true)]      // percent is a hardware cap here, not range-checked
    [InlineData(LimitModes.TimePerMove, null, null, 500L, null, 50, true)]
    [InlineData(LimitModes.TimePerMove, null, null, 500L, null, 0, true)]
    [InlineData(LimitModes.TimePerMove, 60000L, 0L, null, null, 50, false)]
    [InlineData(LimitModes.NodesPerMove, null, null, null, 1000L, 50, true)]
    [InlineData(LimitModes.NodesPerMove, null, null, null, 1000L, 1, true)]
    [InlineData(LimitModes.NodesPerMove, null, null, null, 1000L, 100, true)]
    [InlineData(LimitModes.NodesPerMove, null, null, null, 1000L, 0, false)]  // would give a side 0 nodes
    [InlineData(LimitModes.NodesPerMove, null, null, null, 1000L, 101, false)] // would give the capped side more nodes
    [InlineData(LimitModes.NodesPerMove, null, null, null, 0L, 50, false)]
    [InlineData("bogus", 60000L, 0L, 500L, 1000L, 50, false)]
    public void HasValidLimits_requires_the_limit_of_the_mode(string mode, long? tc, long? ti, long? tpm, long? npm, int percent, bool expected)
        => Assert.Equal(expected, LimitModes.HasValidLimits(mode, tc, ti, tpm, npm, percent));

    [Theory]
    [InlineData(true, "white", true)]
    [InlineData(true, "black", false)]
    [InlineData(false, "white", false)]
    [InlineData(false, "black", true)]
    [InlineData(true, null, false)]
    [InlineData(false, null, false)]
    [InlineData(true, "White", false)] // case-sensitive, like the hardware cap logic
    public void IsCappedSide_is_the_side_with_the_power_cap_colour(bool isWhite, string? color, bool expected)
        => Assert.Equal(expected, LimitModes.IsCappedSide(isWhite, color));

    [Theory]
    [InlineData(1_000_000L, 20, true, 200_000L)]
    [InlineData(1_000_000L, 20, false, 1_000_000L)] // the uncapped side always gets the full budget
    [InlineData(1_000_000L, 100, true, 1_000_000L)]
    [InlineData(1L, 1, true, 1L)]                    // never 0 nodes
    [InlineData(1L, 50, true, 1L)]
    [InlineData(3L, 50, true, 2L)]                   // 1.5 -> 2
    [InlineData(5L, 50, true, 3L)]                   // 2.5 -> 3 (banker's rounding would give 2)
    [InlineData(149L, 1, true, 1L)]                  // 1.49 -> 1
    [InlineData(150L, 1, true, 2L)]                  // 1.5 -> 2
    [InlineData(1000L, 1, true, 10L)]
    public void EffectiveNodesPerMove_scales_only_the_capped_side(long nodes, int percent, bool capped, long expected)
        => Assert.Equal(expected, LimitModes.EffectiveNodesPerMove(nodes, percent, capped));

    [Fact]
    public void EffectiveNodesPerMove_does_not_overflow_for_huge_budgets()
    {
        Assert.Equal(long.MaxValue, LimitModes.EffectiveNodesPerMove(long.MaxValue, 100, true));

        // independent of the production math: exact integer arithmetic, halves rounded up
        var expected = (long)((System.Numerics.BigInteger.Multiply(long.MaxValue, 99) + 50) / 100);
        Assert.Equal(9131138316486228049L, expected);
        Assert.Equal(expected, LimitModes.EffectiveNodesPerMove(long.MaxValue, 99, true));
    }

    [Theory]
    [InlineData(1000L, 0)]
    [InlineData(1000L, 101)]
    [InlineData(1000L, -1)]
    [InlineData(0L, 50)]
    [InlineData(-5L, 50)]
    public void EffectiveNodesPerMove_rejects_out_of_range_input(long nodes, int percent)
        => Assert.Throws<ArgumentOutOfRangeException>(() => LimitModes.EffectiveNodesPerMove(nodes, percent, true));
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
