# Governance Model

> How autonomous actions are authorized, gated, approved, and audited. Governance is a **mechanism**, not a
> guideline — it is the difference between a demo and a system an enterprise will run on its own repositories.
> Contracts: [CONTRACTS.md §Governance](CONTRACTS.md). Decision: [ADR-0005](../adr/0005-governance-policy-and-approval-gates.md).

## 1. Why governance is a pillar, not a feature

An agent that can write files, run shell commands, push branches, or open PRs is, from a security standpoint,
**a program executing model-authored instructions against your assets**. The thesis prototype gestures at this
with a 3-iteration QA cap and a "Human-in-the-Loop" principle, but there is no enforcement point. For a
platform, every side-effecting action must pass through one auditable gate. Without it: no regulated buyer can
adopt it, and no incident is explainable after the fact.

## 2. The authorization chain (single enforcement path)

Every tool/action invocation flows through the **same** pipeline (enforced centrally in `IToolbox.Invoke`, not
per-tool — so a tool author cannot bypass it):

```
request
  → 1. Capability check     (does this agent hold the required Capability grant?)
  → 2. Policy evaluation     (IPolicyEngine: Allow | Deny | RequireApproval, given risk tier + facts)
  → 3. Approval gate         (if RequireApproval: block on a durable human decision)
  → 4. Sandbox               (execute inside an isolated, scoped workspace)
  → 5. Evidence + audit       (immutable record of action, decision, approver, result)
```

Deny short-circuits with an auditable reason. Each stage emits an `EvidenceRecord` (`GateDecision`).

## 3. Capabilities (least privilege)

Agents are granted explicit `Capability` values (`fs.read`, `fs.write`, `git.read`, `git.write`, `shell.exec`,
`build.run`, `net.http`, …). A grant is scoped to a **tenant + repo + branch + path-glob** and a **time
window**. The default grant set is empty — an agent can do nothing until granted.

- **Why:** least privilege limits blast radius; "this agent may read the repo and run tests, but may not push to `main` or reach the network".
- **Scaling:** grants are data, evaluated statelessly and cached; no code change to add a constraint.
- **Security:** capability ≠ identity — even a compromised agent prompt cannot exceed its grants.
- **Enterprise:** maps cleanly to existing RBAC and to per-customer policies.

## 4. Risk tiers → default gating

Every action and tool declares a `RiskTier`; policy packs map tiers to default handling:

| Tier | Examples | Default | Rationale |
|---|---|---|---|
| `ReadOnly` | read file, query KG, run tests in sandbox | **auto-allow** | no external effect |
| `Reversible` | write to a scratch branch, create draft PR | **auto-allow + audit** | undoable, contained |
| `Irreversible` | push to a shared branch, merge PR, post comment | **require approval** | visible to others / hard to undo |
| `Destructive` | force-push, delete branch/data, change infra/config | **require approval + 2nd reviewer** | catastrophic if wrong |

Tiers are policy-overridable per tenant (a team may auto-allow draft PRs but always gate merges).

## 5. Policy engine

`IPolicyEngine.EvaluateAsync(ActionContext)` returns `Allow` / `Deny(reason)` / `RequireApproval(reason, scope)`.

- Policies are **versioned, reviewable assets** (a policy pack in `/src/Policies`), not code branches — changed via PR + audited.
- Evaluation is **stateless and deterministic** given `(action, facts, policy version)` → cacheable, testable, replayable.
- Facts include: risk tier, capability, target (branch/path), provider data-residency, current run budget, time, tenant.
- **Data-residency policy:** a policy can forbid sending classified memory/code to a `CloudExternal` provider — routing it to a `SelfHosted` provider or denying. (Ties Governance ↔ Providers.)

## 6. Approval gates (human-in-the-loop, as a mechanism)

When policy returns `RequireApproval`, the workflow **suspends durably** (it is a workflow signal — survives
restarts; the agent is not blocking a thread). An `ApprovalRequest` carries: the action, the evidence so far,
a diff/preview, the requesting agent, and the policy reason. A human approves/denies in the Studio (or via an
integration: GitHub review, Slack). The decision + approver identity + timestamp are recorded immutably.

- **Why:** humans stay in control at exactly the risky moments, without babysitting low-risk steps.
- **Tradeoffs:** latency on gated actions — bounded by risk tiering (most steps auto-allow).
- **Scaling:** approvals are durable signals, not held connections → unlimited concurrent suspended runs.
- **Enterprise:** segregation of duties, named-approver audit trail, SLA timers + escalation on pending approvals.

## 7. Auditability

- **Immutable audit log:** every gate decision, approval, and side-effecting action is append-only, correlated by `run_id`/`step_id` to the evidence + lineage stores.
- **Replayable:** given the evidence + policy version, any decision can be reconstructed ("why did the agent push to this branch, who approved, on what evidence").
- **Redaction:** audit/evidence may contain secrets → redaction at write + access-controlled reads + retention policy.
- **Tamper-evidence:** audit entries are hash-chained per run (detect mutation).

## 8. Defaults shipped (`/src/Policies`)

A conservative default pack so the platform is safe out of the box:

- All `Irreversible`/`Destructive` actions require approval.
- Network egress (`net.http`) denied unless explicitly granted.
- Classified=`Secret` memory may not be sent to `CloudExternal` providers.
- Per-run budget caps (tokens/cost/wall-clock); exceeding → suspend for approval.
- Every agent starts with **zero** capabilities; grants are explicit and scoped.

Tenants tune from this baseline via reviewed policy-pack overrides — they relax deliberately, never silently.
