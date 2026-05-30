// Design-time factory so `dotnet ef migrations` can construct the context outside the host DI.
// Mirrors PipelineDbContextFactory: reads ConnectionStrings:DefaultConnection or falls back to a
// dummy DSN (migrations need only the provider + model shape, not a live DB).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AgentOs.Modules.Workspaces.Persistence;

public sealed class WorkspacesDbContextFactory : IDesignTimeDbContextFactory<WorkspacesDbContext>
{
    public WorkspacesDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=agentos;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", schema: "workspaces"))
            .Options;

        return new WorkspacesDbContext(options);
    }
}
