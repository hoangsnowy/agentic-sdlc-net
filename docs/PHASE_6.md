# Phase 6 — Azure deployment (Container Apps + App Insights)

> Status: ✅ Done (IaC scaffold) — 2026-05-18
> Live deployment: chưa thực hiện (cần Azure subscription + LLM credit).

## Mục tiêu

Cung cấp IaC + CI/CD pipeline hoàn chỉnh để demo `AgenticSdlc.Api` lên Azure Container Apps. Hỗ trợ scale-to-zero (cost ~$5-10/tháng prototype dev), App Insights APM, Key Vault cho LLM secret.

## Deliverable

### Container image

- `Dockerfile` — multi-stage build, runtime `mcr.microsoft.com/dotnet/aspnet:10.0`, non-root user uid 1654, healthcheck `/health`.
- `.dockerignore` — exclude `bin/`, `obj/`, `tests/fixtures/`, `.claude/`, secret files.

### IaC (Bicep)

- `infra/main.bicep` — single-file template, resources:
  - User-Assigned Managed Identity (cho ACR pull + Key Vault Secrets read)
  - Log Analytics + Application Insights (workspace-based)
  - Azure Container Registry (Basic SKU)
  - Key Vault (RBAC, soft-delete 7 ngày)
  - Container Apps Environment (Consumption profile)
  - Container App với scale rule HTTP concurrency 50, min/max replica configurable
- `infra/main.parameters.json` — default cho `dev` env
- `infra/README.md` — hướng dẫn deploy thủ công + cost estimate

### CI/CD

- `.github/workflows/deploy.yml` — trigger trên push `main` (skip docs-only), build + push ACR + re-apply Bicep với tag = `github.sha`. OIDC federated credential (không cần client secret).

### Docs

- `docs/DEPLOYMENT.md` — one-time setup (SP + OIDC + first-time infra), subsequent deploy flow, smoke test, rollback, monitor, troubleshooting.

## Quyết định kỹ thuật

- **Tại sao Container Apps mà không App Service / AKS?**
  - App Service: không scale-to-zero, dev idle vẫn ~$50/tháng.
  - AKS: overkill cho 1 container, ops overhead lớn.
  - Container Apps Consumption: scale-to-zero, idle = $0; auto HTTPS; managed identity; KEDA-based scaling. Match prototype scale.

- **Tại sao single-file Bicep, không Bicep modules?**
  - Single file ≤ 250 dòng dễ review. Modules là premature abstraction cho 1 deploy target.

- **Tại sao Key Vault chứ không env var trực tiếp?**
  - LLM API key là long-lived secret. Rotation qua Key Vault dễ hơn. Container App có thể đọc secret runtime mà không restart (revision với secretRef).

- **Tại sao OIDC chứ không client secret?**
  - GitHub federated credential expire-less, không cần rotation. Subject filter giới hạn workflow chỉ chạy trên `refs/heads/main` → giảm blast radius.

- **Tại sao chưa thực sự deploy?**
  - Không có Azure subscription gắn billing cho luận văn. Đề án sẽ trình bày kiến trúc + chi phí dự kiến (Mục 4.4) thay vì live demo. Khi cần demo: deploy mất ~15 phút theo `docs/DEPLOYMENT.md`.

## Cost dự kiến (southeastasia, Q2/2026)

| Item | Idle | 1 request/s liên tục |
|---|---|---|
| Container Apps (Consumption) | $0 | ~$15/tháng |
| Log Analytics + App Insights | $0 (free tier) | ~$3 |
| ACR Basic | ~$5 | ~$5 |
| Key Vault Standard | ~$0 | ~$0 |
| **Tổng infra** | **~$5/tháng** | **~$23/tháng** |
| Cost LLM (Hybrid Claude+Azure) | — | ~$10-50 / 1k pipeline call |

## Tham chiếu luận văn

- Mục 4.1 — Triển khai prototype trên Azure
- Mục 4.4 — Phân tích chi phí vận hành
- Mục 5.2 — Lộ trình mở rộng (multi-region, blue/green)

## Phase tiếp theo

Phase 6 hoàn tất scope luận văn. Hướng mở rộng sau bảo vệ:
- **Phase 7**: Persistence (Cosmos DB) cho PipelineResult history.
- **Phase 8**: Human-in-the-loop checkpoint qua SignalR (đã có `PipelineOptions.EnableHumanInTheLoop`).
- **Phase 9**: Multi-region failover + blue/green deployment.
