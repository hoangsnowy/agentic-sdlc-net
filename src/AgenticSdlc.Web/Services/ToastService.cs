// AgenticSdlc.Web/Services/ToastService.cs
// In-memory toast notification service. Components subscribe to Changed; pages call Show(...).

using System;
using System.Collections.Generic;

namespace AgenticSdlc.Web.Services;

/// <summary>Toast notification kinds — drives colour + icon.</summary>
public enum ToastKind { Info, Ok, Warn, Err }

/// <summary>A single toast notification.</summary>
public sealed record Toast(Guid Id, string Title, string Message, ToastKind Kind, DateTime CreatedAt)
{
    public static Toast Create(string title, string message, ToastKind kind = ToastKind.Info)
        => new(Guid.NewGuid(), title, message, kind, DateTime.UtcNow);
}

/// <summary>Singleton notification bus. The host (AppShellLayout) listens; any page can push.</summary>
public sealed class ToastService
{
    private readonly List<Toast> _items = new();

    /// <summary>Read-only current queue.</summary>
    public IReadOnlyList<Toast> Items => _items;

    /// <summary>Fires when the queue mutates so the host can re-render.</summary>
    public event Action? Changed;

    /// <summary>Push a new toast.</summary>
    public void Show(string title, string message, ToastKind kind = ToastKind.Info)
    {
        _items.Add(Toast.Create(title, message, kind));
        Changed?.Invoke();
    }

    /// <summary>Dismiss a toast by id.</summary>
    public void Dismiss(Guid id)
    {
        _items.RemoveAll(t => t.Id == id);
        Changed?.Invoke();
    }
}
