// AgenticSdlc.Application/Prompts/OrchestratorPrompt.cs
// Sprint 3 — Orchestrator KHÔNG gọi LLM trực tiếp (deterministic state machine).
// File này lưu policy text dùng cho documentation + nếu sau này muốn cho 1 LLM "meta-orchestrator" hoạt động.

namespace AgenticSdlc.Application.Prompts;

/// <summary>Meta-policy của orchestrator (không gọi LLM hiện tại).</summary>
public static class OrchestratorPrompt
{
    /// <summary>Prompt version.</summary>
    public const string Version = "v1";

    /// <summary>Policy mô tả luồng KC4 (Mục 2.4 luận văn).</summary>
    public const string Policy = """
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
        """;
}
