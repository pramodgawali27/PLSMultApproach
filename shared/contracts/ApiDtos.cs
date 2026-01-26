namespace PLS.Contracts;

public sealed record DevLoginRequest(string Email, string TenantName);

public sealed record DevLoginResponse(string AccessToken, Guid TenantId, Guid UserId);

public sealed record DocumentUploadResponse(Guid DocumentId, Guid JobId);

public sealed record SummaryGenerateResponse(Guid JobId);

public sealed record JobStatusResponse(Guid JobId, JobStatus Status, string? ErrorMessage);

public sealed record SummaryResponse(
    Guid SummaryId,
    Guid DocumentId,
    SummaryLane Lane,
    string SummaryText,
    string? ModelInfoJson,
    DateTimeOffset CreatedAt);

public sealed record SummaryListItem(
    Guid SummaryId,
    Guid DocumentId,
    SummaryLane Lane,
    JobStatus Status,
    string FileName,
    DateTimeOffset CreatedAt);

public sealed record EntitlementsResponse(IReadOnlyList<SummaryLane> EnabledLanes);
