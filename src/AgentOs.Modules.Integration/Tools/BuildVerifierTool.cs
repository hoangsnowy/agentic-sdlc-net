// Epic E2 — First concrete ITool. Wraps IBuildVerifier so an LLM agent can compile a candidate
// file set during a run ("does this code build?") without the orchestrator having to thread the
// BuildVerifier dependency through every agent. JSON contract is intentionally tight: a flat
// {files:[{path,content}]} payload — same shape the Coding/Testing agents already produce.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AgentOs.Domain.Tools;

namespace AgentOs.Modules.Integration.Tools;

/// <summary>ITool wrapper around <see cref="IBuildVerifier"/>.</summary>
public sealed class BuildVerifierTool : ITool
{
    private const string Schema = """
        {
          "type": "object",
          "properties": {
            "files": {
              "type": "array",
              "description": "Source files to write to a temp dir before running `dotnet build`.",
              "items": {
                "type": "object",
                "properties": {
                  "path": { "type": "string", "description": "Relative path, e.g. \"src/Domain/Product.cs\"." },
                  "content": { "type": "string", "description": "File content (UTF-8 text)." }
                },
                "required": ["path", "content"]
              },
              "minItems": 1
            }
          },
          "required": ["files"]
        }
        """;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IBuildVerifier _verifier;

    public BuildVerifierTool(IBuildVerifier verifier)
    {
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
    }

    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        Name: "build_verifier",
        Description: "Writes a set of generated C# source files to a temp workspace and runs `dotnet build`. "
            + "Returns the build's exit code, captured output (stdout+stderr, truncated to 8 KB) and wall-clock duration. "
            + "Use this to confirm code the model just wrote actually compiles before reporting it as done.",
        JsonInputSchema: Schema);

    /// <inheritdoc />
    public async Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        BuildVerifierInput? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<BuildVerifierInput>(request.Input, SerializerOptions);
        }
        catch (JsonException ex)
        {
            return ToolInvocationResult.Error(request.CallId, $"Input is not valid JSON for build_verifier: {ex.Message}");
        }

        if (parsed?.Files is null || parsed.Files.Count == 0)
        {
            return ToolInvocationResult.Error(request.CallId, "Input must include a non-empty 'files' array.");
        }

        var files = parsed.Files
            .Where(f => !string.IsNullOrWhiteSpace(f.Path))
            .Select(f => new BuildVerifyFile(f.Path, f.Content ?? string.Empty))
            .ToList();
        if (files.Count == 0)
        {
            return ToolInvocationResult.Error(request.CallId, "No files had a non-empty path.");
        }

        var verify = await _verifier.VerifyFilesAsync(files, cancellationToken).ConfigureAwait(false);

        var output = JsonSerializer.Serialize(new BuildVerifierOutput(
            Success: verify.Success,
            ExitCode: verify.ExitCode,
            Output: verify.Output,
            ElapsedMs: verify.ElapsedMilliseconds),
            SerializerOptions);

        return verify.Success
            ? ToolInvocationResult.Success(request.CallId, output)
            : ToolInvocationResult.Error(request.CallId, output);
    }

    internal sealed record BuildVerifierInput(
        [property: JsonPropertyName("files")] IReadOnlyList<BuildVerifierInputFile>? Files);

    internal sealed record BuildVerifierInputFile(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("content")] string Content);

    internal sealed record BuildVerifierOutput(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("exit_code")] int ExitCode,
        [property: JsonPropertyName("output")] string Output,
        [property: JsonPropertyName("elapsed_ms")] long ElapsedMs);
}
