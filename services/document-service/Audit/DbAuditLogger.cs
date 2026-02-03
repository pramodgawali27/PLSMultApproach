using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PLS.BuildingBlocks.Auth;
using PLS.BuildingBlocks.Data;

sealed class DbAuditLogger : IAuditLogger
{
    private readonly PlsDbContext _db;
    private readonly ILogger<DbAuditLogger> _logger;

    public DbAuditLogger(PlsDbContext db, ILogger<DbAuditLogger> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(HttpContext context, string action, string entityType, string entityId, object? metadata)
    {
        var user = context.User;
        var tenantId = user.GetTenantId();
        var userId = user.GetUserId();

        var payload = metadata is null ? null : System.Text.Json.JsonSerializer.Serialize(metadata);

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Timestamp = DateTimeOffset.UtcNow,
            MetadataJson = payload
        });

        await _db.SaveChangesAsync();
        _logger.LogInformation("Audit logged {Action} for {EntityType} {EntityId}", action, entityType, entityId);
    }
}
