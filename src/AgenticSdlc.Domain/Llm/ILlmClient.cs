// AgenticSdlc.Domain/Llm/ILlmClient.cs
// Sprint 1 — Contract cho LLM Gateway (Domain layer, không reference Infrastructure).

namespace AgenticSdlc.Domain.Llm;

/// <summary>
/// Abstraction cho mọi LLM provider (Claude, Azure OpenAI, Mock, ...).
/// 5 agent (Requirement / Coding / Testing / QA / Orchestrator) depend on interface này — KHÔNG depend on
/// concrete client — tuân theo Dependency Inversion Principle (Mục 2.4.2 luận văn).
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Provider tag, dùng cho logging và <see cref="LlmResponse.Provider"/>.
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// Gửi 1 request tới provider và trả về response. Mọi retry/timeout logic phải đã được handle bên trong.
    /// Ném <see cref="LlmException"/> khi đã exhausted retry hoặc gặp lỗi không recoverable.
    /// </summary>
    /// <param name="request">Request đã validate.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Response với content, token usage, cost, latency.</returns>
    System.Threading.Tasks.Task<LlmResponse> SendAsync(
        LlmRequest request,
        System.Threading.CancellationToken cancellationToken = default);
}
