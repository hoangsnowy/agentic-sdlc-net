// AgenticSdlc.Api/RemoteAgent/RemoteAgentHub.cs
// SignalR transport for the remote dev-IDE agent runtime. A dev-side agent connects here (with a pairing
// token), receives "Execute" pushes for codegen requests, and calls CompleteRequest with the result.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AgenticSdlc.Infrastructure.RemoteAgent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;

namespace AgenticSdlc.Api.RemoteAgent;

/// <summary>Hub dev agents connect to. Server → agent: <c>Execute(RemoteExecRequest)</c>;
/// agent → server: <see cref="CompleteRequest"/>.</summary>
public sealed class RemoteAgentHub : Hub
{
    /// <summary>Route the hub is mapped at.</summary>
    public const string Path = "/hubs/remote-agent";

    // ConnectionId -> broker registration handle (disposed on disconnect to mark the agent gone).
    private static readonly ConcurrentDictionary<string, IDisposable> Registrations = new(StringComparer.Ordinal);

    private readonly IRemoteAgentBroker _broker;
    private readonly IConfiguration _config;

    /// <summary>Initializes the hub.</summary>
    public RemoteAgentHub(IRemoteAgentBroker broker, IConfiguration config)
    {
        _broker = broker;
        _config = config;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        // Pairing: a matching RemoteAgent:PairingToken is required. No token configured = feature off
        // (every connection rejected), so the runtime is opt-in and never open by default.
        var expected = _config["RemoteAgent:PairingToken"];
        var presented = Context.GetHttpContext()?.Request.Query["token"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || !string.Equals(presented, expected, StringComparison.Ordinal))
        {
            Context.Abort();
            return;
        }

        Registrations[Context.ConnectionId] = _broker.RegisterAgent(Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Registrations.TryRemove(Context.ConnectionId, out var registration))
        {
            registration.Dispose();
        }
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Invoked by the agent when it has a result for a dispatched request.</summary>
    public void CompleteRequest(RemoteExecResult result) => _broker.Complete(result);
}
