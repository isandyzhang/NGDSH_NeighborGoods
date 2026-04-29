# GitHub Actions CI/CD Setup

This document lists required GitHub Actions workflows, environment settings, and secrets/variables.

## Workflows

- `.github/workflows/frontend_react.yml`
  - Purpose: frontend CI (build/test/artifact)
  - Trigger: `push`, `pull_request`, `workflow_dispatch`
- `.github/workflows/frontend_deploy.yml`
  - Purpose: frontend production deploy (Azure Static Web Apps)
  - Trigger: `workflow_dispatch` only
  - Environment: `production`
- `.github/workflows/backend_csharp_docker.yml`
  - Purpose: backend CI and optional manual production deploy
  - Trigger: `push`, `pull_request`, `workflow_dispatch`
  - Deploy gate: `workflow_dispatch` with `deploy=true`
  - Environment: `production` (deploy job)
- `.github/workflows/infra_bicep.yml`
  - Purpose: infra validate and manual production deploy
  - Trigger: `push`, `pull_request`, `workflow_dispatch`
  - Deploy gate: `workflow_dispatch` only
  - Environment: `production` (deploy job)

## GitHub Environments

Create environment:

- `production`

Recommended environment protection:

- Required reviewers enabled
- Wait timer enabled (optional)
- Restrict branches to `main`

## Repository Secrets

Required for Azure login/deploy:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_RESOURCE_GROUP`
- `AZURE_STATIC_WEB_APPS_API_TOKEN`

## Repository Variables

Required for backend deploy:

- `AZURE_RESOURCE_GROUP`
- `AZURE_CONTAINER_APP_NAME`

Required for frontend deploy build:

- `VITE_API_BASE_URL`
- `VITE_SIGNALR_BASE_URL`

## Security Guidance

- Never commit real secrets into repository files.
- Keep `infra/bicep/deploy.parameters.json` local-only and excluded from git.
- Use `infra/bicep/deploy.parameters.example.json` as the committed template.
