namespace PLS.Contracts;

public enum DocumentStatus
{
    Uploaded = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

public enum JobStatus
{
    Queued = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

public enum SummaryLane
{
    LaneA = 1,
    LaneB = 2,
    LaneC = 3,
    LaneD = 4
}
