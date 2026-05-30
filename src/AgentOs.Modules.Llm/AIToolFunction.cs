// Epic E2 — Adapter that exposes an Domain.Tools.ITool as a Microsoft.Extensions.AI AIFunction so
// FunctionInvokingChatClient can route LLM-emitted tool_use blocks back into the registered ITool.
// The AIFunction surface (Name, Description, JsonSchema) comes straight from ToolDefinition; the
// InvokeCoreAsync override serializes the model-emitted arguments back into the ITool's string
// Input, runs InvokeAsync, and returns the textual Output for the next LLM turn.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentOs.Domain.Tools;
using Microsoft.Extensions.AI;

namespace AgentOs.Modules.Llm;

internal sealed class AIToolFunction : AIFunction
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ITool _tool;
    private readonly string _tenantId;
    private readonly string? _runId;
    private readonly JsonElement _schema;

    public AIToolFunction(ITool tool, string tenantId, string? runId)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tool = tool;
        _tenantId = string.IsNullOrWhiteSpace(tenantId) ? "anonymous" : tenantId;
        _runId = runId;

        var def = tool.Definition;
        def.Validate();
        // ToolDefinition stores schema as a string for transport; parse it once at construction.
        _schema = JsonDocument.Parse(def.JsonInputSchema).RootElement.Clone();
    }

    public override string Name => _tool.Definition.Name;
    public override string Description => _tool.Definition.Description;
    public override JsonElement JsonSchema => _schema;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var inputDict = new Dictionary<string, object?>(arguments.Count, StringComparer.Ordinal);
        foreach (var kvp in arguments)
        {
            inputDict[kvp.Key] = kvp.Value;
        }

        var inputJson = JsonSerializer.Serialize(inputDict, SerializerOptions);
        var request = new ToolInvocationRequest(
            ToolName: _tool.Definition.Name,
            CallId: Guid.NewGuid().ToString("N"),
            Input: inputJson,
            TenantId: _tenantId,
            RunId: _runId);

        var result = await _tool.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            // Surface tool-side errors as the result string; FunctionInvokingChatClient feeds this
            // back as the tool_result content so the LLM can react (retry, give up, ask user).
            return string.IsNullOrEmpty(result.ErrorMessage)
                ? $"Tool '{_tool.Definition.Name}' returned an error."
                : result.ErrorMessage;
        }

        return result.Output;
    }
}
