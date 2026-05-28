# ADR-0006: Tool sandboxing & capability-based permissions

- **Status:** Proposed (target M3, with ADR-0005)
- **Related:** [CONTRACTS §Execution](../architecture/CONTRACTS.md), [GOVERNANCE.md](../architecture/GOVERNANCE.md), W3

## Context
Autonomous coding / CI-CD / repo-understanding agents are inherently **tool-driven**: they must read/write
files, run `git`, `dotnet build`/`test`, etc. Without tools, agents only *guess* code; with tools, they
*verify* it (the root of evidence-driven automation, ADR-0004). But a tool-running agent is the platform's
largest attack surface — model-authored instructions executing against real assets.

## Decision
Introduce `IToolbox`/`ITool` with a **central invocation pipeline** that no tool can bypass: capability check
(does the agent hold the required `Capability` grant, scoped to tenant/repo/branch/path/time?) → policy
(ADR-0005) → approval if required → `ISandbox` (process isolation first, container later; scoped workspace +
network policy) → execute → emit evidence. Tools are surfaced via **MCP** where possible. Agents start with
**zero** capabilities; grants are explicit, scoped, and audited. M3 ships tools + sandbox + governance
together — never tools without the gate.

## Consequences
- **Tradeoffs:** significant new surface + sandbox ops complexity; non-negotiable for safety.
- **Scaling:** tool execution is the hottest/riskiest path → isolated to data-plane workers (ADR-0008), quota per capability.
- **Security:** **P0**. Capability ≠ identity: a compromised prompt cannot exceed its grants; sandbox bounds blast radius; threat-model review gates enabling non-ReadOnly tools on real repos.
- **Enterprise:** lets an org express "agents may read + run tests, may not push to main or reach the network" as policy + grants.

## Alternatives considered
- *Trust the model + run tools directly:* unacceptable risk; no containment. Rejected.
- *Per-tool ad-hoc permission checks:* bypassable, inconsistent. Rejected — central pipeline only.
- *Reuse OS users/chroot instead of containers:* acceptable for v1 process isolation; containers/microVMs for stronger isolation later.
