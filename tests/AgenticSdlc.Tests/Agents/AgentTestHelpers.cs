// AgenticSdlc.Tests/Agents/AgentTestHelpers.cs
// Phase 4 — Helper build LlmResponse stub + AgentsOptions với 1 agent.

using System;
using AgenticSdlc.Application.Agents;
using AgenticSdlc.Domain.Llm;
using AgenticSdlc.Infrastructure.Llm;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AgenticSdlc.Tests.Agents;

internal static class AgentTestHelpers
{
    public static LlmResponse StubResponse(string content)
        => new(
            Content: content,
            InputTokens: 100,
            OutputTokens: 50,
            CostUsd: 0.0001m,
            Latency: TimeSpan.FromMilliseconds(123),
            Model: "mock-model",
            Provider: "Mock");

    public static IOptions<AgentsOptions> OptionsWith(AgentsOptions opts) => Options.Create(opts);

    public static ILlmClientFactory FactoryReturning(ILlmClient client)
    {
        var factory = Substitute.For<ILlmClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(client);
        return factory;
    }
}
