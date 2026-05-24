// AgenticSdlc.Application/Pipeline/IPipelineProgressSink.cs
// Phase 7 — Cổng phát tiến trình. Orchestrator depend on abstraction này; impl
// thật do lớp trình diễn (Blazor) cung cấp, hoặc NullPipelineProgressSink (no-op).

using System.Threading;
using System.Threading.Tasks;
using AgenticSdlc.Domain.Pipeline;

namespace AgenticSdlc.Application.Pipeline;

/// <summary>
/// Nơi nhận <see cref="PipelineProgressEvent"/> mà <c>PipelineOrchestrator</c> phát ra.
/// Tách biệt orchestration khỏi transport hiển thị: API/test dùng bản no-op,
/// Blazor dùng bản đẩy sự kiện xuống circuit để render realtime.
/// </summary>
public interface IPipelineProgressSink
{
    /// <summary>Báo một mốc tiến trình. Impl phải không ném lỗi làm gãy pipeline.</summary>
    /// <param name="progress">Sự kiện tiến trình.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    ValueTask ReportAsync(PipelineProgressEvent progress, CancellationToken cancellationToken = default);
}
