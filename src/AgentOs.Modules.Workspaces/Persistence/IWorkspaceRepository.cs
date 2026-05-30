// M2 — workspace persistence contract. Tenant scoping is enforced by the DbContext global query
// filter, so callers never pass a tenant id; writes stamp it from ITenantContext.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentOs.Modules.Workspaces.Persistence.Entities;

namespace AgentOs.Modules.Workspaces.Persistence;

/// <summary>CRUD for connected workspaces, scoped to the current tenant.</summary>
public interface IWorkspaceRepository
{
    Task<IReadOnlyList<WorkspaceEntity>> ListAsync(CancellationToken ct = default);
    Task<WorkspaceEntity?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(WorkspaceEntity workspace, CancellationToken ct = default);
    Task<bool> RemoveAsync(Guid id, CancellationToken ct = default);
}
