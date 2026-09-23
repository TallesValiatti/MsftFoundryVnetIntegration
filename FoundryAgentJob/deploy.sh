#!/usr/bin/env bash
#
# Deploys FoundryAgentJob to Azure App Service using the logged-in Azure CLI user.
#
# The App Service has public network access disabled (see Part 1), which also blocks
# the SCM/Kudu endpoint used by zip deploy. This script temporarily enables public
# access, deploys, and then restores the original value (even if the deploy fails).
#
# Usage: ./deploy.sh
#
set -euo pipefail

SUBSCRIPTION_ID="b5141841-0979-4186-959a-bbb84ab01ac1"
RESOURCE_GROUP="rg-vnet-demo"
APP_NAME="app-vnet-demo"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/FoundryAgentJob.csproj"
WORK_DIR="$(mktemp -d)"
PUBLISH_DIR="$WORK_DIR/publish"
ZIP_FILE="$WORK_DIR/app.zip"
ORIGINAL_PUBLIC_ACCESS=""

restore_public_access() {
  if [[ -n "$ORIGINAL_PUBLIC_ACCESS" && "$ORIGINAL_PUBLIC_ACCESS" != "Enabled" ]]; then
    echo ">> Restoring public network access to '$ORIGINAL_PUBLIC_ACCESS'..."
    az webapp update \
      --resource-group "$RESOURCE_GROUP" \
      --name "$APP_NAME" \
      --set publicNetworkAccess="$ORIGINAL_PUBLIC_ACCESS" \
      --output none
  fi
  rm -rf "$WORK_DIR"
}
trap restore_public_access EXIT

echo ">> Checking Azure CLI login..."
if ! az account show --output none 2>/dev/null; then
  az login --output none
fi
az account set --subscription "$SUBSCRIPTION_ID"
echo "   Using subscription: $(az account show --query name -o tsv) ($SUBSCRIPTION_ID)"

echo ">> Publishing project (Release)..."
dotnet publish "$PROJECT_FILE" --configuration Release --output "$PUBLISH_DIR"

echo ">> Creating zip package..."
(cd "$PUBLISH_DIR" && zip -qr "$ZIP_FILE" .)

echo ">> Enabling Always On (keeps the Quartz background job running)..."
az webapp config set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --always-on true \
  --output none

ORIGINAL_PUBLIC_ACCESS="$(az webapp show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --query publicNetworkAccess -o tsv)"
ORIGINAL_PUBLIC_ACCESS="${ORIGINAL_PUBLIC_ACCESS:-Enabled}"

if [[ "$ORIGINAL_PUBLIC_ACCESS" != "Enabled" ]]; then
  echo ">> Public network access is '$ORIGINAL_PUBLIC_ACCESS'. Temporarily enabling it for deployment..."
  az webapp update \
    --resource-group "$RESOURCE_GROUP" \
    --name "$APP_NAME" \
    --set publicNetworkAccess=Enabled \
    --output none
fi

echo ">> Deploying zip package to '$APP_NAME'..."
az webapp deploy \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --src-path "$ZIP_FILE" \
  --type zip \
  --restart true

echo ">> Deployment finished."
echo "   Stream logs with: az webapp log tail --resource-group $RESOURCE_GROUP --name $APP_NAME"
