// Module entry: wires JWT bearer auth + the appropriate ITenantContext (DefaultTenantContext for
// operator mode, HttpTenantContext for Keycloak mode). Mounts /auth/token. No DbContext — auth
// state is per-request claims; no DB tables.

using System;
using AgentOs.Modules.Identity.Auth;
using AgentOs.SharedKernel.Identity;
using AgentOs.SharedKernel.Modularity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentOs.Modules.Identity;

public sealed class IdentityModule : IModule, IEndpointModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddJwtAuth(configuration);

        if (string.Equals(configuration["Auth:Mode"], "keycloak", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpContextAccessor();
            services.AddScoped<ITenantContext, HttpTenantContext>();
        }
        else
        {
            services.TryAddScoped<ITenantContext, DefaultTenantContext>();
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapAuthEndpoints();
    }
}
