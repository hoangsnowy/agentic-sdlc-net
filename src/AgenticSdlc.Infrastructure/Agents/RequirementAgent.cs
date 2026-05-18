// AgenticSdlc.Infrastructure/Agents/RequirementAgent.cs
// Phase 4 — Impl IRequirementAgent. Gọi LLM với system prompt structured-output, parse JSON.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticSdlc.Application.Agents;
using AgenticSdlc.Domain;
using AgenticSdlc.Domain.Llm;
using AgenticSdlc.Domain.Pipeline;
using AgenticSdlc.Infrastructure.Llm;
using AgenticSdlc.Domain.Requirements;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSdlc.Infrastructure.Agents;

/// <summary>Phân tích user story → <see cref="RequirementSpec"/> dạng JSON structured.</summary>
public sealed class RequirementAgent : IRequirementAgent
{
    private const string AgentName = nameof(RequirementAgent);

    private const string SystemPrompt = """
        Bạn là Requirement Agent trong hệ thống Agentic SDLC.
        Nhiệm vụ: phân tích 1 user story tiếng Việt và trả về 1 specification JSON.

        Trả về CHỈ JSON (không markdown fence, không prose) theo schema:
        {
          "title": "Tiêu đề ngắn (≤ 80 ký tự)",
          "summary": "1-2 câu tóm tắt",
          "stakeholders": ["chuỗi"],
          "functionalRequirements": ["chuỗi"],
          "nonFunctionalRequirements": ["chuỗi"],
          "entities": [
            { "name": "PascalCase", "fields": ["fieldName: Type"], "notes": "tuỳ chọn" }
          ],
          "endpoints": [
            { "method": "GET|POST|PUT|DELETE|PATCH", "path": "/route", "purpose": "mô tả", "authRequired": false }
          ],
          "acceptanceCriteria": ["chuỗi"]
        }

        Quy tắc:
        - PHẢI có ≥ 1 entity, ≥ 1 endpoint, ≥ 3 acceptance criteria.
        - Tiếng Việt cho mọi field text trừ name của entity (PascalCase tiếng Anh) và HTTP method.
        - KHÔNG include comment, KHÔNG có trailing comma.
        """;

    private readonly ILlmClient _llm;
    private readonly AgentOptions _options;
    private readonly ILogger<RequirementAgent> _logger;

    /// <summary>Khởi tạo với factory + options.</summary>
    public RequirementAgent(ILlmClientFactory factory, IOptions<AgentsOptions> options, ILogger<RequirementAgent> logger)
    {
        System.ArgumentNullException.ThrowIfNull(factory);
        System.ArgumentNullException.ThrowIfNull(options);
        _options = options.Value.Requirement;
        _llm = factory.Create(_options.Provider);
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<RequirementSpec> RunAsync(UserStory story, CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(story);
        story.Validate();

        var userPrompt = $"""
            User story (locale {story.Locale}):
            {story.Description}

            Sinh requirement specification dưới dạng JSON đúng schema.
            """;

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: userPrompt,
            Model: _options.Model,
            Temperature: _options.Temperature,
            MaxTokens: _options.MaxTokens);

        var response = await _llm.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var dto = JsonExtractor.Deserialize<RequirementSpecDto>(response.Content, AgentName);
        dto.Validate(AgentName);

        var metrics = MetricsMapper.From(response);

        _logger.LogInformation(
            "{Agent} done: {InTok}→{OutTok} tokens, ${Cost} USD, {Ms}ms ({Provider} {Model})",
            AgentName, metrics.InputTokens, metrics.OutputTokens, metrics.CostUsd,
            metrics.Latency.TotalMilliseconds, metrics.Provider, metrics.Model);

        return Map(dto, metrics);
    }

    private static RequirementSpec Map(RequirementSpecDto dto, AgentMetrics metrics)
        => new(
            Title: dto.Title!,
            Summary: dto.Summary!,
            Stakeholders: dto.Stakeholders ?? [],
            FunctionalRequirements: dto.FunctionalRequirements ?? [],
            NonFunctionalRequirements: dto.NonFunctionalRequirements ?? [],
            Entities: (dto.Entities ?? []).Select(e => new EntityDescriptor(e.Name!, e.Fields ?? [], e.Notes)).ToArray(),
            Endpoints: (dto.Endpoints ?? []).Select(e => new EndpointDescriptor(e.Method!, e.Path!, e.Purpose!, e.AuthRequired)).ToArray(),
            AcceptanceCriteria: dto.AcceptanceCriteria ?? [],
            Metrics: metrics);

    // ---- DTOs cho JSON deserialize (khớp schema trong SystemPrompt) ----
    private sealed class RequirementSpecDto
    {
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public List<string>? Stakeholders { get; set; }
        public List<string>? FunctionalRequirements { get; set; }
        public List<string>? NonFunctionalRequirements { get; set; }
        public List<EntityDto>? Entities { get; set; }
        public List<EndpointDto>? Endpoints { get; set; }
        public List<string>? AcceptanceCriteria { get; set; }

        public void Validate(string agentName)
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                throw new LlmException($"{agentName}: missing 'title'.", agentName);
            }
            if (string.IsNullOrWhiteSpace(Summary))
            {
                throw new LlmException($"{agentName}: missing 'summary'.", agentName);
            }
            if (Entities is null || Entities.Count == 0)
            {
                throw new LlmException($"{agentName}: 'entities' must have ≥ 1 item.", agentName);
            }
            if (Endpoints is null || Endpoints.Count == 0)
            {
                throw new LlmException($"{agentName}: 'endpoints' must have ≥ 1 item.", agentName);
            }
        }
    }

    private sealed class EntityDto
    {
        public string? Name { get; set; }
        public List<string>? Fields { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class EndpointDto
    {
        public string? Method { get; set; }
        public string? Path { get; set; }
        public string? Purpose { get; set; }
        public bool AuthRequired { get; set; }
    }
}
