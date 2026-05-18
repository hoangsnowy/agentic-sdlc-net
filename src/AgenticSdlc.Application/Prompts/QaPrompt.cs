// AgenticSdlc.Application/Prompts/QaPrompt.cs
// Sprint 3 — tách prompt khỏi QaAgent. Version v1.

using System.Linq;
using System.Text;
using System.Text.Json;
using AgenticSdlc.Domain.Code;
using AgenticSdlc.Domain.Requirements;
using AgenticSdlc.Domain.Testing;

namespace AgenticSdlc.Application.Prompts;

/// <summary>System + User template cho QA Agent (v1).</summary>
public static class QaPrompt
{
    /// <summary>Prompt version.</summary>
    public const string Version = "v1";

    /// <summary>System prompt.</summary>
    public const string System = """
        Bạn là QA Agent trong hệ thống Agentic SDLC.
        Đánh giá nhất quán giữa 3 artefact: RequirementSpec, CodeArtifact, TestArtifact.

        Trả về CHỈ JSON theo schema:
        {
          "score": 0.0-1.0,
          "isConsistent": true|false,
          "iterationNeeded": true|false,
          "issues": [
            {
              "severity": "Critical|Major|Minor",
              "category": "RequirementCoverage|CodeQuality|TestCoverage|Consistency",
              "description": "Tiếng Việt, ngắn gọn",
              "location": "file path hoặc requirement id (tuỳ chọn)"
            }
          ],
          "recommendations": ["Khuyến nghị cho vòng regenerate kế tiếp"]
        }

        Quy tắc chấm điểm:
        - score = 1.0 - (#Critical × 0.3 + #Major × 0.1 + #Minor × 0.03), clamp [0, 1].
        - isConsistent = (score ≥ 0.8) AND (#Critical == 0).
        - iterationNeeded = NOT isConsistent.
        - Mỗi entity trong spec PHẢI có code class tương ứng (kiểm tra theo tên).
        - Mỗi endpoint trong spec PHẢI có code map ROUTE tương ứng.
        - Mỗi acceptanceCriteria PHẢI được phản ánh trong ≥ 1 test.
        """;

    /// <summary>Render user prompt từ spec + code + tests (file excerpt rút gọn 300 chars).</summary>
    public static string RenderUser(RequirementSpec spec, CodeArtifact code, TestArtifact tests, int excerptChars = 300)
    {
        global::System.ArgumentNullException.ThrowIfNull(spec);
        global::System.ArgumentNullException.ThrowIfNull(code);
        global::System.ArgumentNullException.ThrowIfNull(tests);

        var sb = new StringBuilder();
        sb.AppendLine("Spec (entities + endpoints + acceptanceCriteria):");
        sb.AppendLine(JsonSerializer.Serialize(new { spec.Entities, spec.Endpoints, spec.AcceptanceCriteria }, PromptJson.Default));
        sb.AppendLine();
        sb.AppendLine("Code (files, abbreviated):");
        sb.AppendLine(JsonSerializer.Serialize(code.Files.Select(f => new { f.Path, Excerpt = Excerpt(f.Content, excerptChars) }), PromptJson.Default));
        sb.AppendLine();
        sb.AppendLine("Tests (files + counts):");
        sb.AppendLine(JsonSerializer.Serialize(new
        {
            tests.Framework,
            tests.HappyPathCount,
            tests.EdgeCaseCount,
            tests.ErrorCaseCount,
            tests.EstimatedCoveragePercent,
            Files = tests.Files.Select(f => new { f.Path, Excerpt = Excerpt(f.Content, excerptChars) }),
        }, PromptJson.Default));

        sb.AppendLine();
        sb.AppendLine("Sinh QaReport JSON.");
        return sb.ToString();
    }

    private static string Excerpt(string content, int maxChars)
        => string.IsNullOrEmpty(content) ? string.Empty : content.Length <= maxChars ? content : string.Concat(content.AsSpan(0, maxChars), "...");
}
