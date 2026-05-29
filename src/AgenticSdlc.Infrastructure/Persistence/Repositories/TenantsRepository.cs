// EF Core impl of the tenant registry. Spans every tenant — no global filter, no tenant stamping
// (the row IS the tenant). Admin-policy guards live in the API endpoints, not here.

using AgenticSdlc.Application.Identity;
using AgenticSdlc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgenticSdlc.Infrastructure.Persistence.Repositories;

internal sealed class TenantsRepository(AgenticSdlcDbContext db) : ITenantsRepository
{
    public async Task<IReadOnlyList<TenantRecord>> ListAsync(CancellationToken ct = default)
    {
        return await db.Tenants.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new TenantRecord(x.Id, x.Name, x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<TenantRecord?> GetAsync(string id, CancellationToken ct = default)
    {
        var e = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? null : new TenantRecord(e.Id, e.Name, e.CreatedAtUtc);
    }

    public async Task AddAsync(TenantRecord tenant, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        db.Tenants.Add(new TenantEntity
        {
            Id = tenant.Id,
            Name = tenant.Name,
            CreatedAtUtc = tenant.CreatedAtUtc,
        });
        await db.SaveChangesAsync(ct);
    }
}

internal sealed class NullTenantsRepository : ITenantsRepository
{
    public Task<IReadOnlyList<TenantRecord>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TenantRecord>>([]);

    public Task<TenantRecord?> GetAsync(string id, CancellationToken ct = default) =>
        Task.FromResult<TenantRecord?>(null);

    public Task AddAsync(TenantRecord tenant, CancellationToken ct = default) => Task.CompletedTask;
}
