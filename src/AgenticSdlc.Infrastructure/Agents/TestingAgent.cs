// AgenticSdlc.Infrastructure/Agents/TestingAgent.cs
// Phase 4 — Impl ITestingAgent. Sinh xUnit test (happy/edge/error) từ spec + code.

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

/// <summary>Sinh bộ test xUnit phân loại happy/edge/error.</summary>
public sealed class TestingAgent : ITestingAgent
{
    private const string AgentName = nameof(TestingAgent);

    private const string SystemPrompt = """
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

    private readonly ILlmClient _llm;
    private readonly AgentOptions _options;
    private readonly ILogger<TestingAgent> _logger;

    /// <summary>Khởi tạo.</summary>
    public TestingAgent(ILlmClientFactory factory, IOptions<AgentsOptions> options, ILogger<TestingAgent> logger)
    {
        System.ArgumentNullException.ThrowIfNull(factory);
        System.ArgumentNullException.ThrowIfNull(options);
        _options = options.Value.Testing;
        _llm = factory.Create(_options.Provider);
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TestArtifact> RunAsync(
        RequirementSpec spec,
        CodeArtifact code,
        QaReport? previousFeedback = null,
        CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(spec);
        System.ArgumentNullException.ThrowIfNull(code);

        var sb = new StringBuilder();
        sb.AppendLine("Specification (acceptance criteria):");
        sb.AppendLine(JsonSerializer.Serialize(spec.AcceptanceCriteria, JsonExtractor.DefaultOptions));
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
            sb.AppendLine(JsonSerializer.Serialize(previousFeedback.Issues, JsonExtractor.DefaultOptions));
        }

        sb.AppendLine();
        sb.AppendLine("Sinh TestArtifact JSON.");

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: sb.ToString(),
            Model: _options.Model,
            Temperature: _options.Temperature,
            MaxTokens: _options.MaxTokens);

        var response = await _llm.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var dto = JsonExtractor.Deserialize<TestArtifactDto>(response.Content, AgentName);
        dto.Validate(AgentName);

        var metrics = MetricsMapper.From(response);

        _logger.LogInformation(
            "{Agent} done: {InTok}→{OutTok} tokens, ${Cost} USD, {Ms}ms — {Total} tests ({Happy}H/{Edge}E/{Err}X)",
            AgentName, metrics.InputTokens, metrics.OutputTokens, metrics.CostUsd,
            metrics.Latency.TotalMilliseconds, dto.HappyPathCount + dto.EdgeCaseCount + dto.ErrorCaseCount,
            dto.HappyPathCount, dto.EdgeCaseCount, dto.ErrorCaseCount);

        return new TestArtifact(
            Framework: dto.Framework ?? "xUnit",
            Files: dto.Files!.Select(f => new CodeFile(f.Path!, f.Content ?? string.Empty, f.Language ?? "csharp")).ToArray(),
            HappyPathCount: dto.HappyPathCount,
            EdgeCaseCount: dto.EdgeCaseCount,
            ErrorCaseCount: dto.ErrorCaseCount,
            EstimatedCoveragePercent: dto.EstimatedCoveragePercent,
            Metrics: metrics);
    }

    // ---- DTOs ----
    private sealed class TestArtifactDto
    {
        public string? Framework { get; set; }
        public List<FileDto>? Files { get; set; }
        public int HappyPathCount { get; set; }
        public int EdgeCaseCount { get; set; }
        public int ErrorCaseCount { get; set; }
        public int EstimatedCoveragePercent { get; set; }

        public void Validate(string agentName)
        {
            if (Files is null || Files.Count == 0)
            {
                throw new LlmException($"{agentName}: 'files' must have ≥ 1 item.", agentName);
            }
            if (HappyPathCount + EdgeCaseCount + ErrorCaseCount <= 0)
            {
                throw new LlmException($"{agentName}: total test count must be > 0.", agentName);
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
