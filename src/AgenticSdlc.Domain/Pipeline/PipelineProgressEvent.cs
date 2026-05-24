// AgenticSdlc.Domain/Pipeline/PipelineProgressEvent.cs
// Phase 7 — Sự kiện tiến trình do PipelineOrchestrator phát ra trong lúc chạy.
// Cho phép lớp trình diễn (Blazor) hiển thị realtime từng bước của QA loop.

namespace AgenticSdlc.Domain.Pipeline;

/// <summary>
/// Một mốc tiến trình của pipeline 5-tác-tử. Orchestrator phát sự kiện này ở
/// đầu/cuối mỗi bước (Requirement → vòng lặp Coding/Testing/Qa → tổng hợp) để
/// UI dựng timeline realtime. Sự kiện là bất biến (immutable) và serializable.
/// </summary>
/// <param name="Stage">Bước đang chạy.</param>
/// <param name="Phase">Pha của bước (bắt đầu / hoàn tất / lỗi).</param>
/// <param name="Iteration">Số thứ tự vòng QA (<c>0</c> cho bước Requirement tiền-vòng-lặp; <c>1..N</c> trong loop).</param>
/// <param name="MaxIterations">Giới hạn vòng lặp đã clamp — phục vụ hiển thị "vòng N/Max".</param>
/// <param name="Message">Mô tả ngắn, sẵn sàng hiển thị cho người dùng.</param>
/// <param name="QaScore">Điểm QA <c>[0.0, 1.0]</c> — chỉ có ở <see cref="PipelineStage.Qa"/> pha <see cref="PipelinePhase.Completed"/>.</param>
/// <param name="QaConsistent">Cờ nhất quán QA — chỉ có ở bước QA hoàn tất.</param>
/// <param name="Metrics">Metric của bước vừa hoàn tất (token / cost / latency); <c>null</c> ở pha bắt đầu.</param>
/// <param name="TimestampUtc">Thời điểm phát sự kiện (UTC).</param>
public sealed record PipelineProgressEvent(
    PipelineStage Stage,
    PipelinePhase Phase,
    int Iteration,
    int MaxIterations,
    string Message,
    double? QaScore = null,
    bool? QaConsistent = null,
    AgentMetrics? Metrics = null,
    System.DateTime? TimestampUtc = null)
{
    /// <summary>Thời điểm phát — mặc định <see cref="System.DateTime.UtcNow"/> nếu không truyền.</summary>
    public System.DateTime OccurredAtUtc => TimestampUtc ?? System.DateTime.UtcNow;
}

/// <summary>Bước trong pipeline 5-tác-tử (ứng với KC1–KC5, Mục 2.4 luận văn).</summary>
public enum PipelineStage
{
    /// <summary>KC1 — Requirement Agent phân tích user story.</summary>
    Requirement,

    /// <summary>KC2 — Coding Agent sinh source code.</summary>
    Coding,

    /// <summary>KC3 — Testing Agent sinh test case.</summary>
    Testing,

    /// <summary>KC5 — QA Agent đánh giá nhất quán.</summary>
    Qa,

    /// <summary>Tổng hợp metric + chốt kết quả cuối pipeline.</summary>
    Aggregate,
}

/// <summary>Pha của một bước.</summary>
public enum PipelinePhase
{
    /// <summary>Bước vừa bắt đầu.</summary>
    Started,

    /// <summary>Bước hoàn tất thành công.</summary>
    Completed,

    /// <summary>Bước thất bại (LLM exception, output sai định dạng, ...).</summary>
    Failed,
}
