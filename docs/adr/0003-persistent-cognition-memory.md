# ADR-0003: Persistent cognition (memory architecture)

- **Status:** Proposed (target M4)
- **Related:** [CONTRACTS §Memory](../architecture/CONTRACTS.md), W2; pairs with ADR-0004

## Context
Agents are stateless: each run starts from zero, learns nothing from prior runs, and accumulates no repo
understanding. Persistence today stores only run records + metrics. Long-horizon, multi-session work — and
auditability ("what did the agent decide, on what basis") — require a real memory substrate.

## Decision
Introduce a memory bounded context exposed to sessions via `ICognitionContext`, composed of five stores:
**episodic** (what happened), **semantic** (vector knowledge of domain/repo), **execution lineage** (causal
graph of steps/decisions), **decision log** (auto-ADR records), **debugging memory** (failure signatures →
resolutions). Memory items carry a `Classification` (Public/Internal/Secret) and `TenantId`, enforced on read.
This is a **separate** bounded context — not the pipeline `DbContext`.

## Consequences
- **Tradeoffs:** real operational surface (a vector store, retention) vs the simplicity of stateless runs; introduce per-store, not all at once.
- **Scaling:** split read model (semantic/vector) from write model (episodic/lineage); shard per tenant/repo.
- **Security:** memory holds code and possibly secrets → classification + access control + redaction; `Secret` items are gateable from external providers (ADR-0002/0005).
- **Enterprise:** lineage + decision log deliver audit + explainability — a procurement gate for regulated buyers; debugging memory compounds quality over time.

## Alternatives considered
- *Extend the existing persistence tables:* couples cognition to the pipeline schema, no classification, no vector model. Rejected.
- *Single "memory" blob store:* loses the distinct access patterns (vector search vs causal graph vs append log). Rejected.
