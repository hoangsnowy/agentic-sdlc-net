// Decorator quanh IOrchestratorAgent: sinh RunId, set MetricsContext (để per-call RunMetric
// mang RunId), chạy pipeline, rồi lưu PipelineResult + metrics vào DB. Persist là best-effort —
// lỗi DB không làm hỏng kết quả run đã thành công.
using AgenticSdlc.Application.Agents;
using AgenticSdlc.Application.Metrics;
using AgenticSdlc.Application.Persistence;
using AgenticSdlc.Domain.Pipeline;
using Microsoft.Extensions.Logging;

namespace AgenticSdlc.Infrastructure.Orchestration;

internal sealed class PersistingOrchestratorAgent : IOrchestratorAgent
{
    private readonly IOrchestratorAgent _inner;
    private readonly IPipelineRunRepository _repository;
    private readonly IMetricsCollector _metrics;
    private readonly TimeProvider _clock;
    private readonly ILogger<PersistingOrchestratorAgent> _logger;

    public PersistingOrchestratorAgent(
        IOrchestratorAgent inner,
        IPipelineRunRepository repository,
        IMetricsCollector metrics,
        TimeProvider clock,
        ILogger<PersistingOrchestratorAgent> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PipelineResult> RunAsync(UserStory story, CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        var createdAtUtc = _clock.GetUtcNow();

        PipelineResult result;
        using (MetricsContext.BeginScope(runId.ToString(), "ad-hoc"))
        {
            result = await _inner.RunAsync(story, cancellationToken).ConfigureAwait(false);
        }

        var completedAtUtc = _clock.GetUtcNow();
        await PersistAsync(runId, result, createdAtUtc, completedAtUtc, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task PersistAsync(
        Guid runId,
        PipelineResult result,
        DateTimeOffset createdAtUtc,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var runKey = runId.ToString();
            var runMetrics = _metrics.Snapshot()
                .Where(m => string.Equals(m.RunId, runKey, StringComparison.Ordinal))
                .ToList();

            await _repository.SaveAsync(
                new PipelineRunRecord(runId, result, runMetrics, createdAtUtc, completedAtUtc),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Persist best-effort: lỗi DB không được làm hỏng run đã thành công.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Lưu pipeline run {RunId} thất bại — kết quả vẫn trả về cho client.", runId);
        }
    }
}
