# ADR-0004: Execution evidence & tracing

- **Status:** Proposed (target M4)
- **Related:** [CONTRACTS §Evidence](../architecture/CONTRACTS.md), W8; extends the existing Aspire/OTel setup

## Context
Tracing today stops at the service layer (HTTP/DB spans) plus aggregate token/cost/latency metrics. Those
answer "how much" but not "what & why": the actual prompt sent, the raw model output, which tool ran with what
input/output, why QA failed, why a gate passed. "Evidence-driven automation" requires that every autonomous
step leave a reproducible artifact — otherwise autonomous actions are unauditable and undebuggable.

## Decision
Introduce `IEvidenceSink` + an append-only evidence store. Every model call, tool call, gate decision, QA
verdict, and workflow transition emits an `EvidenceRecord` (prompt, raw output, tool I/O, rationale,
attributes), correlated to an OpenTelemetry span via `run_id`/`agent_id`/`step_id`. Redaction runs at write.
Evidence is the substrate the Studio's lineage viewer and the audit log read from.

## Consequences
- **Tradeoffs:** evidence is heavy (full prompts/outputs) → retention tiers + sampling for low-risk steps required; storage cost.
- **Scaling:** append-only, partition by run/tenant, tiered retention (hot → cold).
- **Security:** records may contain secrets/PII → redaction at write, access-controlled reads, encryption at rest.
- **Enterprise:** foundation for incident debugging, audit, and post-hoc review of autonomous actions; satisfies "no hidden AI behavior / no non-auditable actions".

## Alternatives considered
- *Rely on structured logs only:* logs are lossy, unstructured for replay, and not correlated to a causal step graph. Insufficient.
- *Store evidence inside the workflow state:* bloats workflow checkpoints and couples retention to execution. Rejected — separate store, linked by id.
