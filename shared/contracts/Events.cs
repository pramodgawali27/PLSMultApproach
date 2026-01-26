namespace PLS.Contracts;

public sealed record DocumentUploadedEvent(
    Guid DocumentId,
    Guid TenantId,
    Guid UserId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StorageUri,
    DateTimeOffset UploadedAt);

public sealed record SummaryJobRequestedEvent(
    Guid JobId,
    Guid DocumentId,
    Guid TenantId,
    Guid UserId,
    SummaryLane Lane,
    DateTimeOffset RequestedAt,
    string CorrelationId);
