// Audit-trail row for tenant-scoped mutations. Append-only: writers never UPDATE/DELETE these.
// Stored in the tenants schema alongside the registry so the same DbContext owns the migration.

using System;

namespace AgentOs.Modules.Tenants.Persistence.Entities;

internal sealed class AuditEventEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
}
