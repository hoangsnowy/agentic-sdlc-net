# ADR-0007: Durable, resumable workflow engine

- **Status:** Proposed (target M5; engine choice is a spike)
- **Related:** [CONTRACTS §Workflow](../architecture/CONTRACTS.md), W4; consumes ADR-0001, enables ADR-0008

## Context
`PipelineOrchestrator.RunAsync` is an in-process `for` loop. If the process dies mid-run, the run is lost; it
cannot resume, cannot be distributed across workers, and cannot span a deploy. Long-horizon agent workflows
(hours/days, many tool calls, human approvals) need durability, replay, and idempotency — the Temporal model.

## Decision
Introduce `IWorkflowEngine` with checkpoint/replay semantics; re-express the Quality Loop as a durable
`IWorkflowDefinition` whose context exposes replay-safe primitives (`CallAgentAsync` via `IAgentRuntime`,
`WaitForApprovalAsync`, `CheckpointAsync`, `ScheduleAsync`). Approvals (ADR-0005) become durable signals.
Engine selection is a spike with explicit criteria (ops cost, .NET fit, replay model, self-host): **Microsoft
DurableTask** vs **Dapr Workflows** vs **Temporal .NET** — recorded in a follow-up ADR.

## Consequences
- **Tradeoffs:** event-sourcing/state-machine complexity; steps must be idempotent + deterministic on replay; a new infra dependency.
- **Scaling:** runs span restarts/deploys; many concurrent runs with isolation; steps distribute to workers.
- **Security:** durable state carries artifacts → encrypted at rest, tenant-scoped; replay must not re-execute side effects (idempotency keys).
- **Enterprise:** SLA-grade reliability; "the agent worked 6 hours and resumed after a deploy" becomes real.

## Alternatives considered
- *Keep the in-process loop, add try/resume bookkeeping:* reinvents a workflow engine badly; no distribution. Rejected.
- *Queue + handlers only:* gives async but not replay/checkpoint or human-signal suspension. Insufficient.
