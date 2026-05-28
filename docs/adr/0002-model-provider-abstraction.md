# ADR-0002: Model/provider abstraction & registry

- **Status:** Proposed (target M2)
- **Related:** [CONTRACTS §Providers](../architecture/CONTRACTS.md), W5; depends on ADR-0001

## Context
`ILlmClient` + `LlmClientFactory` make the platform agnostic **only at the chat layer**, and only for three
providers. We must support Anthropic, Azure OpenAI, Ollama (self-host), OpenRouter, and route intelligently —
e.g. keep sensitive code on a self-hosted model, send tool-use tasks to a tool-capable model. The current
contract carries no capability or data-residency information, so routing/governance cannot reason about it.

## Decision
Introduce `IModelProvider` (chat/stream/embed) that **declares `ProviderCapabilities`** (tool-use, streaming,
JSON-mode, context window, embeddings) and **`DataResidency`** (CloudExternal / CloudRegionPinned / SelfHosted),
plus `IModelProviderRegistry` that resolves a provider from a `ModelPreference` honoring cost- and
policy-aware routing. `ILlmClient` becomes a thin facade during migration; the already-shipped
`Llm:ForceProvider` switch maps onto registry routing. No provider SDK type crosses the boundary.

## Consequences
- **Tradeoffs:** richer contract than `ILlmClient`; mitigated by a base adapter + facade for the legacy path.
- **Scaling:** per-provider pools, rate-limit/circuit-breaker, cost-aware selection; add providers via plugin assemblies.
- **Security:** data-residency becomes first-class → governance (ADR-0005) can forbid `Secret` data leaving the org.
- **Enterprise:** zero provider lock-in; procurement swaps vendors via config; self-host path for air-gapped/regulated customers.

## Alternatives considered
- *Add providers to the existing factory only:* easy for Ollama/OpenRouter but leaves "agnostic" untrue at the runtime layer and carries no capability metadata. Insufficient.
- *Adopt a third-party gateway (e.g. LiteLLM) as the only path:* useful but Python-centric and a dependency for a core seam; may wrap it behind `IModelProvider`, not expose it. Deferred.
