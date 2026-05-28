# ADR-0005: Governance — policy engine & approval gates

- **Status:** Proposed (target M3, with ADR-0006)
- **Related:** [GOVERNANCE.md](../architecture/GOVERNANCE.md), [CONTRACTS §Governance](../architecture/CONTRACTS.md), W7

## Context
"Human-in-the-Loop" exists today only as a principle (a 3-iteration QA cap). There is no enforcement point
before a risky action. A platform whose agents can write files, push branches, open PRs, or run commands must
gate side-effecting actions through one auditable mechanism — otherwise it cannot be adopted by any
compliance-bound organization and no autonomous action is explainable afterward.

## Decision
Introduce `IPolicyEngine` (Allow / Deny / RequireApproval given an `ActionContext` with risk tier + facts) and
`IApprovalGate` (durable human decision). Every side-effecting action passes a single enforcement chain:
**capability → policy → approval → sandbox → evidence/audit** (enforced centrally so tools can't bypass it).
Actions/tools declare a `RiskTier`; a shipped default policy pack maps tiers to handling (ReadOnly/Reversible →
auto; Irreversible/Destructive → approval). Approvals are durable workflow signals (survive restarts).

## Consequences
- **Tradeoffs:** latency on gated actions — bounded by risk tiering so most steps auto-allow; policy authoring overhead.
- **Scaling:** policy evaluation is stateless/cacheable; approvals are signals, not held connections → unlimited suspended runs.
- **Security:** the enforcement point for least privilege + irreversibility control + data-residency rules.
- **Enterprise:** segregation of duties, named-approver immutable audit, SLA/escalation on pending approvals — the demo→sellable boundary.

## Alternatives considered
- *Per-action ad-hoc checks in code:* unauditable, bypassable, inconsistent. Rejected.
- *Approvals as blocking calls:* doesn't survive restarts, caps concurrency. Rejected in favor of durable signals (ADR-0007).
