# Infra — Azure Container Apps deployment

## Stack

| Resource | SKU | Purpose |
|---|---|---|
| Azure Container Registry | Basic | Store the Docker image |
| Container Apps Environment | Consumption | Host the Container App (auto-scale, scale-to-zero) |
| Container App | — | Run `AgenticSdlc.Api` |
| Log Analytics Workspace | PerGB2018 | Centralized logs + diagnostics |
| Application Insights | Workspace-based | APM, distributed traces, agent cost log |
| Key Vault | Standard | Store the Anthropic + Azure OpenAI API keys |
| User-Assigned Identity | — | ACR pull + Key Vault Secrets read |

## Manual deploy (first time)

```bash
# 1. Create the resource group
az group create --name rg-Hoang-LuanVan --location southeastasia

# 2. Deploy Bicep
az deployment group create \
  --resource-group rg-Hoang-LuanVan \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.json \
  --parameters containerImage=mcr.microsoft.com/azuredocs/containerapps-helloworld:latest

# (the first time uses a placeholder image — the build step below will update it)

# 3. Build + push the image to ACR
ACR=$(az acr list -g rg-Hoang-LuanVan --query "[0].loginServer" -o tsv)
az acr login --name "$ACR"
docker build -t "$ACR/agenticsdlc:$(git rev-parse --short HEAD)" .
docker push "$ACR/agenticsdlc:$(git rev-parse --short HEAD)"

# 4. Update the Container App with the real image
az containerapp update \
  --name agenticsdlc-dev \
  --resource-group rg-Hoang-LuanVan \
  --image "$ACR/agenticsdlc:$(git rev-parse --short HEAD)"

# 5. Set the LLM secrets in Key Vault
KV=$(az deployment group show -g rg-Hoang-LuanVan -n main --query "properties.outputs.keyVaultUri.value" -o tsv | sed 's|https://||;s|/||')
az keyvault secret set --vault-name "$KV" --name "Llm--Anthropic--ApiKey" --value "sk-ant-..."
az keyvault secret set --vault-name "$KV" --name "Llm--AzureOpenAI--ApiKey" --value "..."
az keyvault secret set --vault-name "$KV" --name "Llm--AzureOpenAI--Endpoint" --value "https://<resource>.openai.azure.com"

# 6. Restart the Container App to pick up the new secrets
az containerapp revision restart --name agenticsdlc-dev --resource-group rg-Hoang-LuanVan
```

## Automated deploy via GitHub Actions

After setting up the OIDC federated credential (see `.github/workflows/deploy.yml`),
every push to `main` automatically builds + pushes the image + updates the Container App revision.

## Persistence (Postgres) — optional

The app stores pipeline runs + metrics + Agent Studio state in Postgres via EF Core.
With no `ConnectionStrings:DefaultConnection` → the app runs stateless (no-op repos).

**Local dev:**

```bash
docker compose up -d          # Postgres 16 at localhost:5432
# set the connection string for Api + Web:
cd src/AgenticSdlc.Api && dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=agentic_sdlc;Username=postgres;Password=postgres"
```

Migrations apply automatically at app startup (`Database.Migrate()`). Generate a new migration:

```bash
dotnet ef migrations add <Name> \
  --project src/AgenticSdlc.Infrastructure --startup-project src/AgenticSdlc.Infrastructure \
  --output-dir Persistence/Migrations
```

**Azure (enable a Postgres flexible server ~$13/month):** deploy with `deployPostgres=true`:

```bash
az deployment group create -g rg-Hoang-LuanVan --template-file infra/main.bicep \
  --parameters infra/main.parameters.json \
  --parameters containerImage=<acr>/agenticsdlc:<tag> deployPostgres=true \
               postgresAdminPassword='<strong-password>'
```

Bicep automatically creates the server + DB + firewall (allow Azure services) + injects
`ConnectionStrings__DefaultConnection` as a Container App secret. By default
`deployPostgres=false` so the CI workflow does not incur unintended cost.

## Cleanup

```bash
az group delete --name rg-Hoang-LuanVan --yes --no-wait
```

> ⚠️ Key Vault soft-delete is retained for 7 days. To delete permanently:
> `az keyvault purge --name <kv-name>`

## Cost estimate (Q2/2026, southeastasia)

| Resource | Free / Min cost / Month |
|---|---|
| Container Apps (Consumption, idle/0 replica) | ~$0 (pay only for actual requests) |
| Container Apps (1 replica 0.5 CPU 1GB running continuously) | ~$15-20 |
| Log Analytics (5GB free + ingest) | ~$0-3 |
| App Insights (5GB free) | ~$0 |
| ACR Basic | ~$5 |
| Key Vault Standard | ~$0.03 / 10k ops |
| **Total prototype dev (scale-to-zero)** | **~$5-10/month** |

For a demo / load test, the LLM cost (Anthropic + Azure OpenAI) will far exceed the infra cost.
