// AgenticSdlc.Domain/Llm/LlmResponse.cs
// Sprint 1 — LLM Gateway contract (response side).

namespace AgenticSdlc.Domain.Llm;

/// <summary>
/// Response trả về từ <see cref="ILlmClient.SendAsync(LlmRequest, System.Threading.CancellationToken)"/>.
/// </summary>
/// <param name="Content">Text content (đã extract khỏi shape của provider). Bắt buộc, có thể rỗng.</param>
/// <param name="InputTokens">Số token input đã tiêu thụ.</param>
/// <param name="OutputTokens">Số token output đã sinh.</param>
/// <param name="CostUsd">Cost ước tính theo USD (do <c>CostCalculator</c> tính).</param>
/// <param name="Latency">Thời gian end-to-end (đo bằng <see cref="System.Diagnostics.Stopwatch"/> trong client).</param>
/// <param name="Model">Tên model đã thực sự sinh response (provider có thể alias).</param>
/// <param name="Provider">Tên provider: <c>"Claude"</c>, <c>"AzureOpenAI"</c>, hoặc <c>"Mock"</c>.</param>
public sealed record LlmResponse(
    string Content,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd,
    System.TimeSpan Latency,
    string Model,
    string Provider);
