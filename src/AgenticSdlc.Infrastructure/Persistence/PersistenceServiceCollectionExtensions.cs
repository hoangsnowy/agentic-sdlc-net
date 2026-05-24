// DI cho persistence layer. Có connection string → EF Core + Postgres repos.
// Không có → null-object repos (app chạy stateless, hợp CI/local không DB).
using AgenticSdlc.Application.Persistence;
using AgenticSdlc.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticSdlc.Infrastructure.Persistence;

/// <summary>DI extension cho persistence (EF Core + Postgres).</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký DbContext + repository. Nếu <c>ConnectionStrings:DefaultConnection</c> trống
    /// → dùng no-op repos để app vẫn boot được không cần DB.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IPipelineRunRepository, NullPipelineRunRepository>();
            services.AddSingleton<IOrchestrationRepository, NullOrchestrationRepository>();
            return services;
        }

        services.AddDbContext<AgenticSdlcDbContext>(opt => opt.UseNpgsql(connectionString));
        services.AddScoped<IPipelineRunRepository, PipelineRunRepository>();
        services.AddScoped<IOrchestrationRepository, OrchestrationRepository>();
        return services;
    }

    /// <summary>
    /// Apply EF migration lúc startup (no-op nếu persistence chưa cấu hình DB).
    /// Gọi sau khi build app: <c>await app.Services.InitializePersistenceAsync();</c>
    /// </summary>
    public static async Task InitializePersistenceAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetService<AgenticSdlcDbContext>();
        if (db is not null)
        {
            await db.Database.MigrateAsync(ct);
        }
    }
}
