// M2 — a repository as listed by a provider during the "connect a workspace" flow, before the user
// picks one. Provider-neutral projection of GitHub/Azure DevOps repo metadata.

namespace AgentOs.Domain.Workspaces;

/// <summary>A repository surfaced by <see cref="ISourceProvider.ListRepositoriesAsync"/>.</summary>
/// <param name="Owner">Owner / org (GitHub) or organization (Azure DevOps).</param>
/// <param name="Name">Repository name.</param>
/// <param name="FullName">Display path, e.g. <c>owner/name</c> or <c>org/project/name</c>.</param>
/// <param name="DefaultBranch">Default branch.</param>
/// <param name="RemoteUrl">Clone / web URL.</param>
/// <param name="Private">Whether the repo is private.</param>
/// <param name="Project">Azure DevOps project, when applicable; null for GitHub.</param>
public sealed record RemoteRepo(
    string Owner,
    string Name,
    string FullName,
    string DefaultBranch,
    string RemoteUrl,
    bool Private,
    string? Project = null);
