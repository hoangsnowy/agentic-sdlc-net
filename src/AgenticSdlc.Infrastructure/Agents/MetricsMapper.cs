// AgenticSdlc.Infrastructure/Agents/MetricsMapper.cs
// Phase 4 — Map LlmResponse → AgentMetrics.

using AgenticSdlc.Domain;
using AgenticSdlc.Domain.Llm;

namespace AgenticSdlc.Infrastructure.Agents;

/// <summary>Helper map LlmResponse sang AgentMetrics (1 dòng, tránh trùng lặp ở mọi agent).</summary>
internal static class MetricsMapper
{
    public static AgentMetrics From(LlmResponse response)
    {
        System.ArgumentNullException.ThrowIfNull(response);
        return new AgentMetrics(
            Provider: response.Provider,
            Model: response.Model,
            InputTokens: response.InputTokens,
            OutputTokens: response.OutputTokens,
            CostUsd: response.CostUsd,
            Latency: response.Latency);
    }
}
