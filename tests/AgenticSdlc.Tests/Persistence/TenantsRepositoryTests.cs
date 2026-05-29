// Smoke tests for the tenant registry — the table has no global filter, so admins see every
// tenant across the realm.

using AgenticSdlc.Application.Identity;
using AgenticSdlc.Infrastructure.Identity;
using AgenticSdlc.Infrastructure.Persistence;
using AgenticSdlc.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AgenticSdlc.Tests.Persistence;

public sealed class TenantsRepositoryTests
{
    private static DbContextOptions<AgenticSdlcDbContext> NewOptions() =>
        new DbContextOptionsBuilder<AgenticSdlcDbContext>()
            .UseInMemoryDatabase($"tenants-{Guid.NewGuid()}")
            .Options;

    private static readonly DefaultTenantContext Tenant = new();
    private static readonly string[] ExpectedIds = ["acme", "globex"];

    private static AgenticSdlcDbContext NewDb(DbContextOptions<AgenticSdlcDbContext> options) =>
        new(options, Tenant);

    [Fact]
    public async Task AddAsync_Then_ListAsync_ReturnsRegisteredTenants()
    {
        var options = NewOptions();
        await using (var db = NewDb(options))
        {
            var repo = new TenantsRepository(db);
            await repo.AddAsync(new TenantRecord("acme", "Acme Corp", DateTimeOffset.UtcNow));
            await repo.AddAsync(new TenantRecord("globex", "Globex Inc", DateTimeOffset.UtcNow));
        }

        await using (var db = NewDb(options))
        {
            var list = await new TenantsRepository(db).ListAsync();
            list.Count.ShouldBe(2);
            list.Select(t => t.Id).ToList().ShouldBe(ExpectedIds);
        }
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        await using var db = NewDb(NewOptions());
        var got = await new TenantsRepository(db).GetAsync("nope");
        got.ShouldBeNull();
    }
}
