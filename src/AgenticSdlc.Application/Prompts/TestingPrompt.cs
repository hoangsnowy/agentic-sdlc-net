// AgenticSdlc.Application/Prompts/TestingPrompt.cs
// Sprint 3 — tách prompt khỏi TestingAgent. Version v1.

using System.Text;
using System.Text.Json;
using AgenticSdlc.Domain.Code;
using AgenticSdlc.Domain.Qa;
using AgenticSdlc.Domain.Requirements;

namespace AgenticSdlc.Application.Prompts;

/// <summary>System + User template cho Testing Agent (v1).</summary>
public static class TestingPrompt
{
    /// <summary>Prompt version.</summary>
    public const string Version = "v1";

    /// <summary>System prompt.</summary>
    public const string System = """
        Bạn là Testing Agent trong hệ thống Agentic SDLC.
        Sinh xUnit test cho code đã được Coding Agent sinh ra, dựa trên RequirementSpec.

        Trả về CHỈ JSON theo schema:
        {
          "framework": "xUnit",
          "files": [
            { "path": "tests/<File>Tests.cs", "content": "<source>", "language": "csharp" }
          ],
          "happyPathCount": 0,
          "edgeCaseCount": 0,
          "errorCaseCount": 0,
          "estimatedCoveragePercent": 0
        }

        Quy tắc:
        - Mỗi class test 1 file riêng.
        - PHẢI có ≥ 1 happy-path, ≥ 1 edge-case, ≥ 1 error-case.
        - Dùng [Theory] + [InlineData] cho test có nhiều input variation.
        - Assertion: Shouldly (vd .ShouldBe(...), .ShouldThrow<T>(...)).
        - Mocking: NSubstitute nếu cần.
        - Đảm bảo tests cover AcceptanceCriteria từ spec.
        - estimatedCoveragePercent là ước tính, KHÔNG đo thật (≥ 60 cho prototype).
        """;

    /// <summary>Render user prompt từ spec + code + optional feedback.</summary>
    public static string RenderUser(RequirementSpec spec, CodeArtifact code, QaReport? previousFeedback = null)
    {
        global::System.ArgumentNullException.ThrowIfNull(spec);
        global::System.ArgumentNullException.ThrowIfNull(code);

        var sb = new StringBuilder();
        sb.AppendLine("Specification (acceptance criteria):");
        sb.AppendLine(JsonSerializer.Serialize(spec.AcceptanceCriteria, PromptJson.Default));
        sb.AppendLine();
        sb.AppendLine($"Code đã sinh ({code.Files.Count} file):");
        foreach (var f in code.Files)
        {
            sb.AppendLine($"--- {f.Path} ---");
            sb.AppendLine(f.Content);
            sb.AppendLine();
        }

        if (previousFeedback is not null)
        {
            sb.AppendLine("Previous QA feedback (issue test coverage cần fix):");
            sb.AppendLine(JsonSerializer.Serialize(previousFeedback.Issues, PromptJson.Default));
        }

        sb.AppendLine();
        sb.AppendLine("Sinh TestArtifact JSON.");
        return sb.ToString();
    }
}
