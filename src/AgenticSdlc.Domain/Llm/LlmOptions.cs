// AgenticSdlc.Domain/Llm/LlmOptions.cs
// Sprint 1 — Options binding cho LLM Gateway (bind từ appsettings "Llm" section).

namespace AgenticSdlc.Domain.Llm;

/// <summary>
/// Root options bind từ <c>appsettings.json</c> section <c>"Llm"</c>.
/// </summary>
public sealed class LlmOptions
{
    /// <summary>Section name dùng cho <c>Configuration.GetSection(...)</c>.</summary>
    public const string SectionName = "Llm";

    /// <summary>Provider mặc định: <c>"Claude"</c>, <c>"AzureOpenAI"</c>, hoặc <c>"Mock"</c>.</summary>
    public string Provider { get; set; } = "Mock";

    /// <summary>Cấu hình client Claude (Anthropic).</summary>
    public ClaudeOptions Claude { get; set; } = new();

    /// <summary>Cấu hình client Azure OpenAI.</summary>
    public AzureOpenAiOptions AzureOpenAi { get; set; } = new();

    /// <summary>Cấu hình client Mock (fixture-based, dùng cho test offline).</summary>
    public MockOptions Mock { get; set; } = new();
}

/// <summary>Options cho Claude (Anthropic Messages API).</summary>
public sealed class ClaudeOptions
{
    /// <summary>API key (đặt qua user-secrets hoặc env var, KHÔNG commit).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Base URL của Anthropic API.</summary>
    public string Endpoint { get; set; } = "https://api.anthropic.com";

    /// <summary>Model mặc định (ví dụ <c>claude-sonnet-4-20250514</c>).</summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>Header <c>anthropic-version</c>.</summary>
    public string ApiVersion { get; set; } = "2023-06-01";

    /// <summary>Số retry tối đa khi gặp 429/5xx/timeout.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Timeout HTTP cho mỗi request (giây).</summary>
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>Options cho Azure OpenAI.</summary>
public sealed class AzureOpenAiOptions
{
    /// <summary>API key Azure OpenAI.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Endpoint Azure OpenAI (ví dụ <c>https://my-resource.openai.azure.com</c>).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Deployment name / model alias.</summary>
    public string Model { get; set; } = "gpt-4.1";

    /// <summary>API version (ví dụ <c>2024-10-21</c>).</summary>
    public string ApiVersion { get; set; } = "2024-10-21";

    /// <summary>Số retry tối đa.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Timeout (giây).</summary>
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>Options cho Mock client.</summary>
public sealed class MockOptions
{
    /// <summary>Đường dẫn (tương đối hoặc tuyệt đối) tới folder chứa fixture JSON.</summary>
    public string FixturePath { get; set; } = "tests/fixtures/llm";

    /// <summary>Latency giả lập (ms) khi trả về fixture, để test code hạ nguồn không assume zero-latency.</summary>
    public int SimulatedLatencyMs { get; set; } = 5;
}
