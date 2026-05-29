// AgentOs.Application/Agents/ICodingAgent.cs
// Phase 3 — Contract for the Coding Agent.

using System.Threading;
using System.Threading.Tasks;
using AgentOs.Domain.Code;
using AgentOs.Domain.Qa;
using AgentOs.Domain.Requirements;

namespace AgentOs.Modules.Pipeline.Agents;

/// <summary>
/// Generates C# source code (Clean Architecture) from a <see cref="RequirementSpec"/>.
/// May take <see cref="QaReport"/> feedback from the previous iteration to regenerate.
/// </summary>
public interface ICodingAgent
{
    /// <summary>Runs the agent.</summary>
    /// <param name="spec">The requirement spec.</param>
    /// <param name="previousFeedback">QA report from the previous iteration — null on the first iteration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    System.Threading.Tasks.Task<CodeArtifact> RunAsync(
        RequirementSpec spec,
        QaReport? previousFeedback = null,
        CancellationToken cancellationToken = default);
}
