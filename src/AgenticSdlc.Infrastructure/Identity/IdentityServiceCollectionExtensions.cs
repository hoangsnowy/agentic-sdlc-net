// AgenticSdlc.Infrastructure/Identity/IdentityServiceCollectionExtensions.cs
// DI registration for the Keycloak admin REST client + its options. Called from API composition
// root in keycloak mode so the tenant admin endpoints can provision realm users.

using AgenticSdlc.Application.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticSdlc.Infrastructure.Identity;

/// <summary>DI extensions for the Keycloak admin REST integration.</summary>
public static class IdentityServiceCollectionExtensions
{
    /// <summary>Bind <see cref="KeycloakAdminOptions"/> and register the typed HttpClient. Call
    /// from the API composition root when Auth:Mode=keycloak.</summary>
    public static IServiceCollection AddKeycloakAdmin(this IServiceCollection services, IConfiguration configuration)
    {
        System.ArgumentNullException.ThrowIfNull(services);
        System.ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<KeycloakAdminOptions>()
            .Bind(configuration.GetSection(KeycloakAdminOptions.SectionName));

        services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>();

        return services;
    }
}
