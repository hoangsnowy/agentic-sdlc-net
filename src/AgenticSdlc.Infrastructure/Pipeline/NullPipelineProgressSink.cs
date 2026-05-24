// AgenticSdlc.Infrastructure/Pipeline/NullPipelineProgressSink.cs
// Phase 7 — Impl no-op của IPipelineProgressSink. Đăng ký mặc định để API + test
// giữ nguyên hành vi (không quan tâm tiến trình). Blazor override bằng bản scoped.

using System.Threading;
using System.Threading.Tasks;
using AgenticSdlc.Application.Pipeline;
using AgenticSdlc.Domain.Pipeline;

namespace AgenticSdlc.Infrastructure.Pipeline;

/// <summary>
/// Bản rỗng — nuốt mọi sự kiện tiến trình. Dùng cho host không cần realtime
/// (API, unit test). Cũng là fallback khi orchestrator không được inject sink.
/// </summary>
public sealed class NullPipelineProgressSink : IPipelineProgressSink
{
    /// <summary>Thể hiện dùng chung (stateless, an toàn đa luồng).</summary>
    public static NullPipelineProgressSink Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask ReportAsync(PipelineProgressEvent progress, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
