// AgentOs.Application/Agents/ITestingAgent.cs
// Phase 3 — Contract for the Testing Agent.

using System.Threading;
using System.Threading.Tasks;
using AgentOs.Domain.Code;
using AgentOs.Domain.Qa;
using AgentOs.Domain.Requirements;
using AgentOs.Domain.Testing;

namespace AgentOs.Modules.Pipeline.Agents;

/// <summary>
/// Generates xUnit tests (happy / edge / error) from a <see cref="RequirementSpec"/> + <see cref="CodeArtifact"/>.
/// </summary>
public interface ITestingAgent
{
    /// <summary>Runs the agent.</summary>
    Task<TestArtifact> RunAsync(
        RequirementSpec spec,
        CodeArtifact code,
        QaReport? previousFeedback = null,
        CancellationToken cancellationToken = default);
}
