// AgenticSdlc.Web/Orchestrations/StepNodeModel.cs
// Phase 7b — NodeModel tuỳ biến cho Z.Blazor.Diagrams: bọc GraphNode + trạng thái runtime.
// Mỗi node có 4 port (Left/Right/Top/Bottom) để nối edge tự do.

using System;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;

namespace AgenticSdlc.Web.Orchestrations;

/// <summary>Node trên canvas — bind tới <see cref="GraphNode"/> dữ liệu + trạng thái chạy.</summary>
public sealed class StepNodeModel : NodeModel
{
    /// <summary>Khởi tạo từ dữ liệu node; thêm 4 port.</summary>
    public StepNodeModel(GraphNode data)
        : base(data.Id, new Point(data.X, data.Y))
    {
        Data = data;
        AddPort(PortAlignment.Left);
        AddPort(PortAlignment.Right);
        AddPort(PortAlignment.Top);
        AddPort(PortAlignment.Bottom);
    }

    /// <summary>Dữ liệu step.</summary>
    public GraphNode Data { get; }

    /// <summary>Trạng thái chạy hiện tại (tô màu realtime).</summary>
    public NodeRunState RunState { get; set; } = NodeRunState.Idle;

    /// <summary>Dòng metric hiển thị dưới card khi chạy/xong (token/cost/latency).</summary>
    public string? RunMeta { get; set; }

    /// <summary>Callback mở inspector cho node này (page gán; widget gọi khi bấm nút ✎).</summary>
    public Action? RequestEdit { get; set; }
}
