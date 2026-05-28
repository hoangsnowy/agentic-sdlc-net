# Platform Architecture — Agentic SDLC (.NET)

Design package for evolving this repository from a thesis prototype into a **production-grade, enterprise
agentic SDLC platform** — in the spirit of "Kubernetes for agents" / "Temporal for AI workflows": runtime
abstraction first, provider-agnostic, governed, evidence-driven, security-first, long-horizon.

> **Scope discipline.** These are **design artifacts**, not a refactor. The running prototype is untouched and
> is tagged `v1.0-thesis` before any migration. Contracts here become compiling code in migration **Phase 1**
> (post-defense). We do not create empty capability folders up front — structure is the *result* of the phases.

## Read in this order

1. **[TARGET_ARCHITECTURE.md](TARGET_ARCHITECTURE.md)** — current-state review, weaknesses (W1–W10), principles,
   the layered target platform (control-plane/data-plane + pillars, each with Why/Tradeoffs/Scaling/Security/
   Enterprise), target folder structure, NFR mapping, and the explicit anti-pattern avoid-list.
2. **[CONTRACTS.md](CONTRACTS.md)** — C# interface sketches for every seam (`IAgentRuntime`, `IModelProvider`,
   memory, evidence, governance, tools/sandbox, workflow, agents, knowledge graph) + three runtime adapter examples.
3. **[GOVERNANCE.md](GOVERNANCE.md)** — the authorization chain, capabilities, risk tiers, policy engine,
   approval gates, auditability, shipped default policy pack.
4. **[MIGRATION_BACKLOG.md](MIGRATION_BACKLOG.md)** — dependency-ordered phases (M0–M6) as epics/stories with
   acceptance criteria and a platform-grade Definition of Done.
5. **[ADRs](../adr/)** — the pivotal decisions, with consequences across the four enterprise lenses.

## Architecture decision records

| ADR | Decision | Phase | Fixes |
|---|---|---|---|
| [0001](../adr/0001-agent-runtime-abstraction.md) | `IAgentRuntime` — separate orchestration from execution | M1 | W1 |
| [0002](../adr/0002-model-provider-abstraction.md) | Model/provider abstraction & registry | M2 | W5 |
| [0003](../adr/0003-persistent-cognition-memory.md) | Persistent cognition (memory architecture) | M4 | W2 |
| [0004](../adr/0004-execution-evidence-and-tracing.md) | Execution evidence & tracing | M4 | W8 |
| [0005](../adr/0005-governance-policy-and-approval-gates.md) | Governance — policy engine & approval gates | M3 | W7 |
| [0006](../adr/0006-tool-sandboxing-and-capabilities.md) | Tool sandboxing & capability permissions | M3 | W3 |
| [0007](../adr/0007-durable-resumable-workflow.md) | Durable, resumable workflow engine | M5 | W4 |
| [0008](../adr/0008-control-plane-data-plane-split.md) | Control-plane / data-plane split | M6 | W9 |

## Pillars → contracts → ADRs (at a glance)

| Pillar | Key contract(s) | ADR |
|---|---|---|
| Runtime | `IAgentRuntime`, `AgentSession` | 0001 |
| Providers | `IModelProvider`, `IModelProviderRegistry` | 0002 |
| Memory / Cognition | `ICognitionContext` (episodic/semantic/lineage/decision/debug) | 0003 |
| Evidence / Tracing | `IEvidenceSink`, `EvidenceRecord` | 0004 |
| Governance | `IPolicyEngine`, `IApprovalGate` | 0005 |
| Execution / Tools | `IToolbox`, `ITool`, `ICapability`, `ISandbox` | 0006 |
| Workflow | `IWorkflowEngine`, `IWorkflowDefinition` | 0007 |
| Topology | control-plane / data-plane | 0008 |
| Agents | `IAgent`, `AgentSpec` | — (declarative; rides 0001) |
| Knowledge graph | `IKnowledgeGraph` | — (M6) |
| Spec-driven | `ISpecRegistry` | — (M6, W6) |

## Relationship to other docs
- **[../ROADMAP_PLATFORM_V2.md](../ROADMAP_PLATFORM_V2.md)** — the higher-level horizon roadmap + OSS direction this package expands.
- **W1–W10 review** (`Platform_Architecture_Review_PostThesis.md`, thesis workspace) — the source weakness analysis these decisions resolve.
- **Prototype docs** (`docs/SETUP.md`, `docs/KC_REPRODUCIBILITY.md`) — the current system this migrates from.

## Not in scope here (deliberately)
- Engine selection for ADR-0007 (DurableTask vs Dapr vs Temporal) — a spike + follow-up ADR in M5.
- Concrete tenancy/billing model, UI design for the Studio control-plane, and the knowledge-graph storage choice — sequenced into M6.
- Any change to the prototype before `v1.0-thesis`.
