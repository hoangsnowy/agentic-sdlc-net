// AgenticSdlc.Infrastructure/Agents/QaAgent.cs
// Phase 4 — Impl IQaAgent. Đánh giá nhất quán requirement-code-test.

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
using AgenticSdlc.Domain.Testing;
using AgenticSdlc.Infrastructure.Llm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSdlc.Infrastructure.Agents;

/// <summary>QA — đánh giá nhất quán requirement-code-test. Drive QA loop của orchestrator.</summary>
public sealed class QaAgent : IQaAgent
{
    private const string AgentName = nameof(QaAgent);

    private const string SystemPrompt = """
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

    private readonly ILlmClient _llm;
    private readonly AgentOptions _options;
    private readonly ILogger<QaAgent> _logger;

    /// <summary>Khởi tạo.</summary>
    public QaAgent(ILlmClientFactory factory, IOptions<AgentsOptions> options, ILogger<QaAgent> logger)
    {
        System.ArgumentNullException.ThrowIfNull(factory);
        System.ArgumentNullException.ThrowIfNull(options);
        _options = options.Value.Qa;
        _llm = factory.Create(_options.Provider);
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<QaReport> RunAsync(
        RequirementSpec spec,
        CodeArtifact code,
        TestArtifact tests,
        CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(spec);
        System.ArgumentNullException.ThrowIfNull(code);
        System.ArgumentNullException.ThrowIfNull(tests);

        var sb = new StringBuilder();
        sb.AppendLine("Spec (entities + endpoints + acceptanceCriteria):");
        sb.AppendLine(JsonSerializer.Serialize(new { spec.Entities, spec.Endpoints, spec.AcceptanceCriteria }, JsonExtractor.DefaultOptions));
        sb.AppendLine();
        sb.AppendLine("Code (files, abbreviated):");
        sb.AppendLine(JsonSerializer.Serialize(code.Files.Select(f => new { f.Path, Excerpt = Excerpt(f.Content, 300) }), JsonExtractor.DefaultOptions));
        sb.AppendLine();
        sb.AppendLine("Tests (files + counts):");
        sb.AppendLine(JsonSerializer.Serialize(new
        {
            tests.Framework,
            tests.HappyPathCount,
            tests.EdgeCaseCount,
            tests.ErrorCaseCount,
            tests.EstimatedCoveragePercent,
            Files = tests.Files.Select(f => new { f.Path, Excerpt = Excerpt(f.Content, 300) }),
        }, JsonExtractor.DefaultOptions));

        sb.AppendLine();
        sb.AppendLine("Sinh QaReport JSON.");

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: sb.ToString(),
            Model: _options.Model,
            Temperature: _options.Temperature,
            MaxTokens: _options.MaxTokens);

        var response = await _llm.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var dto = JsonExtractor.Deserialize<QaReportDto>(response.Content, AgentName);
        dto.Validate(AgentName);

        var metrics = MetricsMapper.From(response);

        _logger.LogInformation(
            "{Agent} done: {InTok}→{OutTok} tokens, ${Cost} USD, {Ms}ms — score={Score} consistent={Consistent} issues={Issues}",
            AgentName, metrics.InputTokens, metrics.OutputTokens, metrics.CostUsd,
            metrics.Latency.TotalMilliseconds, dto.Score, dto.IsConsistent, dto.Issues?.Count ?? 0);

        return new QaReport(
            Score: dto.Score,
            IsConsistent: dto.IsConsistent,
            IterationNeeded: dto.IterationNeeded,
            Issues: (dto.Issues ?? []).Select(i => new QaIssue(i.Severity!, i.Category!, i.Description!, i.Location)).ToArray(),
            Recommendations: dto.Recommendations ?? [],
            Metrics: metrics);
    }

    private static string Excerpt(string content, int maxChars)
        => string.IsNullOrEmpty(content) ? string.Empty : content.Length <= maxChars ? content : string.Concat(content.AsSpan(0, maxChars), "...");

    // ---- DTOs ----
    private sealed class QaReportDto
    {
        public double Score { get; set; }
        public bool IsConsistent { get; set; }
        public bool IterationNeeded { get; set; }
        public List<QaIssueDto>? Issues { get; set; }
        public List<string>? Recommendations { get; set; }

        public void Validate(string agentName)
        {
            if (Score is < 0.0 or > 1.0)
            {
                throw new LlmException($"{agentName}: 'score' must be in [0, 1] (got {Score}).", agentName);
            }
        }
    }

    private sealed class QaIssueDto
    {
        public string? Severity { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }
    }
}
