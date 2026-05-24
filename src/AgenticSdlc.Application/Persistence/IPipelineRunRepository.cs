// Repository cho pipeline run + artifact + metrics. Interface ở Application (Clean Arch),
// impl EF Core ở Infrastructure. Domain thuần, không biết DB.
using AgenticSdlc.Application.Metrics;
using AgenticSdlc.Domain.Pipeline;

namespace AgenticSdlc.Application.Persistence;

/// <summary>Lưu + truy vấn lịch sử pipeline run.</summary>
public interface IPipelineRunRepository
{
    /// <summary>Lưu 1 run (full PipelineResult + danh sách RunMetric per LLM call).</summary>
    Task SaveAsync(PipelineRunRecord record, CancellationToken ct = default);

    /// <summary>Lấy 1 run đầy đủ theo Id (null nếu không có).</summary>
    Task<PipelineRunRecord?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Danh sách run gần nhất (summary, không kèm artifact json).</summary>
    Task<IReadOnlyList<PipelineRunSummary>> ListAsync(int limit = 50, CancellationToken ct = default);
}

/// <summary>1 run đầy đủ để lưu / đọc lại.</summary>
public sealed record PipelineRunRecord(
    Guid Id,
    PipelineResult Result,
    IReadOnlyList<RunMetric> Metrics,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset CompletedAtUtc);

/// <summary>Tóm tắt 1 run cho danh sách lịch sử.</summary>
public sealed record PipelineRunSummary(
    Guid Id,
    string Status,
    decimal TotalCostUsd,
    int IterationCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string UserStoryPreview);
