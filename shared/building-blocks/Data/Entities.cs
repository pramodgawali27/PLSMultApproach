using PLS.Contracts;

namespace PLS.BuildingBlocks.Data;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class User
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.ProjectUser;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Subscription
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string PlanName { get; set; } = "Free";
    public int EnabledLanes { get; set; }
    public int TokenQuotaMonthly { get; set; }
    public DateTimeOffset ActiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActiveTo { get; set; }
}

public sealed class Document
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageUri { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? TherapeuticArea { get; set; }
    public string? Audience { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;
}

public sealed class SummaryJob
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid DocumentId { get; set; }
    public SummaryLane Lane { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? PromptOverride { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class Summary
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid JobId { get; set; }
    public SummaryLane Lane { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public string? ModelInfoJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DocumentChunk
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public int? Page { get; set; }
    public string? Section { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string? Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RetrievalRun
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public SummaryLane Lane { get; set; }
    public string Query { get; set; } = string.Empty;
    public string HitsJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? MetadataJson { get; set; }
}

public sealed class ProviderConfig
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProviderType ProviderType { get; set; }
    public AuthMode AuthMode { get; set; } = AuthMode.ApiKey;
    public string? Endpoint { get; set; }
    public string? DeploymentName { get; set; }
    public string? EmbeddingDeploymentName { get; set; }
    public string? ImageDeploymentName { get; set; }
    public string? IndexName { get; set; }
    public string? AccountName { get; set; }
    public string? ContainerName { get; set; }
    public string? SecretRefId { get; set; }
    public string? PublicAccessMode { get; set; }
    public bool UseSasLinks { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LaneRecipe
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SummaryLane LaneType { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string ProviderRefsJson { get; set; } = "{}";
    public string LaneSettingsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
