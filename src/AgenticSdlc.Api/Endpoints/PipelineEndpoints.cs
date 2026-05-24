// AgenticSdlc.Api/Endpoints/PipelineEndpoints.cs
// Phase 4 — Minimal API endpoints cho 5 agent + pipeline.

using System.Threading;
using System.Threading.Tasks;
using AgenticSdlc.Application.Agents;
using AgenticSdlc.Application.Persistence;
using AgenticSdlc.Domain.Code;
using AgenticSdlc.Domain.Pipeline;
using AgenticSdlc.Domain.Qa;
using AgenticSdlc.Domain.Requirements;
using AgenticSdlc.Domain.Testing;

namespace AgenticSdlc.Api.Endpoints;

/// <summary>Mapping endpoint cho 4 specialist + pipeline (KC1-KC4).</summary>
public static class PipelineEndpoints
{
    /// <summary>Map endpoint vào <paramref name="app"/>.</summary>
    public static Microsoft.AspNetCore.Builder.WebApplication MapPipelineEndpoints(this Microsoft.AspNetCore.Builder.WebApplication app)
    {
        System.ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/requirement", async (UserStory body, IRequirementAgent agent, CancellationToken ct) =>
        {
            var spec = await agent.RunAsync(body, ct).ConfigureAwait(false);
            return Microsoft.AspNetCore.Http.Results.Ok(spec);
        })
        .WithName("Requirement")
        .WithSummary("KC1 — Phân tích user story → structured requirement spec")
        .WithTags("Agents");

        app.MapPost("/code", async (CodeRequest body, ICodingAgent agent, CancellationToken ct) =>
        {
            var artifact = await agent.RunAsync(body.Spec, body.PreviousFeedback, ct).ConfigureAwait(false);
            return Microsoft.AspNetCore.Http.Results.Ok(artifact);
        })
        .WithName("Code")
        .WithSummary("KC2 — Sinh source code C# Clean Architecture từ spec")
        .WithTags("Agents");

        app.MapPost("/test", async (TestRequest body, ITestingAgent agent, CancellationToken ct) =>
        {
            var artifact = await agent.RunAsync(body.Spec, body.Code, body.PreviousFeedback, ct).ConfigureAwait(false);
            return Microsoft.AspNetCore.Http.Results.Ok(artifact);
        })
        .WithName("Test")
        .WithSummary("KC3 — Sinh xUnit test (happy/edge/error)")
        .WithTags("Agents");

        app.MapPost("/qa", async (QaRequest body, IQaAgent agent, CancellationToken ct) =>
        {
            var report = await agent.RunAsync(body.Spec, body.Code, body.Tests, ct).ConfigureAwait(false);
            return Microsoft.AspNetCore.Http.Results.Ok(report);
        })
        .WithName("Qa")
        .WithSummary("KC5 — Đánh giá nhất quán requirement-code-test")
        .WithTags("Agents");

        app.MapPost("/pipeline", async (UserStory body, IOrchestratorAgent orchestrator, CancellationToken ct) =>
        {
            var result = await orchestrator.RunAsync(body, ct).ConfigureAwait(false);
            var statusCode = result.Status == PipelineStatus.Failed ? 500 : 200;
            return Microsoft.AspNetCore.Http.Results.Json(result, statusCode: statusCode);
        })
        .WithName("Pipeline")
        .WithSummary("KC4 — Pipeline end-to-end với QA loop (≤ NMax iteration)")
        .WithTags("Agents");

        app.MapGet("/runs", async (IPipelineRunRepository repo, CancellationToken ct) =>
        {
            var runs = await repo.ListAsync(50, ct).ConfigureAwait(false);
            return Microsoft.AspNetCore.Http.Results.Ok(runs);
        })
        .WithName("Runs")
        .WithSummary("Lịch sử pipeline run gần nhất (summary)")
        .WithTags("History");

        app.MapGet("/runs/{id:guid}", async (Guid id, IPipelineRunRepository repo, CancellationToken ct) =>
        {
            var run = await repo.GetAsync(id, ct).ConfigureAwait(false);
            return run is null
                ? Microsoft.AspNetCore.Http.Results.NotFound()
                : Microsoft.AspNetCore.Http.Results.Ok(run);
        })
        .WithName("RunById")
        .WithSummary("Chi tiết 1 pipeline run (full artifact + metrics)")
        .WithTags("History");

        return app;
    }
}

/// <summary>Body cho <c>POST /code</c>.</summary>
public sealed record CodeRequest(RequirementSpec Spec, QaReport? PreviousFeedback = null);

/// <summary>Body cho <c>POST /test</c>.</summary>
public sealed record TestRequest(RequirementSpec Spec, CodeArtifact Code, QaReport? PreviousFeedback = null);

/// <summary>Body cho <c>POST /qa</c>.</summary>
public sealed record QaRequest(RequirementSpec Spec, CodeArtifact Code, TestArtifact Tests);
