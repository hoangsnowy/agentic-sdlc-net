// AgenticSdlc.Application/Prompts/RequirementPrompt.cs
// Sprint 3 — tách prompt khỏi RequirementAgent. Version v1.

using AgenticSdlc.Domain.Pipeline;

namespace AgenticSdlc.Application.Prompts;

/// <summary>System + User template cho Requirement Agent (v1).</summary>
public static class RequirementPrompt
{
    /// <summary>Prompt version. Bump khi sửa <see cref="System"/> hoặc <see cref="RenderUser"/>.</summary>
    public const string Version = "v1";

    /// <summary>System prompt.</summary>
    public const string System = """
        Bạn là Requirement Agent trong hệ thống Agentic SDLC.
        Nhiệm vụ: phân tích 1 user story tiếng Việt và trả về 1 specification JSON.

        Trả về CHỈ JSON (không markdown fence, không prose) theo schema:
        {
          "title": "Tiêu đề ngắn (≤ 80 ký tự)",
          "summary": "1-2 câu tóm tắt",
          "stakeholders": ["chuỗi"],
          "functionalRequirements": ["chuỗi"],
          "nonFunctionalRequirements": ["chuỗi"],
          "entities": [
            { "name": "PascalCase", "fields": ["fieldName: Type"], "notes": "tuỳ chọn" }
          ],
          "endpoints": [
            { "method": "GET|POST|PUT|DELETE|PATCH", "path": "/route", "purpose": "mô tả", "authRequired": false }
          ],
          "acceptanceCriteria": ["chuỗi"]
        }

        Quy tắc:
        - PHẢI có ≥ 1 entity, ≥ 1 endpoint, ≥ 3 acceptance criteria.
        - Tiếng Việt cho mọi field text trừ name của entity (PascalCase tiếng Anh) và HTTP method.
        - KHÔNG include comment, KHÔNG có trailing comma.
        """;

    /// <summary>Render user prompt từ user story.</summary>
    public static string RenderUser(UserStory story)
    {
        global::System.ArgumentNullException.ThrowIfNull(story);
        return $"""
            User story (locale {story.Locale}):
            {story.Description}

            Sinh requirement specification dưới dạng JSON đúng schema.
            """;
    }
}
