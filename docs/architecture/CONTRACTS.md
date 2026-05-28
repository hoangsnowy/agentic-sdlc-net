# Interface Contracts (design)

> C# sketches for the platform's core seams. These are **design artifacts**, not compiled code — Phase 1 of
> the migration turns the `Core`/`Runtime`/`Providers` subset into a real, tested `/src/Core` + `/src/Runtime`
> project with an in-process adapter over today's pipeline. Names are normative; signatures may refine during
> implementation. Async-first, nullable-enabled, no provider/SDK types leak across a boundary.

## Core kernel — `AgenticSdlc.Core`

```csharp
namespace AgenticSdlc.Core;

public readonly record struct AgentId(string Value);
public readonly record struct RunId(Guid Value);
public readonly record struct StepId(Guid Value);
public readonly record struct TenantId(string Value);

/// <summary>A capability an agent may be granted (the unit of authorization for tools/actions).</summary>
public readonly record struct Capability(string Name)   // e.g. "fs.read", "git.commit", "shell.exec", "net.http"
{
    public static readonly Capability FsRead   = new("fs.read");
    public static readonly Capability FsWrite  = new("fs.write");
    public static readonly Capability GitRead  = new("git.read");
    public static readonly Capability GitWrite = new("git.write");
    public static readonly Capability ShellExec = new("shell.exec");
    public static readonly Capability BuildRun = new("build.run");
}

/// <summary>Risk tier drives whether an action is auto-approved or needs a human (see GOVERNANCE.md).</summary>
public enum RiskTier { ReadOnly, Reversible, Irreversible, Destructive }

public abstract record Result
{
    public sealed record Ok(object? Value = null) : Result;
    public sealed record Error(string Code, string Message) : Result;
}
```

## Runtime — `AgenticSdlc.Runtime`

The seam everything attaches to. A runtime executes one **agent session**: a loop of reason → (optionally)
call a tool → observe → continue, until the agent yields a final result or hits a budget/iteration cap.

```csharp
namespace AgenticSdlc.Runtime;

using AgenticSdlc.Core;

public interface IAgentRuntime
{
    /// <summary>Stable id, e.g. "inprocess-chat", "semantic-kernel", "claude-code-cli", "ollama".</summary>
    string Kind { get; }

    /// <summary>Run an agent session to completion (may issue many model + tool calls internally).</summary>
    Task<AgentResult> ExecuteAsync(AgentSession session, CancellationToken ct = default);

    /// <summary>Streaming variant — emits steps as they happen (for the Studio + evidence).</summary>
    IAsyncEnumerable<AgentStep> StreamAsync(AgentSession session, CancellationToken ct = default);
}

/// <summary>Everything a runtime needs for one session — no host/SDK types.</summary>
public sealed record AgentSession(
    RunId Run,
    AgentId Agent,
    string Goal,                       // what this agent must achieve this turn
    IReadOnlyList<ChatTurn> History,   // prior turns (from memory)
    AgentBudget Budget,                // max tokens / cost / wall-clock / tool calls
    IToolbox Toolbox,                  // capability-scoped tools available to this session
    ICognitionContext Memory,          // read/write memory
    IEvidenceSink Evidence,            // per-step evidence
    ModelPreference Model);            // preferred provider/model + params (advisory; registry resolves)

public sealed record AgentBudget(int MaxTokens, decimal MaxCostUsd, TimeSpan MaxWallClock, int MaxToolCalls, int MaxIterations);
public sealed record ModelPreference(string? Provider, string? Model, double Temperature, int MaxTokens);

public abstract record AgentStep(StepId Id, DateTimeOffset At)
{
    public sealed record Thought(StepId Id, DateTimeOffset At, string Text) : AgentStep(Id, At);
    public sealed record ToolCall(StepId Id, DateTimeOffset At, ToolInvocation Invocation) : AgentStep(Id, At);
    public sealed record Observation(StepId Id, DateTimeOffset At, ToolResult Result) : AgentStep(Id, At);
    public sealed record Final(StepId Id, DateTimeOffset At, string Output) : AgentStep(Id, At);
}

public sealed record AgentResult(RunId Run, AgentId Agent, string Output, AgentUsage Usage, bool Succeeded, string? Error);
public sealed record AgentUsage(long TokensIn, long TokensOut, decimal CostUsd, TimeSpan Latency, int ToolCalls, int Iterations);

public interface IAgentRuntimeFactory
{
    IAgentRuntime Resolve(AgentId agent);   // picks runtime per agent config (Agents:<name>:Runtime)
}
```

### Runtime adapters (examples)

```csharp
// (1) Compatibility adapter — wraps today's single-shot ILlmClient. One model call, no tool loop.
public sealed class InProcessChatRuntime(IModelProviderRegistry providers) : IAgentRuntime
{
    public string Kind => "inprocess-chat";
    public async Task<AgentResult> ExecuteAsync(AgentSession s, CancellationToken ct = default)
    {
        var provider = providers.Resolve(s.Model);
        var reply = await provider.CompleteAsync(new ModelRequest(s.Goal, s.History, s.Model), ct);
        await s.Evidence.RecordAsync(EvidenceRecord.ForModelCall(s.Run, s.Agent, reply), ct);
        return new AgentResult(s.Run, s.Agent, reply.Content, reply.ToUsage(), true, null);
    }
    public IAsyncEnumerable<AgentStep> StreamAsync(AgentSession s, CancellationToken ct = default) => /* wrap ExecuteAsync */;
}

// (2) Semantic Kernel adapter — real think→tool→observe loop, tools surfaced as SK functions.
public sealed class SemanticKernelRuntime(IKernelBuilderFactory kf, IModelProviderRegistry providers) : IAgentRuntime
{
    public string Kind => "semantic-kernel";
    // Build a Kernel bound to the resolved provider; register s.Toolbox as KernelFunctions
    // (each invocation routed through capability check + sandbox + governance); run the planner/auto-function-calling loop;
    // emit AgentStep per thought/tool/observation; persist evidence; stop on Final or budget.
}

// (3) Claude Code CLI adapter — out-of-process agentic runtime.
public sealed class ClaudeCodeCliRuntime(IProcessSandbox sandbox) : IAgentRuntime
{
    public string Kind => "claude-code-cli";
    // Launch the CLI inside a sandbox with a capability-scoped workspace; stream its events;
    // map CLI tool-use to ToolInvocation/ToolResult so the SAME governance + evidence path applies.
}
```

The orchestrator/workflow only ever sees `IAgentRuntime` — adding (3) or `OllamaRuntime` changes no caller.

## Providers — `AgenticSdlc.Providers`

```csharp
namespace AgenticSdlc.Providers;

public interface IModelProvider
{
    string Name { get; }                       // "anthropic", "azure-openai", "ollama", "openrouter"
    ProviderCapabilities Capabilities { get; } // tool-use? streaming? json-mode? context window?
    DataResidency Residency { get; }           // CloudExternal | CloudRegionPinned | SelfHosted

    Task<ModelReply> CompleteAsync(ModelRequest request, CancellationToken ct = default);
    IAsyncEnumerable<ModelDelta> StreamAsync(ModelRequest request, CancellationToken ct = default);
    Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken ct = default);
}

public sealed record ProviderCapabilities(bool ToolUse, bool Streaming, bool JsonMode, int ContextWindow, bool Embeddings);
public enum DataResidency { CloudExternal, CloudRegionPinned, SelfHosted }

public interface IModelProviderRegistry
{
    IModelProvider Resolve(ModelPreference preference);     // honors ForceProvider/policy routing
    IModelProvider Get(string name);
    IReadOnlyList<IModelProvider> All { get; }
}
```
*Note:* today's `ILlmClient` becomes a thin facade over `IModelProvider` during migration; the `Llm:ForceProvider`
switch already shipped maps onto registry routing.

## Memory / Cognition — `AgenticSdlc.Memory`

```csharp
namespace AgenticSdlc.Memory;

public interface ICognitionContext            // facade an agent session uses
{
    IEpisodicMemory Episodic { get; }
    ISemanticMemory Semantic { get; }
    IExecutionLineage Lineage { get; }
    IDecisionLog Decisions { get; }
    IDebuggingMemory Debugging { get; }
}

public interface IEpisodicMemory   // what happened, per run/agent
{
    Task AppendAsync(RunId run, EpisodeEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<EpisodeEntry>> RecallAsync(RunId run, int max, CancellationToken ct = default);
}

public interface ISemanticMemory   // vector knowledge of domain/repo
{
    Task UpsertAsync(MemoryDocument doc, CancellationToken ct = default);
    Task<IReadOnlyList<MemoryHit>> SearchAsync(string query, int k, MemoryFilter? filter = null, CancellationToken ct = default);
}

public interface IExecutionLineage // causal graph: step → caused → step (audit)
{
    Task LinkAsync(StepId cause, StepId effect, string relation, CancellationToken ct = default);
    Task<LineageGraph> ForRunAsync(RunId run, CancellationToken ct = default);
}

public interface IDecisionLog      // auto-ADR: decision + rationale + alternatives
{
    Task RecordAsync(DecisionEntry decision, CancellationToken ct = default);
}

public interface IDebuggingMemory  // errors seen + resolutions, reusable across runs
{
    Task RecordFailureAsync(FailureSignature sig, string? resolution, CancellationToken ct = default);
    Task<string?> SuggestResolutionAsync(FailureSignature sig, CancellationToken ct = default);
}
```
Memory items carry a `Classification` (Public/Internal/Secret) and `TenantId` — enforced on read (see GOVERNANCE.md).

## Evidence / Tracing — `AgenticSdlc.Tracing`

```csharp
namespace AgenticSdlc.Tracing;

public interface IEvidenceSink
{
    Task RecordAsync(EvidenceRecord record, CancellationToken ct = default);
}

public sealed record EvidenceRecord(
    RunId Run, AgentId Agent, StepId Step, EvidenceKind Kind,
    string? Prompt, string? RawOutput, ToolInvocation? ToolIn, ToolResult? ToolOut,
    string? Rationale, IReadOnlyDictionary<string, string> Attributes, DateTimeOffset At);

public enum EvidenceKind { ModelCall, ToolCall, GateDecision, QaVerdict, WorkflowTransition }
```
Each record correlates to an OpenTelemetry span (`run_id`, `agent_id`, `step_id` as attributes). Redaction runs at write.

## Governance — `AgenticSdlc.Governance` (contracts; model in GOVERNANCE.md)

```csharp
namespace AgenticSdlc.Governance;

public interface IPolicyEngine
{
    Task<PolicyDecision> EvaluateAsync(ActionContext action, CancellationToken ct = default);
}

public sealed record ActionContext(RunId Run, AgentId Agent, TenantId Tenant, Capability Capability, RiskTier Risk, IReadOnlyDictionary<string, string> Facts);
public abstract record PolicyDecision
{
    public sealed record Allow : PolicyDecision;
    public sealed record Deny(string Reason) : PolicyDecision;
    public sealed record RequireApproval(string Reason, ApprovalScope Scope) : PolicyDecision;
}

public interface IApprovalGate
{
    Task<ApprovalTicket> RequestAsync(ApprovalRequest request, CancellationToken ct = default);
    Task<ApprovalOutcome> WaitAsync(ApprovalTicket ticket, CancellationToken ct = default); // durable signal
}
```

## Execution / Tools — `AgenticSdlc.Execution`

```csharp
namespace AgenticSdlc.Execution;

public interface IToolbox                       // capability-scoped view handed to a session
{
    IReadOnlyList<ToolDescriptor> Available { get; }
    Task<ToolResult> InvokeAsync(ToolInvocation invocation, CancellationToken ct = default);
}

public interface ITool
{
    ToolDescriptor Descriptor { get; }          // name, schema, required capabilities, risk tier
    Task<ToolResult> ExecuteAsync(ToolInvocation invocation, IToolExecutionContext ctx, CancellationToken ct = default);
}

public sealed record ToolDescriptor(string Name, string JsonSchema, IReadOnlyList<Capability> Requires, RiskTier Risk);
public sealed record ToolInvocation(string Tool, string ArgumentsJson, RunId Run, AgentId Agent);
public sealed record ToolResult(bool Ok, string OutputJson, string? Error, ToolEvidence Evidence);

public interface ISandbox                        // process/container isolation boundary
{
    Task<SandboxLease> AcquireAsync(SandboxSpec spec, CancellationToken ct = default); // workspace, net policy, fs scope, limits
}

// Invocation pipeline (enforced centrally, not per tool):
//   IToolbox.Invoke → capability check (grants) → IPolicyEngine.Evaluate → [IApprovalGate] → ISandbox → ITool.Execute → IEvidenceSink
```
The **central invocation pipeline** is the security keystone: a tool author cannot bypass capability/policy/sandbox/evidence.

## Workflow — `AgenticSdlc.Workflow`

```csharp
namespace AgenticSdlc.Workflow;

public interface IWorkflowEngine
{
    Task<RunId> StartAsync(WorkflowDefinition def, WorkflowInput input, CancellationToken ct = default);
    Task SignalAsync(RunId run, string signal, object payload, CancellationToken ct = default);  // e.g. approval
    Task<WorkflowState> GetStateAsync(RunId run, CancellationToken ct = default);                 // resumable/queryable
}

public interface IWorkflowDefinition   // durable, checkpointed; the SDLC Quality Loop becomes one of these
{
    string Name { get; }
    Task<WorkflowState> ExecuteAsync(IWorkflowContext ctx, CancellationToken ct = default);
}
// IWorkflowContext exposes durable primitives: CallAgentAsync (via IAgentRuntime), WaitForApprovalAsync,
// CheckpointAsync, ScheduleAsync — each replay-safe + idempotent.
```

## Agents — `AgenticSdlc.Agents`

```csharp
namespace AgenticSdlc.Agents;

public sealed record AgentSpec(
    AgentId Id, string Role, string PromptRef,            // → ISpecRegistry, not an inline string
    IReadOnlyList<Capability> Capabilities, ModelPreference DefaultModel, string Runtime);

public interface IAgent
{
    AgentSpec Spec { get; }
    Task<AgentResult> RunAsync(AgentSession session, CancellationToken ct = default); // delegates to its IAgentRuntime
}
```

## Knowledge graph — `AgenticSdlc.KnowledgeGraph`

```csharp
namespace AgenticSdlc.KnowledgeGraph;

public interface IKnowledgeGraph
{
    Task IngestAsync(RepoSnapshot snapshot, CancellationToken ct = default);   // build entities/edges
    Task<IReadOnlyList<GraphNode>> QueryAsync(GraphQuery query, CancellationToken ct = default);
    Task<RepoContext> ContextForAsync(string focus, int budgetTokens, CancellationToken ct = default); // accurate context, not raw dumps
}
```
