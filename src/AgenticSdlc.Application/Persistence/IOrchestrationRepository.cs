// Repository cho Agent Studio orchestration graph. Lưu dạng JSON string (Web tự (de)serialize
// sang OrchestrationGraph của nó) → Application/Infrastructure không phụ thuộc Web.
namespace AgenticSdlc.Application.Persistence;

/// <summary>CRUD orchestration definition (Agent Studio editor state).</summary>
public interface IOrchestrationRepository
{
    Task<IReadOnlyList<OrchestrationRecord>> ListAsync(CancellationToken ct = default);

    Task<OrchestrationRecord?> GetAsync(string id, CancellationToken ct = default);

    Task UpsertAsync(OrchestrationRecord record, CancellationToken ct = default);

    Task DeleteAsync(string id, CancellationToken ct = default);
}

/// <summary>1 orchestration graph (DefinitionJson = full graph serialize bởi Web).</summary>
public sealed record OrchestrationRecord(
    string Id,
    string Name,
    string? Description,
    string DefinitionJson,
    DateTimeOffset UpdatedAtUtc);
