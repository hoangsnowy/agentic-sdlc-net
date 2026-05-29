// AgenticSdlc.Infrastructure/Llm/RemoteAgentLlmClient.cs
// "Remote dev-IDE agent" provider — instead of calling an LLM API (and spending tokens), this client
// dispatches the request to a connected remote agent (running in/near the dev's IDE) via IRemoteAgentBroker
// and wraps its reply as an LlmResponse with zero cost. Select it with Provider="RemoteAgent".

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AgenticSdlc.Domain.Llm;
using AgenticSdlc.Infrastructure.RemoteAgent;
using Microsoft.Extensions.Logging;

namespace AgenticSdlc.Infrastructure.Llm;

/// <summary>
/// <see cref="ILlmClient"/> that routes a request to a connected remote agent rather than an LLM API.
/// Returns zero token usage / cost — the work runs on the dev's machine and its own quota.
/// </summary>
public sealed class RemoteAgentLlmClient : ILlmClient
{
    /// <summary>How long to wait for the remote agent to reply before giving up.</summary>
    public static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(120);

    private readonly IRemoteAgentBroker _broker;
    private readonly ILogger<RemoteAgentLlmClient> _logger;

    /// <inheritdoc />
    public string Provider => "RemoteAgent";

    /// <summary>Initializes the client over the dispatch <paramref name="broker"/>.</summary>
    public RemoteAgentLlmClient(IRemoteAgentBroker broker, ILogger<RemoteAgentLlmClient> logger)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        if (!_broker.HasAgent)
        {
            throw new LlmException(
                "No remote dev agent connected. Start the AgentSdlc remote agent on the dev machine and pair it, "
                + "or pick a different provider.",
                Provider);
        }

        var id = Guid.NewGuid().ToString("N");
        var execRequest = new RemoteExecRequest(id, request.SystemPrompt, request.UserPrompt, request.Model);

        var stopwatch = Stopwatch.StartNew();
        RemoteExecResult result;
        try
        {
            result = await _broker.DispatchAsync(execRequest, DispatchTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            throw new LlmException($"Remote agent timed out after {DispatchTimeout.TotalSeconds:0}s.", Provider, innerException: ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new LlmException(ex.Message, Provider, innerException: ex);
        }
        stopwatch.Stop();

        if (!result.Ok)
        {
            throw new LlmException(result.Error ?? "Remote agent reported a failure.", Provider);
        }

        _logger.LogInformation("[RemoteAgent] request {Id} handled by a remote agent ({Count} connected); 0 API tokens spent.",
            id, _broker.AgentCount);

        return new LlmResponse(
            Content: result.Content,
            InputTokens: 0,
            OutputTokens: 0,
            CostUsd: 0m,
            Latency: stopwatch.Elapsed,
            Model: request.Model,
            Provider: Provider);
    }
}
