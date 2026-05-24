// Persistence entity cho 1 pipeline run. Cột quan hệ để query (Chương 4) +
// cột jsonb ResultJson chứa full PipelineResult đã serialize.
namespace AgenticSdlc.Infrastructure.Persistence.Entities;

internal sealed class PipelineRunEntity
{
    public Guid Id { get; set; }

    public string UserStoryText { get; set; } = string.Empty;

    /// <summary>Tên enum PipelineStatus: Done / MaxIterationReached / Failed.</summary>
    public string Status { get; set; } = string.Empty;

    public decimal TotalCostUsd { get; set; }

    public int TotalTokensIn { get; set; }

    public int TotalTokensOut { get; set; }

    public int IterationCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Full PipelineResult serialize JSON (cột jsonb).</summary>
    public string ResultJson { get; set; } = string.Empty;

    public List<RunMetricEntity> Metrics { get; } = [];
}
