// M2 — Workspaces persistence (schema workspaces). One aggregate: Workspace. Tenant-scoped via a
// global query filter on TenantId, mirrored from the request's ITenantContext (same pattern as
// PipelineDbContext / AppConfigDbContext).

using System;
using AgentOs.Modules.Workspaces.Persistence.Entities;
using AgentOs.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace AgentOs.Modules.Workspaces.Persistence;

/// <summary>EF Core context for workspace persistence (schema <c>workspaces</c>).</summary>
public sealed class WorkspacesDbContext : DbContext
{
    private readonly ITenantContext? _tenant;

    public WorkspacesDbContext(DbContextOptions<WorkspacesDbContext> options, ITenantContext? tenant = null)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<WorkspaceEntity> Workspaces => Set<WorkspaceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("workspaces");

        var tenantId = _tenant?.TenantId ?? string.Empty;

        modelBuilder.Entity<WorkspaceEntity>(e =>
        {
            e.ToTable("workspaces");
            e.HasKey(x => x.Id);
            e.Property(x => x.TenantId).IsRequired().HasMaxLength(64);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Kind).IsRequired();
            e.Property(x => x.Owner).IsRequired().HasMaxLength(256);
            e.Property(x => x.Repo).IsRequired().HasMaxLength(256);
            e.Property(x => x.Project).HasMaxLength(256);
            e.Property(x => x.DefaultBranch).IsRequired().HasMaxLength(256);
            e.Property(x => x.RemoteUrl).IsRequired().HasMaxLength(2048);
            e.Property(x => x.CredentialRef).IsRequired().HasMaxLength(256);
            e.Property(x => x.Status).IsRequired().HasMaxLength(32);
            e.Property(x => x.CreatedAtUtc).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });
            e.HasQueryFilter(x => x.TenantId == tenantId);
        });
    }
}
