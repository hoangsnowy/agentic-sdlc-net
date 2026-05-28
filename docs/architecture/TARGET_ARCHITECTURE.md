# Target Architecture — Agentic SDLC Platform (.NET)

> Status: **design** (not yet executed). This document defines where the codebase is heading — a
> production-grade, enterprise agentic platform — and the reasoning behind each decision. It does **not**
> change the running prototype. Execution is phased (see [MIGRATION_BACKLOG.md](MIGRATION_BACKLOG.md));
> the thesis prototype stays intact and is tagged `v1.0-thesis` before any migration begins.
>
> Companion docs: [CONTRACTS.md](CONTRACTS.md) · [GOVERNANCE.md](GOVERNANCE.md) ·
> [MIGRATION_BACKLOG.md](MIGRATION_BACKLOG.md) · [ADRs](../adr/) · [ROADMAP_PLATFORM_V2.md](../ROADMAP_PLATFORM_V2.md)

## 0. How to read & execute this

This is a **target**, reached by migration phases, not a rewrite. Three rules govern execution:

1. **Strangler-fig, not big-bang.** New pillars are introduced behind interfaces; the existing pipeline keeps
   working and is migrated agent-by-agent. No "stop the world" rewrite.
2. **Interfaces before implementations, implementations before refactors.** We add the `*Core*`/`*Runtime*`
   contracts first (compiling, with the in-process adapter wrapping today's code), then grow real
   implementations, then relocate code. We do **not** create empty capability folders up front.
3. **Each phase ships something runnable + tested.** A phase that can't be demoed or tested is not done.

## 1. Current-state review (honest)

The repository today is a **well-built application**, not yet a platform:

- Clean Architecture (`Domain → Application → Infrastructure`, hosts `Api`/`Web`), dependency direction enforced by project refs.
- A real **LLM gateway**: 5 agents depend on `ILlmClient` (Domain); `LlmClientFactory` selects a provider per agent. Three clients (Claude, Azure OpenAI, Mock) + a Web `DemoLlmClient`.
- A **Quality Loop** orchestrator (Requirement → loop[Coding → Testing → QA] ≤ NMax), metrics, realtime progress sink, a Blazor "Agent Studio", EF Core/Postgres persistence, .NET Aspire + OpenTelemetry, 160 passing tests.

It abstracts **exactly one axis** — the chat-model provider. Every "agent" is, mechanically, *one
chat-completion call + JSON parse, in-process*. That is the gap between an *application* and a *platform*.

## 2. Architectural weaknesses (condensed)

Full analysis: the W1–W10 review. Summary of what a platform needs that we lack:

| # | Gap | Pillar that fixes it |
|---|---|---|
| W1 | Only `ILlmClient` (chat-completion); no agent *runtime* abstraction | Runtime |
| W2 | Stateless agents; no persistent cognition | Memory |
| W3 | No tool-calling; no sandbox/capability model | Execution/Tools |
| W4 | Orchestration is an in-process `for` loop; not durable/resumable | Workflow |
| W5 | Provider set is narrow; "agnostic" only at the chat layer | Providers |
| W6 | Prompts are `static`, compiled into the app | Spec/Prompt registry |
| W7 | Governance is principle, not mechanism (no policy/approval gate) | Governance |
| W8 | Tracing stops at the service layer; no reasoning-level evidence | Evidence/Tracing |
| W9 | Both hosts run the engine in-process; no control/data-plane split | Topology |
| W10 | Folder layout is a 3-layer app, not capability-oriented | Structure (a *result*, not a first step) |

## 3. Principles (made concrete)

| Principle | Concrete meaning in this architecture |
|---|---|
| Runtime abstraction first | `IAgentRuntime` is the seam everything else plugs into; orchestration never talks to a model SDK directly. |
| Model/provider agnostic | `IModelProvider` + registry; providers declare capabilities & data-residency; no provider type leaks past the boundary. |
| Governed execution | Every side-effecting action passes a `IPolicyEngine` decision and (when required) an `IApprovalGate`. |
| Persistent cognition | Agents read/write a memory substrate (episodic/semantic/lineage/decision/debug); runs are not amnesiac. |
| Evidence-driven | Every step emits an `EvidenceRecord` (prompt, output, tool I/O, rationale) — claims are backed by artifacts. |
| Security-first | Tools run sandboxed under explicit capability grants; secrets and memory are classified, not flat. |
| Enterprise operability | Control-plane/data-plane split, OTel everywhere, structured logs, multi-tenant isolation. |
| Long-horizon workflows | A durable workflow engine (checkpoint/replay) survives restarts and spans hours/days. |

## 4. Target architecture

### 4.1 Topology — control-plane vs data-plane

```
            ┌──────────────────────── CONTROL PLANE (thin, trusted) ─────────────────────────┐
            │  API / Studio (Web)   Workflow engine   Policy/Governance   Registries          │
            │  - accept work        - schedule        - approve gates     - providers/agents   │
            │  - query state        - checkpoint      - audit             - prompts/specs      │
            └───────────────┬───────────────────────────────────────────────┬─────────────────┘
                            │ commands / events (bus)                        │ read models
            ┌───────────────▼──────────────── DATA PLANE (scaled, less trusted) ──────────────┐
            │  Execution workers                                                               │
            │   └─ IAgentRuntime  ─ runs an agent session (think → tool → observe → loop)      │
            │        ├─ IModelProvider (Anthropic / Azure / Ollama / OpenRouter)               │
            │        ├─ IToolHost + ISandbox (fs, git, build, test — capability-gated)         │
            │        ├─ ICognitionContext (memory read/write)                                  │
            │        └─ IEvidenceSink (per-step evidence + OTel spans)                         │
            └──────────────────────────────────────────────────────────────────────────────────┘
```

The control-plane is small and highly trusted (it never runs arbitrary tools). Execution workers are the
blast-radius boundary: they run model calls and **real tools**, so they are isolated, capability-scoped, and
independently scaled. This split is the single most important enterprise property — see ADR-0008.

### 4.2 Pillars

Each pillar below lists its responsibility, key contracts (full signatures in [CONTRACTS.md](CONTRACTS.md)),
and the four enterprise lenses: **Why · Tradeoffs · Scaling · Security · Enterprise**.

#### Runtime — `IAgentRuntime`
*Responsibility:* execute one **agent session** — a stateful loop (reason → call tool → observe → repeat)
— independent of *how* the underlying model/agent is hosted. Adapters: `InProcessChatRuntime` (wraps today's
`ILlmClient`, the compatibility path), `SemanticKernelRuntime` (function-calling loop), `ClaudeCodeCliRuntime`
(out-of-process, agentic CLI), `OllamaRuntime`.
- **Why:** the runtimes we must support are *not* the same shape. Claude Code CLI is a stateful, multi-step,
  tool-using process; forcing it into "one request → one response" is the wrong abstraction. The runtime seam
  is where tools, memory, evidence, and governance attach. Get this wrong and everything reattaches later.
- **Tradeoffs:** an extra indirection over a direct model call; the in-process adapter keeps the simple case simple.
- **Scaling:** runtimes can be in-process, out-of-process, or remote/containerized — orchestration doesn't change.
- **Security:** the runtime is the natural place to enforce the sandbox/capability boundary (a remote runtime is a containment unit).
- **Enterprise:** new runtimes plug in without touching the orchestrator → low regression risk, vendor independence.

#### Providers — `IModelProvider` + `IModelProviderRegistry`
*Responsibility:* a model endpoint (chat/completion/embeddings) that **declares its capabilities** (tool-use,
streaming, JSON-mode, context window) and **data-residency** (cloud vs self-hosted). Anthropic, Azure OpenAI,
Ollama, OpenRouter.
- **Why:** "agnostic" today is only true at the chat layer. A registry with capability descriptors lets the
  platform *route* (e.g. send sensitive repos to a self-hosted Ollama, route tool-use tasks to a tool-capable model).
- **Tradeoffs:** a richer contract than `ILlmClient`; mitigated by a base adapter and keeping `ILlmClient` as a thin facade during migration.
- **Scaling:** per-provider connection pools, rate-limit/circuit-breaker policies, cost-aware routing.
- **Security:** data-residency is a first-class attribute → governance can *forbid* sensitive data leaving the org.
- **Enterprise:** no provider lock-in; procurement can swap vendors via config + a plugin assembly.

#### Memory / Cognition — `ICognitionContext`
*Responsibility:* persistent, classified memory. Five stores: **episodic** (what happened), **semantic**
(vector knowledge of the domain/repo), **execution lineage** (causal graph of steps/decisions),
**decision log** (auto-generated ADR-like records), **debugging memory** (errors seen + fixes).
- **Why:** stateless agents relearn from zero every run and cannot accumulate repo understanding — a hard
  blocker for long-horizon work. Lineage is also an *audit* requirement ("what did the agent decide, on what basis").
- **Tradeoffs:** a real bounded context (don't bolt it onto the pipeline DbContext); operational cost of a vector store.
- **Scaling:** split read model (semantic/vector) from write model (episodic/lineage); memory is shardable per tenant/repo.
- **Security:** memory holds code and possibly secrets → classification + access control + redaction, not a flat table.
- **Enterprise:** lineage + decision log make the system *auditable* and *explainable*, a procurement gate for regulated buyers.

#### Evidence / Tracing — `IEvidenceSink`
*Responsibility:* every agent/tool step leaves a reproducible `EvidenceRecord` (actual prompt, raw output,
tool input/output, why a gate passed/failed), correlated to OTel spans.
- **Why:** "evidence-driven automation" means claims are backed by artifacts you can replay — not by the model's say-so. Today's metrics answer "how much" but not "what & why".
- **Tradeoffs:** evidence is heavy (full prompts/outputs) → retention + redaction policy required.
- **Scaling:** append-only evidence store, tiered retention, sampling for low-risk steps.
- **Security:** evidence may contain secrets → redaction at write, access-controlled reads.
- **Enterprise:** the substrate for incident debugging, audit, and post-hoc review of autonomous actions.

#### Governance — `IPolicyEngine` + `IApprovalGate`
*Responsibility:* a tangible gate before any risky/irreversible action (write to main, open PR, run a command,
move data). Policy evaluates an action against rules → allow / deny / require-approval; approval gates block
for a human decision. Detail: [GOVERNANCE.md](GOVERNANCE.md).
- **Why:** "human-in-the-loop" must be a *mechanism*, not a comment. Without it, no compliance-bound enterprise will run agents on its repos.
- **Tradeoffs:** latency on gated actions; mitigated by risk-tiered auto-approval for low-risk actions.
- **Scaling:** policy evaluation is stateless and cacheable; approvals are durable workflow signals.
- **Security:** this is the enforcement point for least privilege and irreversibility controls.
- **Enterprise:** the difference between "demo" and "sellable" — auditable, policy-driven autonomy.

#### Execution / Tools — `IToolHost` + `ITool` + `ICapability` + `ISandbox`
*Responsibility:* let agents *act* — read/write files, run `git`, `dotnet build`/`test`, call external tools —
via **MCP** where possible, each invocation capability-checked and sandboxed (process/container isolation).
- **Why:** autonomous coding / CI/CD / repo-understanding agents are inherently tool-driven. Without tools, agents only *guess* code; with tools, they *verify* it (this is the root of evidence-driven).
- **Tradeoffs:** large new surface; sandboxing adds ops complexity. Non-negotiable for security.
- **Scaling:** tool execution is the hottest, riskiest path → isolated workers, quota per capability.
- **Security:** **P0** — an agent running shell/file writes is the platform's largest attack surface. Capability grants + sandbox + governance gate are mandatory *before* any real tool runs.
- **Enterprise:** capability scoping per tenant/repo is what lets an org say "agents may read, may run tests, may not push to main".

#### Workflow — `IWorkflowEngine`
*Responsibility:* durable, resumable orchestration — replace the in-process `for` loop with checkpoint/replay
state machines. Evaluate: Microsoft DurableTask, Dapr Workflows, Temporal .NET.
- **Why:** long-horizon runs must survive deploys/restarts, resume, and distribute steps across workers.
- **Tradeoffs:** event-sourcing/state-machine complexity; idempotency discipline required.
- **Scaling:** runs span hours/days; many concurrent runs with isolation and observability.
- **Security:** durable state is sensitive (carries artifacts) → encrypted at rest, tenant-scoped.
- **Enterprise:** SLA-grade reliability; the basis for "the agent worked on this for 6 hours and resumed after a deploy".

#### Agents — `IAgent` (declarative)
*Responsibility:* an agent is a **declarative spec** (role, capabilities, model preference, prompt/spec ref,
constraints), not a hardcoded class. SDLC agents: Requirement, Coding, Testing, QA today; Design, DevOps,
Documentation, Review, Architecture-review later.
- **Why:** decouple agent definition from code so non-developers can author/version agents and the platform can host many.
- **Tradeoffs:** indirection vs a coded agent; pays off past ~5 agents.
- **Scaling/Security/Enterprise:** agents become governed, versioned assets; new SDLC roles are additive.

#### KnowledgeGraph — `IKnowledgeGraph`
*Responsibility:* structured repo understanding (entities: files/types/endpoints/specs; edges: calls/depends/tests)
feeding agents accurate context (vs dumping raw files).
- **Why:** "repo understanding" + accurate retrieval for large codebases; reduces hallucination and token cost.
- **Enterprise:** the asset that makes agents *good* on a specific large codebase.

#### Integrations — VCS / CI / Issues
*Responsibility:* GitHub (PRs, reviews, checks), CI/CD pipelines, issue trackers — as governed tools/runtimes.
- **Enterprise:** this is where the platform meets the customer's existing SDLC; all integrations are capability-gated and audited.

#### Spec-driven layer — `ISpecRegistry`
*Responsibility:* specifications (requirements, acceptance criteria, prompts, agent definitions) are
versioned, reviewable runtime resources — the source of truth that drives agents, not strings in code (W6).
- **Why:** prompts/specs are assets with their own lifecycle (review, A/B, rollback); coupling them to code blocks all of that.

## 5. Target folder structure

Reached **incrementally** (a *result* of the phases, not created empty up front — W10):

```
/src
  /Core            // kernel: ids, value types, Result, Capability, shared abstractions (no deps)
  /Runtime         // IAgentRuntime + adapters (InProcess, SemanticKernel, ClaudeCodeCli, Ollama)
  /Providers       // IModelProvider + registry + Anthropic/Azure/Ollama/OpenRouter
  /Memory          // episodic/semantic/lineage/decision/debug stores + ICognitionContext
  /Governance      // IPolicyEngine, IApprovalGate, policy model, audit
  /Execution       // IToolHost, ISandbox, capability enforcement, MCP tool adapters
  /Tools           // concrete tools (fs, git, dotnet build/test) as MCP servers/clients
  /Workflow        // IWorkflowEngine + durable definitions for SDLC flows
  /Tracing         // IEvidenceSink, evidence store, OTel wiring
  /Policies        // shipped policy packs (default governance rules)
  /Agents          // IAgent specs + SDLC agent definitions
  /KnowledgeGraph  // repo graph build + query
  /Integrations    // GitHub, CI/CD, issue trackers
  /Hosts
    /ControlPlane  // API + Studio (thin)
    /Worker        // execution worker host (data plane)
```

`Domain`/`Application`/`Infrastructure` map into `Core` + the capability modules during migration; the
existing `Api`/`Web` become `Hosts/ControlPlane`.

## 6. Non-functional requirements (mapping)

| NFR | Where it's satisfied |
|---|---|
| Clean architecture | `Core` has no deps; capability modules depend inward; hosts compose. |
| Highly testable | Every pillar is an interface with an in-memory fake; runtimes/providers have contract tests. |
| Async-first | All contracts are `Task`/`IAsyncEnumerable`; streaming via the runtime. |
| Event-driven | Control↔data plane communicate via a bus; evidence/lineage are events. |
| Structured logging + OTel | `Tracing` pillar; spans around every agent/tool step (extends today's Aspire OTel). |
| Cloud-native + horizontal scale | Plane split; stateless workers; durable workflow externalizes state. |
| Enterprise security posture | Capability + sandbox + governance + classified memory/evidence. |

## 7. Runtime abstraction example

See [CONTRACTS.md §Runtime](CONTRACTS.md) for `IAgentRuntime`, `AgentSession`, and three worked adapters
(in-process chat, Semantic Kernel tool-loop, Claude Code CLI).

## 8. Migration (summary)

Dependency-ordered; full detail in [MIGRATION_BACKLOG.md](MIGRATION_BACKLOG.md):

1. `Core` + `IAgentRuntime` with `InProcessChatRuntime` wrapping today's pipeline (no behavior change).
2. `IModelProvider` registry; fold the 3 existing clients behind it; add Ollama/OpenRouter.
3. Tools + sandbox + capability **with** the governance gate (one inseparable security step).
4. Memory + evidence/lineage.
5. Durable workflow replaces the `for` loop.
6. Claude Code CLI runtime; plane split; spec/prompt registry; capability-based restructure.

## 9. Explicitly avoided

| Anti-pattern (from the brief) | How the design avoids it |
|---|---|
| Prompts coupled to orchestration | Spec/prompt registry (W6), loaded at runtime. |
| Provider lock-in | `IModelProvider` registry + capability descriptors; no SDK type past the seam. |
| Stateless agents | `ICognitionContext` memory substrate. |
| Giant god services | Capability modules + thin control-plane + workers; no single orchestrator owns everything. |
| Hidden AI behavior | Evidence records + lineage + structured traces for every step. |
| Non-auditable actions | Governance gate + immutable audit + approval records. |
