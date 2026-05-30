// M1 — Tool gateway: the single server-side seam that gates (IToolPolicy), invokes (ITool) and
// records (IToolInvocationLog) one tool call. Extracted so EVERY execution path — the in-process
// LLM tool loop AND the remote dev-runner path (where the agentic loop runs server-side but the
// side effect happens off-box) — funnels through the same policy + evidence enforcement. The
// remote runner must never perform a side effect that did not pass through here on the server
// (the trust boundary).

namespace AgentOs.Domain.Tools;

/// <summary>Outcome of a gated tool invocation.</summary>
/// <param name="Output">Text fed back to the model: the tool output, the error message, or the policy-deny reason.</param>
/// <param name="IsError">True when the policy denied the call OR the tool returned an error.</param>
/// <param name="Denied">True only when a policy decision short-circuited the call before the tool ran.</param>
public sealed record ToolGatewayResult(string Output, bool IsError, bool Denied = false);

/// <summary>
/// Gates, invokes and records a single tool call. The one place policy + evidence are enforced,
/// so any caller (the LLM tool loop, the remote-session executor) gets identical governance.
/// </summary>
public interface IToolGateway
{
    /// <summary>Run <paramref name="request"/> against <paramref name="tool"/> through policy + evidence.</summary>
    System.Threading.Tasks.Task<ToolGatewayResult> InvokeAsync(
        ITool tool,
        ToolInvocationRequest request,
        System.Threading.CancellationToken cancellationToken = default);
}
