// M2 — a connected source repository (schema workspaces.workspaces). Stores only a CredentialRef
// (a key into the encrypted AppConfig store), never the access token itself. Tenant-stamped on
// write; reads are tenant-filtered by the DbContext global query filter.

using System;
using AgentOs.Domain.Workspaces;

namespace AgentOs.Modules.Workspaces.Persistence.Entities;

/// <summary>A persisted workspace = a connected repo + how to find its credentials.</summary>
public sealed class WorkspaceEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SourceProviderKind Kind { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string? Project { get; set; }
    public string DefaultBranch { get; set; } = "main";
    public string RemoteUrl { get; set; } = string.Empty;

    /// <summary>Key into the encrypted AppConfig store where the access token lives. Never the secret itself.</summary>
    public string CredentialRef { get; set; } = string.Empty;

    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string Status { get; set; } = "Connected";
}
