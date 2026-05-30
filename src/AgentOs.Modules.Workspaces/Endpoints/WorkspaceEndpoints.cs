// M2 — Workspaces HTTP endpoints. Connect a GitHub/Azure DevOps repo as a workspace, list/get/
// remove them, list connectable repos for a token, and read a repo's context to ground the
// Requirement agent. Auth: Member policy; tenant resolved from the token by ITenantContext.
//
// Secret handling: the access token is validated, then stored ONLY in the encrypted AppConfig store
// under the workspace's CredentialRef. It is never written to the workspace row and never returned
// in any response DTO. It is rehydrated just-in-time when a provider call needs it.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentOs.Domain.Workspaces;
using AgentOs.Modules.AppConfig;
using AgentOs.Modules.Workspaces.Persistence;
using AgentOs.Modules.Workspaces.Persistence.Entities;
using AgentOs.SharedKernel.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgentOs.Modules.Workspaces.Endpoints;

internal static class WorkspaceEndpoints
{
    public static void MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/workspaces").RequireAuthorization("Member");

        group.MapGet(string.Empty, ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost(string.Empty, ConnectAsync);
        group.MapDelete("/{id:guid}", RemoveAsync);
        group.MapPost("/repos", ListReposAsync);
        group.MapGet("/{id:guid}/context", ContextAsync);
    }

    private static async Task<IResult> ListAsync(IWorkspaceRepository repo, CancellationToken ct)
    {
        var rows = await repo.ListAsync(ct).ConfigureAwait(false);
        return Results.Ok(rows.Select(WorkspaceDto.From).ToList());
    }

    private static async Task<IResult> GetAsync(Guid id, IWorkspaceRepository repo, CancellationToken ct)
    {
        var row = await repo.GetAsync(id, ct).ConfigureAwait(false);
        return row is null ? Results.NotFound() : Results.Ok(WorkspaceDto.From(row));
    }

    private static async Task<IResult> ConnectAsync(
        ConnectWorkspaceRequest request,
        IWorkspaceRepository repo,
        ISourceProviderResolver providers,
        IAppConfigStore credentials,
        ITenantContext tenant,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.AccessToken))
        {
            return Results.BadRequest("Name and accessToken are required.");
        }
        if (!providers.TryResolve(request.Kind, out var provider) || provider is null)
        {
            return Results.BadRequest($"No source provider registered for '{request.Kind}'.");
        }

        var id = Guid.NewGuid();
        var requestedBranch = string.IsNullOrWhiteSpace(request.DefaultBranch) ? "main" : request.DefaultBranch!;
        var descriptor = new WorkspaceDescriptor(
            id, tenant.TenantId, request.Kind, request.Owner, request.Repo,
            request.Project, requestedBranch, request.AccessToken, request.Host);

        try
        {
            descriptor.Validate();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        var validation = await provider.ValidateAsync(descriptor, ct).ConfigureAwait(false);
        if (!validation.Ok)
        {
            return Results.BadRequest(validation.Error ?? "Could not reach the repository with the supplied credentials.");
        }

        // Store the secret encrypted, keyed by the workspace; the row keeps only the reference.
        var credentialRef = CredentialKey(id);
        await credentials.SetAsync(credentialRef, request.AccessToken, ct).ConfigureAwait(false);

        var entity = new WorkspaceEntity
        {
            Id = id,
            TenantId = tenant.TenantId,
            Name = request.Name,
            Kind = request.Kind,
            Owner = request.Owner,
            Repo = request.Repo,
            Project = request.Project,
            DefaultBranch = validation.DefaultBranch ?? requestedBranch,
            RemoteUrl = BuildRemoteUrl(request.Kind, request.Owner, request.Project, request.Repo, request.Host),
            CredentialRef = credentialRef,
            CreatedByUserId = tenant.UserId,
            CreatedAtUtc = clock.GetUtcNow(),
            Status = "Connected",
        };
        await repo.AddAsync(entity, ct).ConfigureAwait(false);

        return Results.Created($"/workspaces/{id}", WorkspaceDto.From(entity));
    }

    private static async Task<IResult> RemoveAsync(
        Guid id, IWorkspaceRepository repo, IAppConfigStore credentials, CancellationToken ct)
    {
        var row = await repo.GetAsync(id, ct).ConfigureAwait(false);
        if (row is null)
        {
            return Results.NotFound();
        }
        var removed = await repo.RemoveAsync(id, ct).ConfigureAwait(false);
        if (removed)
        {
            await credentials.DeleteAsync(row.CredentialRef, ct).ConfigureAwait(false);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> ListReposAsync(
        ListReposRequest request,
        ISourceProviderResolver providers,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.AccessToken))
        {
            return Results.BadRequest("accessToken is required.");
        }
        if (!providers.TryResolve(request.Kind, out var provider) || provider is null)
        {
            return Results.BadRequest($"No source provider registered for '{request.Kind}'.");
        }

        var creds = new ConnectionCredentials(request.Kind, request.AccessToken, request.Owner, request.Host);
        var repos = await provider.ListRepositoriesAsync(creds, ct).ConfigureAwait(false);
        return Results.Ok(repos);
    }

    private static async Task<IResult> ContextAsync(
        Guid id,
        IWorkspaceRepository repo,
        ISourceProviderResolver providers,
        IAppConfigStore credentials,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var row = await repo.GetAsync(id, ct).ConfigureAwait(false);
        if (row is null)
        {
            return Results.NotFound();
        }
        if (!providers.TryResolve(row.Kind, out var provider) || provider is null)
        {
            return Results.BadRequest($"No source provider registered for '{row.Kind}'.");
        }

        var token = await credentials.GetAsync(row.CredentialRef, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
        {
            return Results.BadRequest("Stored credentials for this workspace are missing; reconnect it.");
        }

        var descriptor = new WorkspaceDescriptor(
            row.Id, tenant.TenantId, row.Kind, row.Owner, row.Repo,
            row.Project, row.DefaultBranch, token, null);
        var context = await provider.ReadRepoContextAsync(descriptor, ct).ConfigureAwait(false);
        return Results.Ok(context);
    }

    private static string CredentialKey(Guid id) =>
        string.Create(CultureInfo.InvariantCulture, $"workspace/{id:N}/token");

    private static string BuildRemoteUrl(SourceProviderKind kind, string owner, string? project, string repo, string? host)
    {
        var baseHost = string.IsNullOrWhiteSpace(host)
            ? (kind == SourceProviderKind.AzureDevOps ? "https://dev.azure.com" : "https://github.com")
            : host!.TrimEnd('/');

        return kind == SourceProviderKind.AzureDevOps
            ? string.Create(CultureInfo.InvariantCulture, $"{baseHost}/{owner}/{project}/_git/{repo}")
            : string.Create(CultureInfo.InvariantCulture, $"{baseHost}/{owner}/{repo}");
    }
}

/// <summary>Connect-a-workspace request body.</summary>
internal sealed record ConnectWorkspaceRequest(
    string Name,
    SourceProviderKind Kind,
    string Owner,
    string Repo,
    string? Project,
    string? DefaultBranch,
    string? Host,
    string AccessToken);

/// <summary>List-connectable-repos request body (a token probe, no repo chosen yet).</summary>
internal sealed record ListReposRequest(
    SourceProviderKind Kind,
    string AccessToken,
    string? Owner,
    string? Host);

/// <summary>Workspace projection returned to clients — never carries the access token or CredentialRef.</summary>
internal sealed record WorkspaceDto(
    Guid Id,
    string Name,
    SourceProviderKind Kind,
    string Owner,
    string Repo,
    string? Project,
    string DefaultBranch,
    string RemoteUrl,
    string Status,
    DateTimeOffset CreatedAtUtc)
{
    public static WorkspaceDto From(WorkspaceEntity e) => new(
        e.Id, e.Name, e.Kind, e.Owner, e.Repo, e.Project, e.DefaultBranch, e.RemoteUrl, e.Status, e.CreatedAtUtc);
}
