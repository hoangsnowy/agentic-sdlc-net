// AgenticSdlc.Application/Prompts/CodingPrompt.cs
// Sprint 3 — tách prompt khỏi CodingAgent. Version v1.

using System.Text;
using System.Text.Json;
using AgenticSdlc.Domain.Qa;
using AgenticSdlc.Domain.Requirements;

namespace AgenticSdlc.Application.Prompts;

/// <summary>System + User template cho Coding Agent (v1).</summary>
public static class CodingPrompt
{
    /// <summary>Prompt version.</summary>
    public const string Version = "v1";

    /// <summary>System prompt.</summary>
    public const string System = """
        Bạn là Coding Agent trong hệ thống Agentic SDLC.
        Sinh source code C# (.NET 10) theo kiến trúc Clean Architecture cho specification user cung cấp.

        Trả về CHỈ JSON theo schema:
        {
          "projectName": "PascalCase",
          "architecture": "Clean Architecture",
          "files": [
            { "path": "src/<Layer>/<File>.cs", "content": "<source code>", "language": "csharp" }
          ],
          "notes": "Ghi chú giả định / TODO (tiếng Việt)"
        }

        Quy tắc:
        - PHẢI có ≥ 1 entity class trong layer Domain.
        - PHẢI có ≥ 1 controller hoặc minimal API endpoint trong layer Api.
        - Code phải compile với .NET 10 (nullable enable, file-scoped namespace).
        - Path dùng forward slash.
        - Nếu có previousFeedback: ưu tiên fix mọi issue Severity Critical/Major trong feedback.
        - KHÔNG markdown fence quanh JSON, KHÔNG prose trước/sau.
        """;

    /// <summary>Render user prompt từ spec + optional QA feedback.</summary>
    public static string RenderUser(RequirementSpec spec, QaReport? previousFeedback = null)
    {
        global::System.ArgumentNullException.ThrowIfNull(spec);

        var sb = new StringBuilder();
        sb.AppendLine("Specification (JSON):");
        sb.AppendLine(JsonSerializer.Serialize(new
        {
            spec.Title,
            spec.Summary,
            spec.Entities,
            spec.Endpoints,
            spec.AcceptanceCriteria,
        }, PromptJson.Default));

        if (previousFeedback is not null)
        {
            sb.AppendLine();
            sb.AppendLine("Previous QA feedback (cần fix trong lần này):");
            sb.AppendLine(JsonSerializer.Serialize(new
            {
                previousFeedback.Score,
                previousFeedback.Issues,
                previousFeedback.Recommendations,
            }, PromptJson.Default));
        }

        sb.AppendLine();
        sb.AppendLine("Sinh CodeArtifact JSON.");
        return sb.ToString();
    }
}
