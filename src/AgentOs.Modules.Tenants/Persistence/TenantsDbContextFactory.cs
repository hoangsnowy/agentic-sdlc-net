// Design-time factory for `dotnet ef migrations add` on TenantsDbContext.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgentOs.Modules.Tenants.Persistence;

internal sealed class TenantsDbContextFactory : IDesignTimeDbContextFactory<TenantsDbContext>
{
    public TenantsDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=agentos;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<TenantsDbContext>()
            .UseNpgsql(connectionString, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", schema: "tenants"))
            .Options;

        return new TenantsDbContext(options);
    }
}
