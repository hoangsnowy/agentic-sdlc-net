// AgenticSdlc.Web/Services/Demo/DemoRunContext.cs
// Phase 7 — Trạng thái theo circuit, quyết định lần chạy hiện tại dùng nguồn LLM nào.

namespace AgenticSdlc.Web.Services.Demo;

/// <summary>
/// Ngữ cảnh 1 lần chạy pipeline trên 1 circuit Blazor. Trang Studio đặt
/// <see cref="UseDemo"/> trước khi resolve orchestrator; <see cref="DemoAwareLlmClientFactory"/>
/// đọc cờ này để chọn DemoLlmClient (offline) hay client thật.
/// </summary>
public sealed class DemoRunContext
{
    /// <summary>
    /// <c>true</c> ⇒ mọi agent dùng <see cref="DemoLlmClient"/> (JSON canned, chạy offline).
    /// <c>false</c> ⇒ dùng provider thật theo cấu hình từng agent (Claude / Azure OpenAI).
    /// </summary>
    public bool UseDemo { get; set; } = true;
}
