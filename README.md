# Microsoft Foundry and Azure App Service with Private Networking
This repository demonstrates how an **ASP.NET Core** app running on **Azure App Service** consumes a **Microsoft Foundry** agent over **private connectivity** (VNet Integration, Private Endpoint and Private DNS), authenticating with **Managed Identity** and no secrets.

The app runs a **Quartz.NET** background job every 30 seconds. Each run asks the Foundry agent to write one short paragraph about an Azure service and writes the answer to the application logs, which you can watch in the App Service Log stream. The Foundry resource has public access disabled, so the call only succeeds through the private path:

```text
app-vnet-demo (App Service, public access OFF)
    ↓ VNet Integration
default subnet (10.0.0.0/24)
    ↓
vnet-demo (10.0.0.0/16)
    ↓ Private DNS (privatelink.services.ai.azure.com)
pe-foundry-vnet-integration (10.0.1.x, snet-foundry)
    ↓ Private Link
foundry-vnet-integration / proj-default
    ↓
azure-agent → response → Log stream
```

## What's inside
* `FoundryAgentJob/Program.cs`: configures console logging, validates the `Foundry` options on startup, registers the `AIProjectClient` with `DefaultAzureCredential` and schedules the Quartz job
* `FoundryAgentJob/Jobs/AgentJob.cs`: Quartz job that calls the agent through the project Responses API and logs the response or any error
* `FoundryAgentJob/Options/FoundryOptions.cs`: strongly typed settings bound to the `Foundry` section
* `FoundryAgentJob/appsettings.json`: project endpoint, agent name, prompt and job interval
* `FoundryAgentJob/Properties/launchSettings.json`: local profile, sets `AZURE_TOKEN_CREDENTIALS=dev` so local runs use your Azure CLI login
* `FoundryAgentJob/deploy.sh`: publishes the app and deploys it to App Service with the Azure CLI
* `Articles/`: article drafts for this series (part 1 covers the infrastructure, part 2 covers this app)

## Prerequisites
* **.NET SDK** compatible with `net10.0`
* **Azure CLI** logged in with a user that can deploy to the App Service (`az login`)
* The infrastructure from part 1, in resource group `rg-vnet-demo`:
  * VNet `vnet-demo` with subnets `default` (App Service VNet Integration) and `snet-foundry` (Private Endpoint)
  * App Service `app-vnet-demo` with the **.NET 10** stack, public access disabled and VNet Integration enabled
  * Microsoft Foundry resource `foundry-vnet-integration` with project `proj-default`, public access disabled and Private Endpoint `pe-foundry-vnet-integration`
  * Private DNS zones linked to `vnet-demo`
* A prompt agent named `azure-agent` in the Foundry project

## Configure credentials
The app uses no keys or connection strings. `DefaultAzureCredential` picks your Azure CLI user locally and the App Service managed identity in Azure.

Managed identity (Azure portal):
1. `app-vnet-demo` > **Identity** > **System assigned** > **On**
2. `foundry-vnet-integration` > **Access control (IAM)** > **Add role assignment**, assign **Azure AI User** to the `app-vnet-demo` managed identity

Settings live in `FoundryAgentJob/appsettings.json`:

```json
"Foundry": {
  "ProjectEndpoint": "https://foundry-vnet-integration.services.ai.azure.com/api/projects/proj-default",
  "AgentName": "azure-agent",
  "Prompt": "Write a single short paragraph about any Azure service of your choice, explaining what it is and when to use it.",
  "JobIntervalSeconds": 30
}
```

Optionally, override any value without redeploying by using environment variables (App Service **Environment variables** > **App settings**). Use `__` as the section separator.

macOS / Linux:
```bash
export Foundry__AgentName="azure-agent"
export Foundry__JobIntervalSeconds="60"
```

Windows (PowerShell):
```powershell
$env:Foundry__AgentName = "azure-agent"
$env:Foundry__JobIntervalSeconds = "60"
```

## Run
Locally:
```bash
az login
cd FoundryAgentJob
dotnet run
```

Because Foundry has public access disabled, local calls fail unless your IP is allowed in the Foundry networking settings. The error is logged and the job keeps running, which shows the network isolation working.

Deploy to Azure:
```bash
cd FoundryAgentJob
chmod +x deploy.sh
./deploy.sh
```

The script selects the subscription, runs `dotnet publish`, enables **Always On** and deploys the zip package with `az webapp deploy`. Since the App Service has public access disabled, which also blocks the SCM endpoint, it temporarily enables public access for the deployment and restores the original value afterwards, even if the deployment fails.

Watch the logs:
```bash
az webapp log tail --resource-group rg-vnet-demo --name app-vnet-demo
```

Output every 30 seconds:
```text
2026-09-23 10:00:00 info: FoundryAgentJob.Jobs.AgentJob[0] Calling Foundry agent 'azure-agent' at https://foundry-vnet-integration.services.ai.azure.com/api/projects/proj-default
2026-09-23 10:00:03 info: FoundryAgentJob.Jobs.AgentJob[0] Foundry agent response: Azure Service Bus is a fully managed enterprise message broker ...
```

## Customizing
* Change `Foundry:Prompt` to ask the agent for something else
* Change `Foundry:JobIntervalSeconds` to run the job more or less often
* Point `Foundry:AgentName` to another agent in the same project
* Edit `FoundryAgentJob/deploy.sh` to target another subscription, resource group or App Service
* If you use a user assigned managed identity, add the `AZURE_CLIENT_ID` app setting with the identity client ID

## References
Microsoft Foundry SDKs: https://learn.microsoft.com/azure/foundry/how-to/develop/sdk-overview  
Azure.AI.Projects for .NET: https://learn.microsoft.com/dotnet/api/overview/azure/ai.projects-readme  
Configure network isolation for Microsoft Foundry: https://learn.microsoft.com/azure/foundry/how-to/configure-private-link  
Foundry Agent Service networking options: https://learn.microsoft.com/azure/foundry/agents/concepts/networking-options  
App Service VNet Integration: https://learn.microsoft.com/azure/app-service/overview-vnet-integration  
Private Endpoint DNS zone values: https://learn.microsoft.com/azure/private-link/private-endpoint-dns  
Managed identities for App Service: https://learn.microsoft.com/azure/app-service/overview-managed-identity  
DefaultAzureCredential credential chains: https://learn.microsoft.com/dotnet/azure/sdk/authentication/credential-chains  
Quartz.NET hosted services integration: https://www.quartz-scheduler.net/documentation/quartz-4.x/packages/hosted-services-integration.html  
Deploy files to App Service: https://learn.microsoft.com/azure/app-service/deploy-zip  
