# Migration Roadmap & Backlog

> Phased, dependency-ordered path from the current prototype to the [target architecture](TARGET_ARCHITECTURE.md).
> Strangler-fig: each phase ships runnable + tested; the prototype keeps working throughout. **Phase 0 (tag
> `v1.0-thesis`) gates everything** — no structural migration before the defense.

## Sequencing principle

Order follows **causal dependency**, not ease. The runtime seam (M1) comes first because tools, memory,
evidence, and governance all attach to it; security (M3) is inseparable from first tool use. Reordering creates
rework. Each phase = an epic; stories carry acceptance criteria (AC) and a verification hook.

```
M0 freeze ─▶ M1 runtime ─▶ M2 providers ─▶ M3 tools+sandbox+governance ─▶ M4 memory+evidence
                                                                              │
                              M6 plane split + spec registry + restructure ◀─ M5 durable workflow
```

---

## M0 — Stabilize & freeze (now → defense)
**Goal:** lock the thesis evidence; do not refactor.
- **S0.1** Tag `v1.0-thesis` once defense passes. *AC:* tag exists; CI green at that commit.
- **S0.2** Live-demo hardening already done (ForceProvider, clear key errors, Studio live toggle). *AC:* real Azure run renders end-to-end in the Studio.
- **S0.3** KC reproducibility evidence (see `docs/KC_REPRODUCIBILITY.md`). *AC:* `kc_live_summary.md` produced from a real run.
> Exit: prototype is a frozen, reproducible reference app. Migration starts on a `platform-v2` branch.

## M1 — `Core` + `IAgentRuntime` (foundation)
**Goal:** introduce the runtime seam with zero behavior change.
- **S1.1** Create `/src/Core` (ids, `Capability`, `RiskTier`, `Result`). *AC:* no dependencies; unit-tested value types.
- **S1.2** Define `/src/Runtime` contracts (`IAgentRuntime`, `AgentSession`, `AgentResult`, `IAgentRuntimeFactory`). *AC:* compiles; contract tests with an in-memory fake.
- **S1.3** `InProcessChatRuntime` wrapping today's `ILlmClient` path. *AC:* the existing Quality-Loop pipeline runs **through** the runtime; all 160 tests still pass.
- **S1.4** Orchestrator calls `IAgentRuntime` instead of agents-calling-`ILlmClient` directly. *AC:* no caller references a model client; behavior identical (golden-output test).
> Risk if skipped/reordered: every later pillar reattaches → highest-leverage phase.

## M2 — `Providers` registry (provider-agnostic, broaden)
**Goal:** model layer becomes a registry with capability descriptors; add self-host + aggregator.
- **S2.1** `IModelProvider` + `IModelProviderRegistry`; fold Claude/Azure/Mock behind it; `ILlmClient` becomes a facade. *AC:* parity tests vs current clients.
- **S2.2** Capability + data-residency descriptors per provider. *AC:* registry exposes them; a routing test picks SelfHosted for a `Secret`-classified request.
- **S2.3** Add `OllamaProvider` (self-host) + `OpenRouterProvider`. *AC:* live smoke per provider (gated, like `LivePipelineSmokeTests`).
- **S2.4** Cost-aware + policy-aware routing in `Resolve`. *AC:* unit tests for routing rules; `Llm:ForceProvider` maps onto registry.

## M3 — Tools + sandbox + capability + governance (security keystone — one phase)
**Goal:** agents *act*, safely. These ship together — never tools without the gate.
- **S3.1** `IToolbox`/`ITool`/`ToolDescriptor`; central invocation pipeline (capability→policy→approval→sandbox→evidence). *AC:* a tool cannot execute without passing all stages (test proves bypass impossible).
- **S3.2** MCP tool adapters: filesystem, git, `dotnet build`, `dotnet test`. *AC:* Coding agent runs `dotnet build` on generated code; QA reads real compiler output (evidence-driven).
- **S3.3** `ISandbox` (process isolation first; container later) with scoped workspace + net policy. *AC:* a tool cannot read outside its workspace or reach the network unless granted.
- **S3.4** `IPolicyEngine` + default policy pack (`/src/Policies`) + `IApprovalGate`. *AC:* an `Irreversible` action (e.g. push) suspends pending approval; approval/denial audited.
- **S3.5** Capability grants model (tenant/repo/branch/path scoped). *AC:* agent with only `fs.read`+`build.run` is denied `git.write`, audited.
> Security gate: M3 must pass a threat-model review before any non-`ReadOnly` tool is enabled in a real repo.

## M4 — Memory + evidence/lineage (persistent cognition + audit)
**Goal:** agents stop being amnesiac; the system becomes auditable/explainable.
- **S4.1** `IEvidenceSink` + evidence store + OTel correlation; emit on every model/tool/gate step. *AC:* a run produces a replayable evidence trail; redaction at write.
- **S4.2** `IExecutionLineage` (causal graph) + `IDecisionLog`. *AC:* Studio can render "why did QA fail → what changed next".
- **S4.3** `IEpisodicMemory` + `ISemanticMemory` (vector) with `Classification`. *AC:* a second run reuses prior episodic context; semantic search returns repo-relevant hits; `Secret` items blocked from external providers.
- **S4.4** `IDebuggingMemory`. *AC:* a known failure signature suggests a prior resolution.

## M5 — Durable workflow (long-horizon, resumable)
**Goal:** replace the in-process `for` loop with a checkpoint/replay engine.
- **S5.1** Select engine (spike: DurableTask vs Dapr Workflows vs Temporal .NET) → ADR. *AC:* decision recorded with criteria (ops cost, .NET fit, replay model).
- **S5.2** `IWorkflowEngine` + re-express the Quality Loop as a durable `IWorkflowDefinition`. *AC:* a run survives a host restart mid-loop and resumes; idempotent steps.
- **S5.3** Approvals as durable signals (`SignalAsync`). *AC:* a run suspended for approval resumes on signal after a deploy.
- **S5.4** Distributed execution: steps dispatched to workers. *AC:* two workers process one run's steps; observable.

## M6 — Operability & maturity
**Goal:** cloud-native topology + asset lifecycle + structure.
- **S6.1** `ISpecRegistry`: prompts/specs/agent-defs as versioned runtime resources (kill `static` prompts, W6). *AC:* change a prompt without recompiling; A/B two versions; audit the change.
- **S6.2** Control-plane / data-plane split (`/Hosts/ControlPlane` + `/Hosts/Worker`) over a bus. *AC:* scaling workers does not scale the control-plane; tool execution isolated to workers.
- **S6.3** `IKnowledgeGraph` repo ingestion + context API. *AC:* agents get graph-derived context; token cost per task drops vs raw-file dumping (measured).
- **S6.4** Capability-based folder restructure (the *result* of M1–M5). *AC:* modules own their boundaries; no god service; dependency tests enforce direction.
- **S6.5** New SDLC agents (Design, DevOps, Documentation, Review/Architecture-review) on the existing protocol. *AC:* each ships as an `AgentSpec` + governed tools; no orchestrator change.

---

## Cross-cutting backlog (every phase)
- **Testing:** each pillar ships an in-memory fake + contract tests; gated live smoke for runtimes/providers/tools.
- **Observability:** OTel spans + structured logs around every new seam; dashboards for cost/latency/failure per agent.
- **Security:** threat-model review at M3 and M6 (plane split); secret/PII redaction in evidence + audit.
- **Docs:** an ADR per pillar decision; update `TARGET_ARCHITECTURE.md` as reality diverges from design.

## Definition of Done (platform-grade, per story)
Code + tests (unit + contract) green · OTel instrumented · no secret leakage in logs/evidence · governance path
exercised for any new action · docs/ADR updated · demoable in the Studio or via an integration test.
