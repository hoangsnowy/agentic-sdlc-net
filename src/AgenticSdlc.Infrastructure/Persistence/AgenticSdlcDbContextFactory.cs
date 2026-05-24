// Design-time factory cho `dotnet ef migrations add`. KHÔNG dùng lúc runtime.
// Connection string lấy từ env (CI/local) hoặc default localhost cho việc sinh migration.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgenticSdlc.Infrastructure.Persistence;

internal sealed class AgenticSdlcDbContextFactory : IDesignTimeDbContextFactory<AgenticSdlcDbContext>
{
    public AgenticSdlcDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=agentic_sdlc;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AgenticSdlcDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AgenticSdlcDbContext(options);
    }
}
