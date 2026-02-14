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
    LaneD = 4,
    LaneE = 5
}

public enum ProviderType
{
    AzureOpenAi = 1,
    AzureSearch = 2,
    Blob = 3
}

public enum AuthMode
{
    ApiKey = 1,
    ManagedIdentity = 2
}

public enum UserRole
{
    PlatformAdmin = 1,
    TenantAdmin = 2,
    ProjectUser = 3
}
