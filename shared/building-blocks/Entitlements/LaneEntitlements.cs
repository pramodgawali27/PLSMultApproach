using PLS.Contracts;

namespace PLS.BuildingBlocks.Entitlements;

[Flags]
public enum LaneFlags
{
    None = 0,
    LaneA = 1 << 0,
    LaneB = 1 << 1,
    LaneC = 1 << 2,
    LaneD = 1 << 3
}

public static class LaneEntitlements
{
    public static IReadOnlyList<SummaryLane> ToLanes(int enabledLanes)
    {
        var lanes = new List<SummaryLane>();
        var flags = (LaneFlags)enabledLanes;
        if (flags.HasFlag(LaneFlags.LaneA)) lanes.Add(SummaryLane.LaneA);
        if (flags.HasFlag(LaneFlags.LaneB)) lanes.Add(SummaryLane.LaneB);
        if (flags.HasFlag(LaneFlags.LaneC)) lanes.Add(SummaryLane.LaneC);
        if (flags.HasFlag(LaneFlags.LaneD)) lanes.Add(SummaryLane.LaneD);
        return lanes;
    }

    public static bool Allows(int enabledLanes, SummaryLane lane)
    {
        var flag = lane switch
        {
            SummaryLane.LaneA => LaneFlags.LaneA,
            SummaryLane.LaneB => LaneFlags.LaneB,
            SummaryLane.LaneC => LaneFlags.LaneC,
            SummaryLane.LaneD => LaneFlags.LaneD,
            _ => LaneFlags.None
        };

        return ((LaneFlags)enabledLanes).HasFlag(flag);
    }
}
