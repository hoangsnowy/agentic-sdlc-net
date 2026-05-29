// AgenticSdlc.Web/Services/AppCatalog.cs
// Single source of truth for the desktop's launchable apps. The Start menu, dock and
// desktop icons all read from here so a new app is registered in exactly one place.

using System.Collections.Generic;
using System.Linq;

namespace AgenticSdlc.Web.Services;

/// <summary>A launchable desktop application.</summary>
/// <param name="Key">Stable id used by <see cref="WindowManagerService"/> and recents.</param>
/// <param name="Title">Window + launcher title.</param>
/// <param name="Icon">Icon name (see <c>Icon.razor</c> map).</param>
/// <param name="Caption">One-line description shown in the launcher.</param>
/// <param name="Category">Grouping bucket in the Start menu.</param>
/// <param name="W">Default window width.</param>
/// <param name="H">Default window height.</param>
/// <param name="Pinned">Whether the app appears in the dock + pinned grid.</param>
public sealed record DesktopApp(
    string Key,
    string Title,
    string Icon,
    string Caption,
    string Category,
    int W = 920,
    int H = 620,
    bool Pinned = true);

/// <summary>The fixed catalog of apps the AgentOS shell can launch.</summary>
public static class AppCatalog
{
    /// <summary>All registered apps, in display order.</summary>
    public static IReadOnlyList<DesktopApp> All { get; } = new List<DesktopApp>
    {
        new("pipeline", "Pipeline", "play",  "Run the 5-agent SDLC pipeline", "Agents", 920, 620),
        new("workflow", "Workflow", "graph", "Visual orchestration editor",   "Agents", 1080, 660),
        new("settings", "Settings", "gear",  "LLM keys, providers, GitHub",   "System", 760, 600),
        new("system",   "System",   "wrench","OS appearance, themes, about",  "System", 760, 600),
    };

    /// <summary>Apps that appear in the dock and the pinned grid.</summary>
    public static IReadOnlyList<DesktopApp> Pinned { get; } =
        All.Where(a => a.Pinned).ToList();

    /// <summary>Resolve an app by key, or <c>null</c> if unknown.</summary>
    public static DesktopApp? Find(string key) =>
        All.FirstOrDefault(a => a.Key == key);
}
