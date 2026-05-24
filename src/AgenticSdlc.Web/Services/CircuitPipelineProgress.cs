// AgenticSdlc.Web/Services/CircuitPipelineProgress.cs
// Phase 7 — Cổng phát tiến trình theo từng circuit Blazor. Orchestrator (resolve trong
// cùng scope circuit) báo sự kiện vào đây; trang Studio đăng ký Listener để re-render realtime.

using System;
using System.Threading;
using System.Threading.Tasks;
using AgenticSdlc.Application.Pipeline;
using AgenticSdlc.Domain.Pipeline;

namespace AgenticSdlc.Web.Services;

/// <summary>
/// Impl <see cref="IPipelineProgressSink"/> theo scope circuit. Không tự đụng tới UI —
/// chỉ chuyển tiếp sự kiện cho <see cref="Listener"/> mà component cài đặt (component chịu
/// trách nhiệm gọi <c>InvokeAsync(StateHasChanged)</c> để render đúng luồng đồng bộ của Blazor).
/// </summary>
public sealed class CircuitPipelineProgress : IPipelineProgressSink
{
    /// <summary>Callback do component đăng ký. <c>null</c> ⇒ bỏ qua (không có ai nghe).</summary>
    public Func<PipelineProgressEvent, Task>? Listener { get; set; }

    /// <inheritdoc />
    public async ValueTask ReportAsync(PipelineProgressEvent progress, CancellationToken cancellationToken = default)
    {
        var listener = Listener;
        if (listener is not null)
        {
            await listener(progress).ConfigureAwait(false);
        }
    }
}
