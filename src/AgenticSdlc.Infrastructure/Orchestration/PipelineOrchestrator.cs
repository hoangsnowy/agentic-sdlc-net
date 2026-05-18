// AgenticSdlc.Infrastructure/Orchestration/PipelineOrchestrator.cs
// Phase 4 — Implement IOrchestratorAgent. Run 4 specialist + QA loop tối đa NMax iteration.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticSdlc.Application.Agents;
using AgenticSdlc.Application.Pipeline;
using AgenticSdlc.Domain;
using AgenticSdlc.Domain.Code;
using AgenticSdlc.Domain.Llm;
using AgenticSdlc.Domain.Pipeline;
using AgenticSdlc.Domain.Qa;
using AgenticSdlc.Domain.Requirements;
using AgenticSdlc.Domain.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSdlc.Infrastructure.Orchestration;

/// <summary>
/// Điều phối luồng KC4 (Mục 2.4 luận văn):
/// <list type="number">
///   <item>RequirementAgent(story) → spec</item>
///   <item>Vòng lặp ≤ NMax: CodingAgent → TestingAgent → QaAgent → check IsConsistent</item>
///   <item>Trả PipelineResult với history QA + tổng metrics</item>
/// </list>
/// </summary>
public sealed class PipelineOrchestrator : IOrchestratorAgent
{
    private readonly IRequirementAgent _requirement;
    private readonly ICodingAgent _coding;
    private readonly ITestingAgent _testing;
    private readonly IQaAgent _qa;
    private readonly PipelineOptions _options;
    private readonly ILogger<PipelineOrchestrator> _logger;

    /// <summary>Khởi tạo.</summary>
    public PipelineOrchestrator(
        IRequirementAgent requirement,
        ICodingAgent coding,
        ITestingAgent testing,
        IQaAgent qa,
        IOptions<PipelineOptions> options,
        ILogger<PipelineOrchestrator> logger)
    {
        System.ArgumentNullException.ThrowIfNull(options);
        _requirement = requirement ?? throw new System.ArgumentNullException(nameof(requirement));
        _coding = coding ?? throw new System.ArgumentNullException(nameof(coding));
        _testing = testing ?? throw new System.ArgumentNullException(nameof(testing));
        _qa = qa ?? throw new System.ArgumentNullException(nameof(qa));
        _options = options.Value;
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PipelineResult> RunAsync(UserStory story, CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(story);
        story.Validate();

        var maxIterations = System.Math.Min(story.NMax, _options.MaxIterations);
        _logger.LogInformation(
            "Pipeline start: story={Title}, maxIter={Max}",
            Truncate(story.Description, 60), maxIterations);

        RequirementSpec spec;
        try
        {
            spec = await _requirement.RunAsync(story, cancellationToken).ConfigureAwait(false);
        }
        catch (LlmException ex)
        {
            _logger.LogError(ex, "RequirementAgent failed — pipeline abort.");
            return FailEarly(story, ex);
        }

        var qaHistory = new List<QaReport>(maxIterations);
        CodeArtifact? code = null;
        TestArtifact? tests = null;
        QaReport? lastQa = null;
        var status = PipelineStatus.MaxIterationReached;

        for (var iter = 1; iter <= maxIterations; iter++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Iteration {N}/{Max} start", iter, maxIterations);

            try
            {
                code = await _coding.RunAsync(spec, lastQa, cancellationToken).ConfigureAwait(false);
                tests = await _testing.RunAsync(spec, code, lastQa, cancellationToken).ConfigureAwait(false);
                lastQa = await _qa.RunAsync(spec, code, tests, cancellationToken).ConfigureAwait(false);
            }
            catch (LlmException ex)
            {
                _logger.LogError(ex, "Iteration {N} failed.", iter);
                return FailMidway(story, spec, code, tests, qaHistory, ex);
            }

            qaHistory.Add(lastQa);
            _logger.LogInformation(
                "Iteration {N} QA: score={Score} consistent={Consistent}",
                iter, lastQa.Score, lastQa.IsConsistent);

            if (lastQa.IsConsistent)
            {
                status = PipelineStatus.Done;
                break;
            }
        }

        var total = Aggregate(spec.Metrics, code?.Metrics, tests?.Metrics, qaHistory);

        return new PipelineResult(
            UserStory: story,
            Spec: spec,
            Code: code!,
            Tests: tests!,
            QaHistory: qaHistory,
            Status: status,
            TotalMetrics: total);
    }

    private static AgentMetrics Aggregate(AgentMetrics spec, AgentMetrics? code, AgentMetrics? tests, IReadOnlyList<QaReport> qaHistory)
    {
        var sum = spec;
        if (code is not null)
        {
            sum = sum.Add(code);
        }
        if (tests is not null)
        {
            sum = sum.Add(tests);
        }
        foreach (var q in qaHistory)
        {
            sum = sum.Add(q.Metrics);
        }
        return sum;
    }

    private static PipelineResult FailEarly(UserStory story, System.Exception ex)
        => new(
            UserStory: story,
            Spec: ErrorSpec(ex),
            Code: EmptyCode(),
            Tests: EmptyTests(),
            QaHistory: [],
            Status: PipelineStatus.Failed,
            TotalMetrics: AgentMetrics.Empty);

    private static PipelineResult FailMidway(
        UserStory story, RequirementSpec spec, CodeArtifact? code, TestArtifact? tests,
        IReadOnlyList<QaReport> history, System.Exception ex)
        => new(
            UserStory: story,
            Spec: spec,
            Code: code ?? EmptyCode(),
            Tests: tests ?? EmptyTests(),
            QaHistory: history,
            Status: PipelineStatus.Failed,
            TotalMetrics: AgentMetrics.Empty)
        {
            // Hint cho debug — exception in log đầy đủ.
        };

    private static RequirementSpec ErrorSpec(System.Exception ex)
        => new("(failed)", ex.Message, [], [], [], [], [], [], AgentMetrics.Empty);

    private static CodeArtifact EmptyCode()
        => new("(none)", "Clean Architecture", [], null, AgentMetrics.Empty);

    private static TestArtifact EmptyTests()
        => new("xUnit", [], 0, 0, 0, 0, AgentMetrics.Empty);

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "...");
}
