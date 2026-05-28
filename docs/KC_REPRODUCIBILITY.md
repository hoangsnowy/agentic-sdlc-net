# KC1–KC5 reproducibility runbook (Table 2.6 / 2.7)

> **Goal:** make the thesis's experimental numbers reproducible and defensible. Today the committed KC
> harness runs on `MockLlmClient` (deterministic, 123 ms) and **cannot** regenerate the real-LLM figures in
> Table 2.6. Before the defense, either run a live `n=10` benchmark in the true hybrid config and compare,
> **or** preserve the raw artifacts/logs from the real runs that produced those numbers.
>
> Companion docs: [RUN_LIVE_PIPELINE.md](RUN_LIVE_PIPELINE.md) · [comparison/kc_v1_vs_thesis_table_2_6.md](comparison/kc_v1_vs_thesis_table_2_6.md)

## What the thesis claims (targets to reproduce)

**Table 2.6 — KC results (mean of n=10):**

| KC | Agent / model | Completion | Quality | Avg latency |
|---|---|---|---|---|
| KC1 Requirement | Claude Sonnet 4 | 10/10 | AC coverage 92%, schema 100% | 3.4 s |
| KC2 Coding | GPT-4.1 (Azure) | 10/10 | compile-pass 9/10, fidelity 95% | 5.2 s |
| KC3 Testing | GPT-4o-mini | 10/10 | tests-runnable 87%, 3 groups 10/10 | 3.1 s |
| KC4 E2E | mixed (Sonnet + GPT-4.1 + GPT-4o-mini + Haiku) | 9/10 | req-code-test consistency 90% | 15.0 s |
| KC5 Quality Loop | Claude Haiku 4.5 | 10/10 PASS | avg 1.8 iters, +18 pts/iter, recovery 100% | 19.7 s |

**Table 2.7 — manual vs agentic** (sample problem = Product REST API CRUD; baseline from 5 devs, 2–5 yrs .NET):
total 232 min → ~30 min (~87% reduction); artifact quality 7.2 → 8.1 (+0.9/10).

## The gap (why the current harness can't reproduce these)

`LivePipelineSmokeTests` exists and runs a real pipeline, but it:
- runs **once** (not `n=10`); uses **one provider for all agents** (not the per-agent hybrid);
- forces **NMax=1** (so no QA-loop, no 1.8-iteration figure);
- does **not** compile the generated code, run the generated tests, measure AC coverage, or track per-iteration QA score deltas.

So four metrics in Table 2.6 are currently unmeasured: AC coverage % (KC1), compile-pass % (KC2),
tests-runnable % (KC3), score-improvement/iter (KC5). See the comparison doc for the line-by-line delta.

## Path decision

| Path | When | Recommendation |
|---|---|---|
| **A — run live `n=10`** | a budget + keys are available | **Recommended.** Most accurate, most convincing to the committee. |
| **Preserve** | the real runs were already done elsewhere | Locate + commit the raw transcripts/logs that produced Table 2.6, so the numbers are traceable. |
| **B — revise Table 2.6 to "mock baseline"** | no key/budget at all | Last resort — weakens the thesis argument. Not recommended. |

---

## Path A — step by step

### 1. Provide keys (PowerShell; do NOT commit)
```powershell
$env:RUN_LIVE_LLM       = "1"
$env:ANTHROPIC_API_KEY  = "sk-ant-..."
$env:AZURE_OPENAI_API_KEY = "..."
$env:AZURE_OPENAI_ENDPOINT = "https://<resource>.openai.azure.com"
```

### 2. Run the built harness
The runner exists: `tests/AgenticSdlc.Tests/Smoke/KcLiveBenchTests.cs` (gated by `RUN_LIVE_LLM`, never in CI).
It runs the **full hybrid pipeline `n` times** and derives KC1–KC5 from the per-agent metric rows + each
`PipelineResult` — one pipeline run feeds every KC (RequirementAgent→KC1, CodingAgent→KC2, TestingAgent→KC3,
whole run→KC4, QA loop→KC5).

```powershell
$env:KC_LIVE_MODE    = "hybrid"   # hybrid | azure | claude
$env:KC_LIVE_N       = "10"       # runs (default 10)
$env:KC_LIVE_MAX_USD = "20"       # hard budget guard — stops the run if exceeded
$env:KC_LIVE_TEMP    = "0.2"      # > 0 so the QA loop can vary
$env:KC_LIVE_BUILD   = "1"        # opt-in: compile-pass (KC2) + tests-build (KC3) via `dotnet build`
dotnet test tests/AgenticSdlc.Tests/AgenticSdlc.Tests.csproj --filter "FullyQualifiedName~KcLiveBenchTests"
```

Model overrides (defaults match thesis Bảng 2.3): `ANTHROPIC_SONNET_MODEL` (KC1), `ANTHROPIC_HAIKU_MODEL`
(KC5), `AZURE_GPT41_DEPLOYMENT` (KC2 — must equal your Azure *deployment* name), `AZURE_GPT4OMINI_DEPLOYMENT` (KC3).

> **Azure-student-account caveat:** Azure OpenAI serves GPT-4.1 / GPT-4o-mini (KC2/KC3) but **not Claude**.
> `hybrid` mode therefore needs `ANTHROPIC_API_KEY` *in addition to* the Azure keys. If you only have Azure,
> run `KC_LIVE_MODE=azure` for a real (non-hybrid) reproduction and note in the thesis that KC1/KC5 used a GPT
> model instead of Claude.

**Auto-measured:** completion %, per-KC latency, tokens, cost, KC4 consistency, KC5 avg-iterations + score-delta,
AC/entity/endpoint counts. **Opt-in (`KC_LIVE_BUILD=1`):** compile-pass (KC2), tests-build (KC3). **Still manual:**
AC-coverage % vs a ground-truth answer key.

### 3. Outputs (under `TestResults/`)
- `kc_metrics_live.csv` — one row per agent call (RunId, KC, agent, model, tokens, latency, cost, success).
- `kc_live/run-NN.json` — per-run transcript (status, iterations, scores, file counts).
- `kc_live_summary.md` — the **measured-vs-Table-2.6** comparison table.

### 4. Compare
Open `kc_live_summary.md`: each measured cell should land within tolerance of Table 2.6 (or update the thesis
cell to the measured value). Cross-check [comparison/kc_v1_vs_thesis_table_2_6.md](comparison/kc_v1_vs_thesis_table_2_6.md).

### 5. Cost
≈ **$5–15** for `n=10` hybrid; the `KC_LIVE_MAX_USD` guard stops the run if exceeded. KC4 (full pipeline) dominates.

---

## Table 2.7 (manual baseline) — provenance

Table 2.7 cites "5 developers, 2–5 yrs .NET". Before the defense:
- Locate any raw timing notes behind the 45–60 / 90–120 / 60–90-minute figures.
- If it was an informal time-and-motion estimate (not a controlled study), say so plainly and state the
  assumptions + reviewer rubric (the 1–10 "completeness / consistency / convention" scale). A committee will
  accept a clearly-scoped estimate far better than an unsourced precise-looking number.

## Security
- Never commit keys. The JSON transcripts contain response content + metrics, **not** secrets — inspect before committing.
- Rotate keys after the defense.

## Status
- [x] Build the live KC runner — `KcLiveBenchTests` (hybrid/azure/claude, n configurable, budget guard, CSV + per-run JSON + summary). Compiles under .NET 10; skips unless `RUN_LIVE_LLM=1`.
- [ ] Decide path (A / preserve / B)
- [ ] Provide keys + run (`RUN_LIVE_LLM=1`; hybrid needs Anthropic **and** Azure)
- [ ] Open `kc_live_summary.md`; confirm Table 2.6 / 2.7 reproduce (or update cells)
