# Live LLM Smoke Test

Chứng minh ClaudeClient + AzureOpenAiClient gọi được API thật. Mặc định **skip** trong `dotnet test` để CI không cần secret + không tốn tiền.

## Chạy khi nào

- Trước demo bảo vệ — confirm 2 client thật vẫn hoạt động.
- Khi bump model version hoặc đổi endpoint config.
- KHÔNG chạy trong CI mặc định.

## Cost ước tính

~$0.01 mỗi run (2 call, mỗi call ≤ 100 output tokens với model rẻ).

## Cách chạy (PowerShell)

```powershell
# 1. Set env vars
$env:RUN_LIVE_LLM = "1"
$env:ANTHROPIC_API_KEY = "sk-ant-..."
$env:AZURE_OPENAI_API_KEY = "..."
$env:AZURE_OPENAI_ENDPOINT = "https://<resource>.openai.azure.com"

# Optional — override model/deployment
$env:ANTHROPIC_MODEL = "claude-haiku-4-5"           # default
$env:AZURE_OPENAI_DEPLOYMENT = "gpt-4.1"            # default

# 2. Chạy chỉ smoke
dotnet test --filter "FullyQualifiedName~LiveLlmSmokeTests"

# 3. Unset sau khi xong
$env:RUN_LIVE_LLM = $null
$env:ANTHROPIC_API_KEY = $null
$env:AZURE_OPENAI_API_KEY = $null
```

## Expected output

```
Passed!  - Failed: 0, Passed: 2, Skipped: 0
```

Mỗi test assert:
- Response content non-empty.
- `InputTokens > 0`, `OutputTokens > 0` (provider trả usage thật).
- `Latency < 30s` (timeout HttpClient 30s).
- `Provider` = "Claude" hoặc "AzureOpenAI".

## Khi skip

Nếu `RUN_LIVE_LLM != 1` hoặc thiếu API key, test in:

```
Skipped: 2 — RUN_LIVE_LLM != 1 hoặc <KEY> not set.
```

## Bảo mật

- KHÔNG commit API key.
- KHÔNG paste key vào fixture / log / commit message.
- Rotate key sau bảo vệ nếu lo lộ.
- CI workflow `live-smoke.yml` (chưa tạo) sẽ dùng GitHub Secrets, chạy manual `workflow_dispatch`.
