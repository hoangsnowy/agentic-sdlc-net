// M2 — resolves the right ISourceProvider for a SourceProviderKind from the DI-registered set.
// Mirrors how LlmClientFactory selects a provider by name; here we select by kind.

using System;
using System.Collections.Generic;
using AgentOs.Domain.Workspaces;

namespace AgentOs.Modules.Integration.Sources;

internal sealed class SourceProviderResolver : ISourceProviderResolver
{
    private readonly Dictionary<SourceProviderKind, ISourceProvider> _byKind = new();

    public SourceProviderResolver(IEnumerable<ISourceProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        foreach (var p in providers)
        {
            _byKind[p.Kind] = p; // last registration wins
        }
    }

    public ISourceProvider Resolve(SourceProviderKind kind)
        => _byKind.TryGetValue(kind, out var p)
            ? p
            : throw new InvalidOperationException($"No source provider registered for '{kind}'.");

    public bool TryResolve(SourceProviderKind kind, out ISourceProvider? provider)
        => _byKind.TryGetValue(kind, out provider);
}
