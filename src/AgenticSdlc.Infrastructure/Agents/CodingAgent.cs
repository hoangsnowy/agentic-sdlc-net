// AgenticSdlc.Infrastructure/Agents/CodingAgent.cs
// Phase 4 — Impl ICodingAgent. Sinh source code C# Clean Architecture từ RequirementSpec.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgenticSdlc.Application.Agents;
using AgenticSdlc.Domain;
using AgenticSdlc.Domain.Code;
using AgenticSdlc.Domain.Llm;
using AgenticSdlc.Domain.Qa;
using AgenticSdlc.Domain.Requirements;
using AgenticSdlc.Infrastructure.Llm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSdlc.Infrastructure.Agents;

/// <summary>Sinh source code C# Clean Architecture từ requirement spec (+ optional QA feedback).</summary>
public sealed class CodingAgent : ICodingAgent
{
    private const string AgentName = nameof(CodingAgent);

    private const string SystemPrompt = """
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

    private readonly ILlmClient _llm;
    private readonly AgentOptions _options;
    private readonly ILogger<CodingAgent> _logger;

    /// <summary>Khởi tạo.</summary>
    public CodingAgent(ILlmClientFactory factory, IOptions<AgentsOptions> options, ILogger<CodingAgent> logger)
    {
        System.ArgumentNullException.ThrowIfNull(factory);
        System.ArgumentNullException.ThrowIfNull(options);
        _options = options.Value.Coding;
        _llm = factory.Create(_options.Provider);
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CodeArtifact> RunAsync(
        RequirementSpec spec,
        QaReport? previousFeedback = null,
        CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(spec);

        var sb = new StringBuilder();
        sb.AppendLine("Specification (JSON):");
        sb.AppendLine(JsonSerializer.Serialize(new
        {
            spec.Title,
            spec.Summary,
            spec.Entities,
            spec.Endpoints,
            spec.AcceptanceCriteria,
        }, JsonExtractor.DefaultOptions));

        if (previousFeedback is not null)
        {
            sb.AppendLine();
            sb.AppendLine("Previous QA feedback (cần fix trong lần này):");
            sb.AppendLine(JsonSerializer.Serialize(new
            {
                previousFeedback.Score,
                previousFeedback.Issues,
                previousFeedback.Recommendations,
            }, JsonExtractor.DefaultOptions));
        }

        sb.AppendLine();
        sb.AppendLine("Sinh CodeArtifact JSON.");

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: sb.ToString(),
            Model: _options.Model,
            Temperature: _options.Temperature,
            MaxTokens: _options.MaxTokens);

        var response = await _llm.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var dto = JsonExtractor.Deserialize<CodeArtifactDto>(response.Content, AgentName);
        dto.Validate(AgentName);

        var metrics = MetricsMapper.From(response);

        _logger.LogInformation(
            "{Agent} done: {InTok}→{OutTok} tokens, ${Cost} USD, {Ms}ms — {FileCount} files",
            AgentName, metrics.InputTokens, metrics.OutputTokens, metrics.CostUsd,
            metrics.Latency.TotalMilliseconds, dto.Files!.Count);

        return new CodeArtifact(
            ProjectName: dto.ProjectName!,
            Architecture: dto.Architecture ?? "Clean Architecture",
            Files: dto.Files!.Select(f => new CodeFile(f.Path!, f.Content ?? string.Empty, f.Language ?? "csharp")).ToArray(),
            Notes: dto.Notes,
            Metrics: metrics);
    }

    // ---- DTOs ----
    private sealed class CodeArtifactDto
    {
        public string? ProjectName { get; set; }
        public string? Architecture { get; set; }
        public List<FileDto>? Files { get; set; }
        public string? Notes { get; set; }

        public void Validate(string agentName)
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                throw new LlmException($"{agentName}: missing 'projectName'.", agentName);
            }
            if (Files is null || Files.Count == 0)
            {
                throw new LlmException($"{agentName}: 'files' must have ≥ 1 item.", agentName);
            }
        }
    }

    private sealed class FileDto
    {
        public string? Path { get; set; }
        public string? Content { get; set; }
        public string? Language { get; set; }
    }
}
