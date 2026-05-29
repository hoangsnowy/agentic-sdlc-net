// AgenticSdlc.Infrastructure/RemoteAgent/IRemoteExecApprover.cs
// Approval gate for the remote dev-IDE agent runtime. Because a remote agent executes work on a
// developer machine, every dispatched request passes through an approver before it is pushed down the
// wire. The default auto-approves; a UI-driven human-in-the-loop approver replaces it later (P0 security).

using System.Threading;
using System.Threading.Tasks;

namespace AgenticSdlc.Infrastructure.RemoteAgent;

/// <summary>Decides whether a <see cref="RemoteExecRequest"/> may be sent to a remote agent.</summary>
public interface IRemoteExecApprover
{
    /// <summary>Returns true to allow dispatch, false to deny (the caller fails the request).</summary>
    Task<bool> ApproveAsync(RemoteExecRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Default approver — allows everything. Replace with a human-in-the-loop gate for production.</summary>
public sealed class AutoApproveRemoteExec : IRemoteExecApprover
{
    /// <inheritdoc />
    public Task<bool> ApproveAsync(RemoteExecRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
