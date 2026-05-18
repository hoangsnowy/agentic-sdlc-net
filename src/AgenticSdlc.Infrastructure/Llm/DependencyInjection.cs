// AgenticSdlc.Infrastructure/Llm/DependencyInjection.cs
// Sprint 1 — Extension đăng ký LLM Gateway vào IServiceCollection.

using System;
using AgenticSdlc.Domain.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgenticSdlc.Infrastructure.Llm;

/// <summary>
/// DI extension cho LLM Gateway. Gọi 1 lần trong <c>Program.cs</c> của API project.
/// </summary>
public static class LlmGatewayServiceCollectionExtensions
{
    /// <summary>
    /// Register:
    /// <list type="bullet">
    ///   <item>Bind <see cref="LlmOptions"/> từ section <c>"Llm"</c>.</item>
    ///   <item><see cref="ClaudeClient"/>, <see cref="AzureOpenAiClient"/>, <see cref="MockLlmClient"/> (concrete).</item>
    ///   <item><see cref="ILlmClientFactory"/> → <see cref="LlmClientFactory"/>.</item>
    ///   <item><see cref="ILlmClient"/> → resolve qua factory tại runtime.</item>
    ///   <item>Named <c>HttpClient</c> cho mỗi provider qua <see cref="IHttpClientFactory"/>.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddLlmGateway(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LlmOptions>()
            .Bind(configuration.GetSection(LlmOptions.SectionName))
            .ValidateOnStart();

        // Named HttpClient — IHttpClientFactory để tránh socket exhaustion.
        services.AddHttpClient(ClaudeClient.HttpClientName, (sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<LlmOptions>>().Value.Claude;
            if (!string.IsNullOrWhiteSpace(opts.Endpoint))
            {
                http.BaseAddress = new Uri(opts.Endpoint.TrimEnd('/') + "/");
            }
            if (opts.TimeoutSeconds > 0)
            {
                http.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            }
        });

        services.AddHttpClient(AzureOpenAiClient.HttpClientName, (sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<LlmOptions>>().Value.AzureOpenAi;
            if (!string.IsNullOrWhiteSpace(opts.Endpoint))
            {
                http.BaseAddress = new Uri(opts.Endpoint.TrimEnd('/') + "/");
            }
            if (opts.TimeoutSeconds > 0)
            {
                http.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            }
        });

        // Concrete clients — singleton-ish, nhưng để Transient để mỗi resolve có HttpClient mới từ factory.
        services.AddTransient<ClaudeClient>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(ClaudeClient.HttpClientName);
            var opts = sp.GetRequiredService<IOptions<LlmOptions>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ClaudeClient>>();
            return new ClaudeClient(http, opts, logger);
        });

        services.AddTransient<AzureOpenAiClient>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(AzureOpenAiClient.HttpClientName);
            var opts = sp.GetRequiredService<IOptions<LlmOptions>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AzureOpenAiClient>>();
            return new AzureOpenAiClient(http, opts, logger);
        });

        services.AddTransient<MockLlmClient>();

        // Factory + default ILlmClient.
        services.AddSingleton<ILlmClientFactory, LlmClientFactory>();
        services.AddTransient<ILlmClient>(sp => sp.GetRequiredService<ILlmClientFactory>().CreateDefault());

        return services;
    }
}
