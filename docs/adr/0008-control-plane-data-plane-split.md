# ADR-0008: Control-plane / data-plane split

- **Status:** Proposed (target M6)
- **Related:** [TARGET_ARCHITECTURE §Topology](../architecture/TARGET_ARCHITECTURE.md), W9; consumes ADR-0001/0006/0007

## Context
Both hosts (`Api`, `Web`) run the engine **in-process**. Scaling the Web scales the engine with it; tool
execution (the riskiest code) runs in the same process as request handling and the UI. A cloud-native,
horizontally scalable, secure platform needs a thin trusted control-plane separated from scaled,
less-trusted execution workers.

## Decision
Split into a **control-plane** (API + Studio + workflow scheduling + governance/registries — accepts work,
queries state, approves gates; never runs arbitrary tools) and a **data-plane** of **execution workers**
(host `IAgentRuntime`; run model calls + sandboxed tools). They communicate via a command/event bus; the
control-plane reads run state from read models. Workers are stateless (durable state lives in the workflow
engine, ADR-0007) and scale horizontally.

## Consequences
- **Tradeoffs:** infra complexity (bus, worker host, deployment topology) vs the current single process; only worth it once tools/workflow exist (hence M6).
- **Scaling:** workers scale independently of the control-plane; tool-heavy workloads don't degrade the UI/API.
- **Security:** the blast-radius boundary — workers running model-authored tools are isolated from the trusted control-plane and from each other; least-privilege per worker.
- **Enterprise:** multi-tenant isolation, independent capacity planning, and a clean place to enforce per-tenant quotas and network policy.

## Alternatives considered
- *Stay in-process, scale the whole host:* couples UI/API scaling to execution; co-locates risky tool execution with trusted code. Rejected for production.
- *Serverless functions per step:* possible later for bursty tool steps; premature before the workflow/runtime seams stabilize.
