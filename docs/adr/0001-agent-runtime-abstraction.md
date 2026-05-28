# ADR-0001: Introduce `IAgentRuntime` to separate orchestration from execution

- **Status:** Proposed (target M1)
- **Deciders:** Platform architecture
- **Related:** [TARGET_ARCHITECTURE §Runtime](../architecture/TARGET_ARCHITECTURE.md), [CONTRACTS §Runtime](../architecture/CONTRACTS.md), W1

## Context
Today an "agent" is `factory.Create(provider).SendAsync(request)` + JSON parse — one chat-completion call,
in-process. The runtimes we must support are not that shape: **Claude Code CLI** is a stateful, multi-step,
tool-using process; a **Semantic Kernel** agent runs a think→tool→observe loop; **Ollama** is a self-hosted
endpoint. There is no abstraction for "how an agent executes a turn"; orchestration is welded to single-shot
chat. Every other capability (tools, memory, evidence, governance) needs a place to attach.

## Decision
Introduce `IAgentRuntime` as the primary seam. A runtime executes an `AgentSession` (goal, history, budget,
capability-scoped toolbox, memory, evidence) and returns an `AgentResult`, optionally streaming `AgentStep`s.
Orchestration depends only on `IAgentRuntime`/`IAgentRuntimeFactory`, never on a model SDK. Ship three
adapters over time: `InProcessChatRuntime` (wraps today's path — compatibility), `SemanticKernelRuntime`
(tool loop), `ClaudeCodeCliRuntime` (out-of-process). Migration is behavior-preserving: the existing pipeline
runs *through* `InProcessChatRuntime` first.

## Consequences
- **Tradeoffs:** one indirection over a direct model call; the in-process adapter keeps the trivial case trivial. Net new concept the team must learn.
- **Scaling:** runtimes may be in-process, out-of-process, or remote/containerized with no orchestrator change → enables the data-plane worker model (ADR-0008).
- **Security:** the runtime boundary is the natural containment unit; a remote/sandboxed runtime isolates tool execution.
- **Enterprise:** new runtimes/vendors plug in without touching orchestration → low regression risk, no runtime lock-in.

## Alternatives considered
- *Keep `ILlmClient`, add methods for tool-use:* bloats the chat contract and still can't model a stateful CLI process. Rejected.
- *Per-runtime orchestrators:* duplicates orchestration; diverges. Rejected.
