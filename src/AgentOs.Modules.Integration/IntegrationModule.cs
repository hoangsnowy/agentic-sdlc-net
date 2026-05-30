// Module entry: registers IGitHubPrService + IBuildVerifier. Both consumed by Pipeline orchestrator
// when the run generates code that needs to land in a PR or be build-verified locally first.

using System;
using AgentOs.Modules.Integration.Tools;
using AgentOs.Modules.Tools;
using AgentOs.SharedKernel.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentOs.Modules.Integration;

public sealed class IntegrationModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddGitHubIntegration();
        services.AddBuildVerifier();
        // Epic E2 — wrap IBuildVerifier as an ITool so agents can call it via FunctionInvokingChatClient.
        // ToolsModule.InitializeAsync pumps every ITool DI registration into the IToolRegistry at startup.
        services.AddTool<BuildVerifierTool>();
    }
}
