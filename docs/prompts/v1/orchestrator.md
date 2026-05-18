# OrchestratorPrompt — v1

Version: **v1** · Source: `src/AgenticSdlc.Application/Prompts/OrchestratorPrompt.cs`

Orchestrator hiện tại là deterministic state machine (KHÔNG gọi LLM). File này chỉ ghi policy text dùng cho documentation + dự phòng nếu sau này có một "meta-orchestrator" do LLM điều khiển.

## Policy

```text
Orchestrator policy (deterministic, không gọi LLM):

1. RequirementAgent(story) → spec.
2. Vòng lặp i = 1..NMax:
   a. CodingAgent(spec, qaFeedback[i-1]?) → code.
   b. TestingAgent(spec, code, qaFeedback[i-1]?) → tests.
   c. QaAgent(spec, code, tests) → qaReport.
   d. Nếu qaReport.isConsistent → trả PipelineResult(Done, iterations=i).
   e. Nếu i = NMax → trả PipelineResult(MaxIterationReached).
3. Nếu bất kỳ agent ném LlmException → trả PipelineResult(Failed) với agent + lỗi.

Bất kỳ exception khác phải propagate (không nuốt).
Metric aggregate: tổng InputTokens, OutputTokens, CostUsd, Latency của tất cả agent call.
```

## Changelog
- **v1** (2026-05-18): khởi tạo file để snapshot policy.
