using server.Models;
using server.Services;
using Xunit;

namespace powercapping_benchmark.tests;

public class ScheduleRequestValidatorTests
{
    // timeControl, timeIncrement, timePerMove, nodesPerMove, powerCapPercent, expected mode (null => invalid request)
    public static TheoryData<int?, int?, int?, long?, int, string?> Cases => new()
    {
        { 60000, 1000, null, null, 50, LimitModes.TimeControl },
        { 60000, 0, null, null, 50, LimitModes.TimeControl },
        { null, null, 500, null, 50, LimitModes.TimePerMove },
        { null, null, null, 1_000_000L, 50, LimitModes.NodesPerMove },
        { null, null, null, 5_000_000_000L, 50, LimitModes.NodesPerMove },
        { null, null, null, null, 50, null },        // nothing
        { 60000, null, null, null, 50, null },       // partial clock
        { null, 1000, null, null, 50, null },        // partial clock
        { 60000, 0, 500, null, 50, null },           // clock + time per move
        { 60000, 0, null, 1000L, 50, null },         // clock + nodes
        { null, null, 500, 1000L, 50, null },        // time per move + nodes
        { 60000, 0, 500, 1000L, 50, null },          // everything
        { null, 0, 500, null, 50, null },            // increment alone is not a clock
        { 0, 0, null, null, 50, null },              // timeControl must be > 0
        { -1, 0, null, null, 50, null },
        { 60000, -1, null, null, 50, null },         // negative increment
        { null, null, 0, null, 50, null },           // timePerMove must be > 0
        { null, null, -5, null, 50, null },
        { null, null, null, 0L, 50, null },          // nodesPerMove must be > 0
        { null, null, null, -1L, 50, null },

        // powerCapPercent scales the node budget in nodes_per_move, so it must be 1..100 there
        { null, null, null, 1000L, 1, LimitModes.NodesPerMove },
        { null, null, null, 1000L, 100, LimitModes.NodesPerMove },
        { null, null, null, 1000L, 0, null },
        { null, null, null, 1000L, -1, null },
        { null, null, null, 1000L, 101, null },
        { null, null, null, 1000L, 150, null },

        // the other modes keep a hardware cap and are not range-checked, exactly as before
        { 60000, 1000, null, null, 0, LimitModes.TimeControl },
        { 60000, 1000, null, null, 150, LimitModes.TimeControl },
        { null, null, 500, null, 0, LimitModes.TimePerMove },
        { null, null, 500, null, 150, LimitModes.TimePerMove },
        { null, null, 500, null, -5, LimitModes.TimePerMove },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Validate_infers_the_mode_or_rejects(int? timeControl, int? timeIncrement, int? timePerMove, long? nodesPerMove, int powerCapPercent, string? expectedMode)
    {
        var (error, mode) = ScheduleRequestValidator.Validate(timeControl, timeIncrement, timePerMove, nodesPerMove, powerCapPercent);

        if (expectedMode == null)
        {
            Assert.False(string.IsNullOrWhiteSpace(error));
            Assert.Null(mode);
        }
        else
        {
            Assert.Null(error);
            Assert.Equal(expectedMode, mode);
        }
    }

    [Fact]
    public void Validate_error_lists_the_allowed_combinations()
    {
        var (error, _) = ScheduleRequestValidator.Validate(null, null, 500, 1000, 50);

        Assert.Contains("timeControl", error);
        Assert.Contains("timePerMove", error);
        Assert.Contains("nodesPerMove", error);
    }

    [Fact]
    public void Validate_percent_error_names_the_parameter_and_the_mode()
    {
        var (error, _) = ScheduleRequestValidator.Validate(null, null, null, 1000, 150);

        Assert.Contains("powerCapPercent", error);
        Assert.Contains("nodes_per_move", error);
    }

    [Fact]
    public void Validate_reports_an_invalid_node_budget_before_the_percent()
    {
        var (error, _) = ScheduleRequestValidator.Validate(null, null, null, 0, 0);

        Assert.Contains("nodesPerMove", error);
        Assert.DoesNotContain("powerCapPercent", error);
    }
}
