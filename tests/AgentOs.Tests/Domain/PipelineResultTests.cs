// AgentOs.Tests/Domain/PipelineResultTests.cs
// Phase 3 — IterationCount + Status enum + TestArtifact.TotalCount.

using System;
using System.Collections.Generic;
using AgentOs.Domain;
using AgentOs.Domain.Code;
using AgentOs.Domain.Pipeline;
using AgentOs.Domain.Qa;
using AgentOs.Domain.Requirements;
using AgentOs.Domain.Testing;
using Shouldly;
using Xunit;

namespace AgentOs.Tests.Domain;

public class PipelineResultTests
{
    [Fact]
    public void IterationCount_FromQaHistory()
    {
        var result = BuildResult(new[] { Qa(true), Qa(true) });
        result.IterationCount.ShouldBe(2);
    }

    [Fact]
    public void IterationCount_NoHistory_Zero()
    {
        var result = BuildResult(Array.Empty<QaReport>());
        result.IterationCount.ShouldBe(0);
    }

    [Fact]
    public void TestArtifact_TotalCount_SumsAllBuckets()
    {
        var t = new TestArtifact(
            Framework: "xUnit",
            Files: Array.Empty<CodeFile>(),
            HappyPathCount: 3,
            EdgeCaseCount: 2,
            ErrorCaseCount: 1,
            EstimatedCoveragePercent: 70,
            Metrics: AgentMetrics.Empty);
        t.TotalCount.ShouldBe(6);
    }

    [Theory]
    [InlineData(PipelineStatus.Done)]
    [InlineData(PipelineStatus.MaxIterationReached)]
    [InlineData(PipelineStatus.Failed)]
    public void Status_RoundTrip(PipelineStatus status)
    {
        var result = BuildResult(Array.Empty<QaReport>(), status);
        result.Status.ShouldBe(status);
    }

    private static PipelineResult BuildResult(IReadOnlyList<QaReport> history, PipelineStatus status = PipelineStatus.Done)
        => new(
            UserStory: new UserStory("story"),
            Spec: new RequirementSpec("t", "s", [], [], [], [], [], [], AgentMetrics.Empty),
            Code: new CodeArtifact("p", "Clean Architecture", [], null, AgentMetrics.Empty),
            Tests: new TestArtifact("xUnit", [], 0, 0, 0, 0, AgentMetrics.Empty),
            QaHistory: history,
            Status: status,
            TotalMetrics: AgentMetrics.Empty);

    private static QaReport Qa(bool consistent)
        => new(
            Score: consistent ? 0.9 : 0.5,
            IsConsistent: consistent,
            IterationNeeded: !consistent,
            Issues: [],
            Recommendations: [],
            Metrics: AgentMetrics.Empty);
}
