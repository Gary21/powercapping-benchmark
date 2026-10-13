using server.Models;
using server.Services;
using Xunit;

namespace powercapping_benchmark.tests;

public class ScheduleRequestValidatorTests
{
    // timeControl, timeIncrement, timePerMove, nodesPerMove, expected mode (null => invalid request)
    public static TheoryData<int?, int?, int?, long?, string?> Cases => new()
    {
        { 60000, 1000, null, null, LimitModes.TimeControl },
        { 60000, 0, null, null, LimitModes.TimeControl },
        { null, null, 500, null, LimitModes.TimePerMove },
        { null, null, null, 1_000_000L, LimitModes.NodesPerMove },
        { null, null, null, 5_000_000_000L, LimitModes.NodesPerMove },
        { null, null, null, null, null },        // nothing
        { 60000, null, null, null, null },       // partial clock
        { null, 1000, null, null, null },        // partial clock
        { 60000, 0, 500, null, null },           // clock + time per move
        { 60000, 0, null, 1000L, null },         // clock + nodes
        { null, null, 500, 1000L, null },        // time per move + nodes
        { 60000, 0, 500, 1000L, null },          // everything
        { null, 0, 500, null, null },            // increment alone is not a clock
        { 0, 0, null, null, null },              // timeControl must be > 0
        { -1, 0, null, null, null },
        { 60000, -1, null, null, null },         // negative increment
        { null, null, 0, null, null },           // timePerMove must be > 0
        { null, null, -5, null, null },
        { null, null, null, 0L, null },          // nodesPerMove must be > 0
        { null, null, null, -1L, null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Validate_infers_the_mode_or_rejects(int? timeControl, int? timeIncrement, int? timePerMove, long? nodesPerMove, string? expectedMode)
    {
        var (error, mode) = ScheduleRequestValidator.Validate(timeControl, timeIncrement, timePerMove, nodesPerMove);

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
        var (error, _) = ScheduleRequestValidator.Validate(null, null, 500, 1000);

        Assert.Contains("timeControl", error);
        Assert.Contains("timePerMove", error);
        Assert.Contains("nodesPerMove", error);
    }
}
