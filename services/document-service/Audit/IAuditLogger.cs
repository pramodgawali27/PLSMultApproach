using Microsoft.AspNetCore.Http;

interface IAuditLogger
{
    Task LogAsync(HttpContext context, string action, string entityType, string entityId, object? metadata);
}
