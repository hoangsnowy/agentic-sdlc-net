// AgenticSdlc.Web/Services/Demo/DemoAwareLlmClientFactory.cs
// Phase 7 — Factory bọc ngoài LlmClientFactory thật. Nếu circuit đang ở chế độ Demo
// (DemoRunContext.UseDemo) thì trả DemoLlmClient; ngược lại uỷ quyền cho factory gốc
// để dùng Claude / Azure OpenAI theo cấu hình từng agent. Giữ nguyên nguyên tắc
// Platform-Agnostic của luận văn (đổi nguồn không sửa agent).

using System;
using AgenticSdlc.Domain.Llm;
using AgenticSdlc.Infrastructure.Llm;

namespace AgenticSdlc.Web.Services.Demo;

/// <summary>Factory nhận biết chế độ Demo, override <see cref="ILlmClientFactory"/> mặc định.</summary>
public sealed class DemoAwareLlmClientFactory : ILlmClientFactory
{
    private readonly DemoRunContext _context;
    private readonly DemoLlmClient _demo;
    private readonly LlmClientFactory _inner;

    /// <summary>Khởi tạo với ngữ cảnh circuit, client demo, và factory thật để uỷ quyền.</summary>
    public DemoAwareLlmClientFactory(DemoRunContext context, DemoLlmClient demo, LlmClientFactory inner)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _demo = demo ?? throw new ArgumentNullException(nameof(demo));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc />
    public ILlmClient CreateDefault()
        => _context.UseDemo ? _demo : _inner.CreateDefault();

    /// <inheritdoc />
    public ILlmClient Create(string providerName)
        => _context.UseDemo ? _demo : _inner.Create(providerName);
}
