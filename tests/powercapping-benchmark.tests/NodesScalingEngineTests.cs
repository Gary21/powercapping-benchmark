using client.Services;
using server.Models;
using Xunit;

namespace powercapping_benchmark.tests;

// What each side of a nodes_per_move game really sends to its engine, and what hardware limit it runs at.
public class NodesScalingEngineTests
{
    private static readonly MoveMadeModel Move = new() { GameId = "g", CurrentMoves = "position startpos" };

    private static NewGameModel NodesGame(long nodes, int percent, string capColor) => new()
    {
        GameId = "g", LimitMode = LimitModes.NodesPerMove, NodesPerMove = nodes, PowerCap = percent, PowerCapColor = capColor
    };

    private static string GoCommandOf(bool isWhite, NewGameModel game)
        => EngineCommunication.BuildGoCommand(game.LimitMode, game.TimePerMove, EngineCommunication.ResolveNodesPerMove(isWhite, game), Move);

    [Theory]
    [InlineData(true, "white", "go nodes 200000")]    // white is capped
    [InlineData(false, "white", "go nodes 1000000")]  // black plays the full budget
    [InlineData(true, "black", "go nodes 1000000")]
    [InlineData(false, "black", "go nodes 200000")]   // black is capped
    public void Each_side_sends_its_own_node_budget_to_the_engine(bool isWhite, string capColor, string expectedCommand)
        => Assert.Equal(expectedCommand, GoCommandOf(isWhite, NodesGame(1_000_000, 20, capColor)));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Cap_of_100_percent_gives_both_sides_the_full_budget(bool isWhite)
        => Assert.Equal("go nodes 1000000", GoCommandOf(isWhite, NodesGame(1_000_000, 100, "white")));

    [Fact]
    public void Other_modes_pass_the_configured_value_through_untouched()
    {
        var game = new NewGameModel { LimitMode = LimitModes.TimePerMove, TimePerMove = 500, PowerCap = 20, PowerCapColor = "white" };

        Assert.Null(EngineCommunication.ResolveNodesPerMove(true, game));
        Assert.Equal("go movetime 500", GoCommandOf(true, game));
    }

    [Theory]
    // nodes_per_move: both sides at full power, whatever the percent and the colour are
    [InlineData(LimitModes.NodesPerMove, true, 20, "white", 100L)]
    [InlineData(LimitModes.NodesPerMove, false, 20, "white", 100L)]
    [InlineData(LimitModes.NodesPerMove, true, 20, "black", 100L)]
    [InlineData(LimitModes.NodesPerMove, false, 20, "black", 100L)]
    // the other modes: unchanged hardware cap logic (capped side gets the percent, the other side 100)
    [InlineData(LimitModes.TimePerMove, true, 20, "white", 20L)]
    [InlineData(LimitModes.TimePerMove, false, 20, "white", 100L)]
    [InlineData(LimitModes.TimePerMove, true, 20, "black", 100L)]
    [InlineData(LimitModes.TimePerMove, false, 20, "black", 20L)]
    [InlineData(LimitModes.TimeControl, true, 35, "white", 35L)]
    [InlineData(LimitModes.TimeControl, false, 35, "white", 100L)]
    [InlineData(LimitModes.TimeControl, true, 35, "black", 100L)]
    [InlineData(LimitModes.TimeControl, false, 35, "black", 35L)]
    // an unknown colour in nodes mode still means full power (nothing is capped on the hardware there)
    [InlineData(LimitModes.NodesPerMove, true, 20, "purple", 100L)]
    [InlineData(LimitModes.NodesPerMove, false, 20, "purple", 100L)]
    public void Target_hardware_limit_per_side(string mode, bool isWhite, double percent, string capColor, long expected)
        => Assert.Equal(expected, EngineCommunication.ResolveTargetLimitPercent(mode, isWhite, percent, capColor));

    [Theory]
    [InlineData(true, "purple")]
    [InlineData(false, "purple")]
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(true, "White")] // case-sensitive, like before
    public void Unknown_capped_colour_leaves_the_hardware_limit_alone_outside_nodes_mode(bool isWhite, string? colour)
        => Assert.Null(EngineCommunication.ResolveTargetLimitPercent(LimitModes.TimePerMove, isWhite, 20, colour));

    [Theory]
    [InlineData(true, "purple")]
    [InlineData(false, "purple")]
    [InlineData(true, null)]
    [InlineData(false, null)]
    public void Unknown_capped_colour_in_nodes_mode_gives_both_sides_the_full_budget(bool isWhite, string? colour)
        => Assert.Equal("go nodes 1000000", GoCommandOf(isWhite, NodesGame(1_000_000, 20, colour!)));

    [Theory]
    [InlineData(0)]
    [InlineData(150)]
    public void Constructor_rejects_a_nodes_game_with_a_percent_outside_1_to_100_before_starting_an_engine(int percent)
    {
        // the guard runs first, so the engine process, the energy service and the enforcer are never touched
        var game = NodesGame(1_000_000, percent, "white");

        Assert.Throws<ArgumentException>(() => new EngineCommunication(true, game, new PowerLimitState(), null!, null!));
    }

    [Fact]
    public void Constructor_rejects_an_unknown_limit_mode_before_starting_an_engine()
    {
        var game = new NewGameModel { LimitMode = "bogus", PowerCap = 50, PowerCapColor = "white" };

        Assert.Throws<ArgumentException>(() => new EngineCommunication(true, game, new PowerLimitState(), null!, null!));
    }
}
