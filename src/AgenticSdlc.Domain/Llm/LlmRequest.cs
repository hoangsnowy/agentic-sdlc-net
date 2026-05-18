// AgenticSdlc.Domain/Llm/LlmRequest.cs
// Sprint 1 — LLM Gateway contract (request side).

namespace AgenticSdlc.Domain.Llm;

/// <summary>
/// Request gửi vào <see cref="ILlmClient.SendAsync(LlmRequest, System.Threading.CancellationToken)"/>.
/// Đây là DTO trung lập với provider — <see cref="ILlmClient"/> sẽ map sang shape của Anthropic / Azure OpenAI.
/// </summary>
/// <param name="SystemPrompt">System / role prompt áp cho agent. Không được null nhưng có thể rỗng.</param>
/// <param name="UserPrompt">User prompt. Bắt buộc, không rỗng.</param>
/// <param name="Model">Tên model (ví dụ "claude-sonnet-4-20250514", "gpt-4.1"). Bắt buộc.</param>
/// <param name="Temperature">Sampling temperature. Mặc định 0 (deterministic).</param>
/// <param name="MaxTokens">Giới hạn output token. Mặc định 4096.</param>
/// <param name="JsonSchema">JSON schema (optional) cho structured output. Null = free-form text.</param>
public sealed record LlmRequest(
    string SystemPrompt,
    string UserPrompt,
    string Model,
    double Temperature = 0.0,
    int MaxTokens = 4096,
    string? JsonSchema = null)
{
    /// <summary>
    /// Validate giá trị bắt buộc. Ném <see cref="System.ArgumentException"/> nếu sai.
    /// </summary>
    public void Validate()
    {
        if (SystemPrompt is null)
        {
            throw new System.ArgumentException("SystemPrompt must not be null.", nameof(SystemPrompt));
        }

        if (string.IsNullOrWhiteSpace(UserPrompt))
        {
            throw new System.ArgumentException("UserPrompt must not be null or whitespace.", nameof(UserPrompt));
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new System.ArgumentException("Model must not be null or whitespace.", nameof(Model));
        }

        if (Temperature is < 0.0 or > 2.0)
        {
            throw new System.ArgumentException("Temperature must be in [0, 2].", nameof(Temperature));
        }

        if (MaxTokens <= 0)
        {
            throw new System.ArgumentException("MaxTokens must be positive.", nameof(MaxTokens));
        }
    }
}
