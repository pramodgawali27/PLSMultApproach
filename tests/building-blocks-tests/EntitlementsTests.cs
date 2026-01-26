using PLS.BuildingBlocks.Entitlements;
using PLS.BuildingBlocks.Jobs;
using PLS.BuildingBlocks.Data;
using PLS.Contracts;

namespace BuildingBlocks.Tests;

public class EntitlementsTests
{
    [Fact]
    public void Allows_ReturnsTrue_WhenLaneEnabled()
    {
        var enabled = (int)(LaneFlags.LaneA | LaneFlags.LaneC);

        Assert.True(LaneEntitlements.Allows(enabled, SummaryLane.LaneA));
        Assert.True(LaneEntitlements.Allows(enabled, SummaryLane.LaneC));
        Assert.False(LaneEntitlements.Allows(enabled, SummaryLane.LaneB));
    }

    [Fact]
    public void JobStateMachine_Transitions_AsExpected()
    {
        var job = new SummaryJob { Status = JobStatus.Queued };

        var start = JobStateMachine.Start(job);
        Assert.True(start.IsSuccess);
        Assert.Equal(JobStatus.Processing, job.Status);

        var complete = JobStateMachine.Complete(job);
        Assert.True(complete.IsSuccess);
        Assert.Equal(JobStatus.Completed, job.Status);
    }
}
