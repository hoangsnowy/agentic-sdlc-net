// AgentOs.Application/Agents/IQaAgent.cs
// Phase 3 — Contract for the QA Agent.

using System.Threading;
using System.Threading.Tasks;
using AgentOs.Domain.Code;
using AgentOs.Domain.Qa;
using AgentOs.Domain.Requirements;
using AgentOs.Domain.Testing;

namespace AgentOs.Modules.Pipeline.Agents;

/// <summary>
/// Evaluates requirement-code-test consistency. Returns a <see cref="QaReport"/> whose
/// <see cref="QaReport.IterationNeeded"/> flag drives the loop.
/// </summary>
public interface IQaAgent
{
    /// <summary>Runs the agent.</summary>
    Task<QaReport> RunAsync(
        RequirementSpec spec,
        CodeArtifact code,
        TestArtifact tests,
        CancellationToken cancellationToken = default);
}
