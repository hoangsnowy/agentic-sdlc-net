// AgenticSdlc.Application/Prompts/PromptJson.cs
// Shared JsonSerializerOptions cho mọi prompt renderer (camelCase, indented, unicode-safe).

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace AgenticSdlc.Application.Prompts;

/// <summary>JSON options chuẩn dùng khi render user prompt (camelCase, indented, không escape unicode).</summary>
public static class PromptJson
{
    /// <summary>Default options dùng cho mọi prompt.</summary>
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };
}
