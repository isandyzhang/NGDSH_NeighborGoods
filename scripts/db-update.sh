#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export ASPNETCORE_ENVIRONMENT=Development
dotnet ef database update --project NeighborGoods.Data --startup-project NeighborGoods.Api "$@"
