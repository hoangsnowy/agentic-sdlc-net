// AgenticSdlc.Web/Orchestrations/OrchestrationModels.cs
// Phase 7b — Mô hình đồ thị orchestration cho editor kéo-thả (UI-layer, không thuộc Domain lõi).
// Một orchestration = tập node (step) + edge (route) trên canvas. Editor cho phép dựng đồ thị
// bất kỳ; "Run" sẽ thông dịch đồ thị (xem OrchestrationRunner).

using System.Collections.Generic;

namespace AgenticSdlc.Web.Orchestrations;

/// <summary>Loại step trong orchestration (khớp palette "Add step" kiểu Synapse).</summary>
public enum StepType
{
    /// <summary>Gọi LLM thô với prompt tự do.</summary>
    Llm,

    /// <summary>Tác tử chuyên biệt (Requirement / Coding / Testing / QA / Orchestrator).</summary>
    Agent,

    /// <summary>Gọi tool/hàm ngoài.</summary>
    Tool,

    /// <summary>Đánh giá output rồi rẽ nhánh theo route (vd: pass / fail).</summary>
    Evaluator,

    /// <summary>Chạy nhiều nhánh song song.</summary>
    Parallel,

    /// <summary>Gộp kết quả nhiều nhánh.</summary>
    Merge,

    /// <summary>Lặp một nhánh tới điều kiện dừng.</summary>
    Loop,

    /// <summary>Chốt chặn người (human-in-the-loop).</summary>
    Human,

    /// <summary>Biến đổi dữ liệu (map/format).</summary>
    Transform,

    /// <summary>Trích JSON từ text.</summary>
    ExtractJson,

    /// <summary>Rẽ nhánh nhị phân theo điều kiện.</summary>
    IfElse,

    /// <summary>Rẽ nhiều nhánh theo giá trị.</summary>
    Switch,

    /// <summary>In/log giá trị.</summary>
    Print,

    /// <summary>Kết thúc orchestration.</summary>
    End,
}

/// <summary>Nhóm hiển thị (quyết định màu card). Suy ra từ <see cref="StepType"/>.</summary>
public enum StepCategory
{
    /// <summary>Tác tử — teal.</summary>
    Agent,

    /// <summary>Đánh giá/rẽ nhánh — tím.</summary>
    Evaluator,

    /// <summary>Người — đỏ/maroon.</summary>
    Human,

    /// <summary>LLM thô — xanh dương.</summary>
    Llm,

    /// <summary>Logic luồng (Loop/Merge/Parallel/Transform/ExtractJson) — xám.</summary>
    Logic,

    /// <summary>Kết thúc — xám đậm.</summary>
    Terminal,
}

/// <summary>Trạng thái runtime của một node khi chạy (để tô màu realtime).</summary>
public enum NodeRunState
{
    /// <summary>Chưa chạy.</summary>
    Idle,

    /// <summary>Đang chạy.</summary>
    Running,

    /// <summary>Xong.</summary>
    Done,

    /// <summary>Lỗi.</summary>
    Failed,
}

/// <summary>Một step trên canvas. Vị trí (X,Y) là toạ độ canvas.</summary>
public sealed class GraphNode
{
    /// <summary>Id duy nhất trong đồ thị.</summary>
    public required string Id { get; set; }

    /// <summary>Loại step.</summary>
    public StepType Type { get; set; }

    /// <summary>Tên hiển thị trên thanh tiêu đề card.</summary>
    public string Title { get; set; } = "Step";

    /// <summary>Toạ độ X trên canvas.</summary>
    public double X { get; set; }

    /// <summary>Toạ độ Y trên canvas.</summary>
    public double Y { get; set; }

    /// <summary>Vai trò tác tử (chỉ với <see cref="StepType.Agent"/>): Requirement/Coding/Testing/Qa/Orchestrator.</summary>
    public string? AgentRole { get; set; }

    /// <summary>Mô tả / prompt ngắn hiển thị trong thân card.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Khoá state đầu vào (hiển thị "in:").</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>Khoá state đầu ra (hiển thị "out:").</summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>Số lần lặp tối đa (hiển thị "max Nx"). 0 ⇒ ẩn.</summary>
    public int MaxIterations { get; set; }

    /// <summary>Các route rẽ nhánh (Evaluator/IfElse/Switch). Hiển thị "N routes: a, b".</summary>
    public List<string> Routes { get; set; } = [];

    /// <summary>Node bắt đầu đồ thị (badge START xanh).</summary>
    public bool IsStart { get; set; }

    /// <summary>Nhóm hiển thị suy ra từ <see cref="Type"/> (+ vai trò).</summary>
    public StepCategory Category => Type switch
    {
        StepType.Agent => StepCategory.Agent,
        StepType.Evaluator or StepType.IfElse or StepType.Switch => StepCategory.Evaluator,
        StepType.Human => StepCategory.Human,
        StepType.Llm => StepCategory.Llm,
        StepType.End => StepCategory.Terminal,
        _ => StepCategory.Logic,
    };
}

/// <summary>Một edge nối 2 node, có nhãn route tuỳ chọn.</summary>
public sealed class GraphEdge
{
    /// <summary>Id duy nhất.</summary>
    public required string Id { get; set; }

    /// <summary>Node nguồn.</summary>
    public required string SourceId { get; set; }

    /// <summary>Node đích.</summary>
    public required string TargetId { get; set; }

    /// <summary>Nhãn route (vd "continue", "pass", "fail", "ask_user"). Rỗng ⇒ không hiện.</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>Một orchestration: metadata + đồ thị + state schema + guardrails.</summary>
public sealed class OrchestrationGraph
{
    /// <summary>Id duy nhất.</summary>
    public required string Id { get; set; }

    /// <summary>Tên hiển thị (selector + tiêu đề).</summary>
    public string Name { get; set; } = "Untitled";

    /// <summary>Mô tả ngắn.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Các node.</summary>
    public List<GraphNode> Nodes { get; set; } = [];

    /// <summary>Các edge.</summary>
    public List<GraphEdge> Edges { get; set; } = [];

    /// <summary>State schema JSON (tab "State Schema").</summary>
    public string StateSchemaJson { get; set; } = "{\n  \"input\": \"string\"\n}";

    /// <summary>Guardrails (tab "Guardrails").</summary>
    public List<string> Guardrails { get; set; } = [];
}
