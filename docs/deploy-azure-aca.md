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

## Notes
- This is demo‑grade and does not include managed Postgres provisioning.
- For prod, use Azure Database for PostgreSQL and managed identities.
