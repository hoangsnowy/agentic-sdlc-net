// AgenticSdlc.Infrastructure/Agents/DependencyInjection.cs
// Phase 4 — Đăng ký 5 agent + PipelineOrchestrator + AgentsOptions + PipelineOptions vào DI.

using AgenticSdlc.Application.Agents;
using AgenticSdlc.Application.Pipeline;
using AgenticSdlc.Infrastructure.Orchestration;
using AgenticSdlc.Infrastructure.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgenticSdlc.Infrastructure.Agents;

/// <summary>DI extension cho agents layer (gọi sau <c>AddLlmGateway</c>).</summary>
public static class AgentsServiceCollectionExtensions
{
    /// <summary>
    /// Register:
    /// <list type="bullet">
    ///   <item>Bind <see cref="AgentsOptions"/> + <see cref="PipelineOptions"/>.</item>
    ///   <item>4 specialist agent (Requirement / Coding / Testing / QA).</item>
    ///   <item><see cref="PipelineOrchestrator"/> implements <see cref="IOrchestratorAgent"/>.</item>
    ///   <item><see cref="IPipelineProgressSink"/> mặc định no-op (host realtime override sau).</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddAgents(this IServiceCollection services, IConfiguration configuration)
    {
        System.ArgumentNullException.ThrowIfNull(services);
        System.ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<AgentsOptions>()
            .Bind(configuration.GetSection(AgentsOptions.SectionName));
        services.AddOptions<PipelineOptions>()
            .Bind(configuration.GetSection(PipelineOptions.SectionName));

        services.AddTransient<IRequirementAgent, RequirementAgent>();
        services.AddTransient<ICodingAgent, CodingAgent>();
        services.AddTransient<ITestingAgent, TestingAgent>();
        services.AddTransient<IQaAgent, QaAgent>();
        services.AddTransient<IOrchestratorAgent, PipelineOrchestrator>();

        // Mặc định no-op — host cần realtime (Blazor) override bằng đăng ký scoped sau AddAgents.
        services.TryAddSingleton<IPipelineProgressSink>(NullPipelineProgressSink.Instance);

        return services;
    }
}
