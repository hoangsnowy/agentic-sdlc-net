// Persistence entity cho Agent Studio orchestration graph (thay App_Data/orchestrations.json).
namespace AgenticSdlc.Infrastructure.Persistence.Entities;

internal sealed class OrchestrationEntity
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Full OrchestrationGraph serialize JSON (cột jsonb).</summary>
    public string DefinitionJson { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
