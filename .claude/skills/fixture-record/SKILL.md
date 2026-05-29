---
name: fixture-record
description: >
  Record a real LLM response (Claude / Azure OpenAI / MAF) into a JSON fixture for AgentOs
  MockLlmClient. Compute SHA-256 hash of (model + system + user prompt), save to
  tests/fixtures/llm/<hash>.json. Enables offline tests + CI without API keys. Use when user
  says "record fixture for X", "save mock response", "/fixture-record".
---

Record one real LLM response into a deterministic fixture for `MockLlmClient`.

## When

- A new test calling `MockLlmClient` fails on hash miss → fixture needed.
- Freezing a response for a deterministic demo.
- Snapshotting output of a prompt to compare drift after a model upgrade.

## Input

1. **System prompt** (string, may be empty).
2. **User prompt** (string, required).
3. **Model** (e.g. `claude-sonnet-4-20250514`, `gpt-4.1`).
4. **Provider**: `Claude` | `AzureOpenAI`.
5. **Temperature, MaxTokens** (optional, default `0.0` / `4096`).

If user references a specific test file, read it → extract the prompt directly.

## Steps

### 1. Compute hash

Logic mirrors `MockLlmClient.ComputeHash` (in `src/AgentOs.Modules.Llm/MockLlmClient.cs`):

- Input: `$"{Model}\n---\n{SystemPrompt}\n---\n{UserPrompt}"`
- SHA-256 → first 8 bytes → lowercase hex (16 chars).

Quickest verify: write a one-shot test under `tests/AgentOs.Tests/Llm/FixtureHashScratch.cs`, run it, capture stdout, delete the file.

### 2. Check existing fixture

```bash
ls tests/fixtures/llm/<hash>.json 2>&1
```

If present, ask before overwriting — fixtures are a baseline; silent overwrites cause silent test drift.

### 3. Call the real API

Need user-secrets:
```bash
cd src/AgentOs.Api
dotnet user-secrets list | grep -i apikey
```

If empty → point user to `docs/SETUP.md`.

Scratch test `tests/AgentOs.Tests/Llm/Scratch_RecordFixture.cs`:

```csharp
using AgentOs.Domain.Llm;
using AgentOs.Modules.AppConfig;
using AgentOs.Modules.Llm;
using AgentOs.SharedKernel.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgentOs.Tests.Llm;

[Trait("Category", "Live")]
public class Scratch_RecordFixture
{
    [Fact(Skip = "Manual: drop Skip to record, then restore.")]
    public async Task Record()
    {
        var config = new ConfigurationBuilder().AddUserSecrets<Scratch_RecordFixture>().Build();

        var services = new ServiceCollection()
            .AddLogging(b => b.AddConsole())
            .AddDataProtection()
            .AddModulesFromAssemblies(config,
                typeof(AppConfigModule).Assembly,
                typeof(LlmModule).Assembly)
            .BuildServiceProvider();

        var client = services.GetRequiredService<ILlmClientFactory>().Create("{Provider}");

        var request = new LlmRequest(
            SystemPrompt: "{System}",
            UserPrompt: "{User}",
            Model: "{Model}",
            Temperature: {Temp},
            MaxTokens: {Max});

        var response = await client.SendAsync(request);

        var fixture = new { content = response.Content, inputTokens = response.InputTokens, outputTokens = response.OutputTokens };
        var json = System.Text.Json.JsonSerializer.Serialize(fixture, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var hash = MockLlmClient.ComputeHash(request);
        var path = System.IO.Path.Combine("tests", "fixtures", "llm", $"{hash}.json");
        await System.IO.File.WriteAllTextAsync(path, json);
        Console.WriteLine($"Wrote: {path}");
    }
}
```

Run:
```bash
dotnet test --filter "FullyQualifiedName~Scratch_RecordFixture"
```

### 4. Verify fixture

```bash
cat tests/fixtures/llm/<hash>.json
```

Required shape (camelCase — `MockLlmClient` uses `JsonNamingPolicy.CamelCase`):
```json
{
  "content": "...",
  "inputTokens": 123,
  "outputTokens": 456
}
```

### 5. Delete scratch file

Never commit `Scratch_RecordFixture.cs` — it hits real APIs and CI has no keys.

### 6. Commit fixture

```bash
git add tests/fixtures/llm/<hash>.json
git commit -m "test(fixtures): record <agent> <scenario> fixture (<model>)"
```

## Safety

- Never commit the scratch test (CI would fail on missing keys).
- Never record fixtures for prompts containing PII / secrets (emails, internal URLs).
- Warn if estimated cost > $0.05 (use `CostCalculator.Calculate` to pre-estimate).
- Each model alias change rotates the hash → old fixture goes stale; re-record or keep both.

## Out of scope

- Replay (handled by `MockLlmClient`).
- Batch record across many prompts (handle case-by-case).
- Fixture diff before/after (`prompt-tune` skill territory).
