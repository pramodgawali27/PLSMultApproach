# Deploy to Azure Container Apps (Demo)

This guide uses GitHub Actions to build/push Docker images to ACR and deploy to Azure Container Apps.

## Prerequisites
- Azure subscription
- Azure CLI locally (optional, for setup/verification)
- GitHub repo with Actions enabled

## 1) Create Azure resources

Set your defaults:
```
az login
az account set --subscription <SUBSCRIPTION_ID>
az group create -n pls-demo-rg -l eastus
az acr create -n plsdemoregistry -g pls-demo-rg --sku Basic
az containerapp env create -n pls-demo-aca -g pls-demo-rg -l eastus
```

## 2) Create service principal for GitHub Actions
```
az ad sp create-for-rbac \
  --name pls-demo-gha \
  --role contributor \
  --scopes /subscriptions/<SUBSCRIPTION_ID> \
  --sdk-auth
```
Copy the JSON output and store it in the GitHub secret `AZURE_CREDENTIALS`.

## 3) Add GitHub secrets

Required secrets:
- `AZURE_CREDENTIALS` (JSON from `az ad sp create-for-rbac`)
- `ACR_LOGIN_SERVER` (e.g., `plsdemoregistry.azurecr.io`)
- `ACR_USERNAME`
- `ACR_PASSWORD`
- `POSTGRES_CONN` (connection string for your prod Postgres)
- `SUMMARIZATION_URL` (internal URL for summarization container app)
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_KEY`
- `AZURE_OPENAI_DEPLOYMENT_PROMPT`
- `AZURE_OPENAI_DEPLOYMENT_FINETUNE`
- `AZURE_OPENAI_DEPLOYMENT_AGENT`
- `AZURE_OPENAI_DEPLOYMENT_IMAGE`
- `API_BASE_URL` (public URL for document-service ingress)
- `STORAGE_BLOB_CONNECTION` (Azure Blob connection string)
- `STORAGE_BLOB_CONTAINER` (e.g., `plsfiles`)

Tip: Get ACR credentials:
```
az acr credential show -n plsdemoregistry -g pls-demo-rg
```

## 4) GitHub Actions workflow
The workflow is located at:
```
.github/workflows/aca-deploy.yml
```
It builds and pushes images, then deploys:
- `pls-document-service` (external)
- `pls-orchestrator-service` (internal)
- `pls-summarization-service` (internal)
- `pls-web` (external)

## 5) Verify deployment
```
az containerapp list -g pls-demo-rg -o table
```
Get the public URLs:
```
az containerapp show -g pls-demo-rg -n pls-web --query properties.configuration.ingress.fqdn -o tsv
az containerapp show -g pls-demo-rg -n pls-document-service --query properties.configuration.ingress.fqdn -o tsv
```

Update `API_BASE_URL` secret to the document-service FQDN:
```
https://<document-service-fqdn>
```

## Runtime API base config for web (no rebuild)
The web UI reads `/config.json` at runtime. Use an ACA config map to override it.

1) Create a local `prod-config.json`:
```json
{ "apiBaseUrl": "https://api.yourdomain.com" }
```

2) Create/update the config map:
```bash
az containerapp configmap set \
  --name <web-app> \
  --resource-group <rg> \
  --key config.json \
  --value "$(cat prod-config.json)"
```

3) Mount it into the web container:
```bash
az containerapp update \
  --name <web-app> \
  --resource-group <rg> \
  --set \
  "template.volumes[0].name=config" \
  "template.volumes[0].configMap.name=<web-app>-configmap" \
  "template.containers[0].volumeMounts[0].volumeName=config" \
  "template.containers[0].volumeMounts[0].mountPath=/app/public" \
  "template.containers[0].volumeMounts[0].readOnly=true"
```

## Notes
- This is demo‑grade and does not include managed Postgres provisioning.
- For prod, use Azure Database for PostgreSQL and managed identities.
