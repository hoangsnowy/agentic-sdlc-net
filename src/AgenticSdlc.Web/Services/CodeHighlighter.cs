// AgenticSdlc.Web/Services/CodeHighlighter.cs
// Phase 7 — Tô màu cú pháp C# "nhẹ" cho hiển thị read-only. An toàn XSS: HTML-encode
// trước, rồi 1 lượt regex alternation (comment > string > keyword > number) nên mỗi ký tự
// thuộc tối đa 1 token, không lồng nhau.

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace AgenticSdlc.Web.Services;

/// <summary>Bộ tô màu cú pháp tối giản, trả <see cref="MarkupString"/> đã an toàn để render.</summary>
public static partial class CodeHighlighter
{
    /// <summary>Tô màu một đoạn mã C#. Trả MarkupString (đã HTML-encode + bọc span).</summary>
    public static MarkupString Highlight(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return new MarkupString(string.Empty);
        }

        var encoded = WebUtility.HtmlEncode(code);
        var html = TokenRegex().Replace(encoded, static m =>
        {
            if (m.Groups["cm"].Success)
            {
                return $"<span class=\"cm\">{m.Value}</span>";
            }
            if (m.Groups["str"].Success)
            {
                return $"<span class=\"str\">{m.Value}</span>";
            }
            if (m.Groups["kw"].Success)
            {
                return $"<span class=\"kw\">{m.Value}</span>";
            }
            if (m.Groups["num"].Success)
            {
                return $"<span class=\"num\">{m.Value}</span>";
            }
            return m.Value;
        });

        return new MarkupString(html);
    }

    [GeneratedRegex(
        @"(?<cm>//[^\n]*)|(?<str>&quot;.*?&quot;)|(?<kw>\b(?:public|private|protected|internal|sealed|static|class|interface|record|namespace|using|return|new|async|await|var|void|if|else|for|foreach|while|true|false|null|required|init|get|set|this)\b)|(?<num>\b\d+m?\b)")]
    private static partial Regex TokenRegex();
}
