## Text Analytics for Health Adaptive Client for Processing Large Volumes of Data

This sample provides code and best practices on how to process large volumes of data using Text Analytics for Health in a scalable way.


#### prerequeisites

#### components
-  **Input Storage** - this is where the all the documents are stored. This could be a local file system storage, muonted volume, an azure blob storage or any other implementation that supports storage of files using paths.
 The default implmentation assumes that every .txt file under the input storage location should be sent to processing by TA4H.
-  **Output Storage** - this is where the results of TA4H analysis will be stored as json files.
-  **Client Application** - reads the documents from the Input storage, sends them analytis using the TA4H API, and stored where results once they're ready.
-  **Metadata Storage** - this is where the data about what documents exists and what is their processing status is stored.

####

#### Application Runtime Flow
- Find all text files in the Input Storage
- While there are still documents to be processed:
  - load a batch of text documents
  - group the documents into "payloads" of multiple documents (the Text Analytics API can handle up to 25 documents in one request).
  - send the pyloads


## Running the Application Locally
For small volumes of data (up to 50k documents), running the .NET application locally can be good enough. Here's how to run it locally:

1. Clone the repository from GitHub: https://github.com/Microsoft/azure-health-ai-services-samples/tree/main/samples/ta4h-adaptive-client-blueprint
2. Navigate to the `ta4h-adaptive-client-blueprint` directory.
3. Run the following command to build the application: `dotnet build`
4. Run the following command to start the application: `dotnet run`

## Running the Application in Azure
When working with large datasets, the processing can take many hours or days, and then it is important to have a more resilient solution that can run on the cloud. The suggested solution is using the following components:

- Azure Language Resource: This is the account that is used for calling the Text Analytics for Health API.
- Docker: The Text Analytics for Health Adaptive Client .NET application should be built into a Docker container image.
- Azure Container Instances: The container image will be run using Azure Container Instances (ACI). ACI supports by default an "Always On" restart policy, which guarantees that if the application failed or stopped for any reason, the container will be restarted. Also, running it in the same Azure region as the Language Resource and other Azure components means that the network latency will be minimal.
- Azure Blob Storage: Will be used to store (as text files) the input documents that should be sent to TA4H, and to then store the analysis results returned from TA4H for these in JSON format.
- Azure Table Storage: Will be used to store the metadata of the documents and help manage the workflow.
- Azure SQL Server: An alternative for Azure Table Storage.
- Application Insights (optional): Can store the logs written from the application - can help investigate any issues and gather statistics about errors and latency.

### Deploying to Azure
Follow these steps to deploy the Text Analytics for Health Adaptive Client for Processing Large Volumes of Data on Azure:



1. Create a Resource Group:
```
az group create --name <resource-group-name> --location <location>
```

2. Create the Language Resource.
*Note: if you already have a language resource in this region, you can skip this step and just use the api key of the existing resource*  
```
az cognitiveservices account create --name <language-resource-name> --resource-group <resource-group-name> --kind TextAnalytics --sku S --location <location> --yes
```

3. Get the language resource apiKey:
```
az cognitiveservices account keys list --name <language-resource-name> --resource-group <resource-group-name>
```

4. Create a Managed Identity that will be used for the container instance:
```
az identity create --name <identity-name> --resource-group <resource-group-name>
```
```
az identity list --resource-group <resource-group-name>
```

copy the "clientId" value from the response, you will need it for role assignments in the next steps


5. Create an Azure Storage account:
```
az storage account create --name <storage-account-name> --resource-group <resource-group-name> --sku Standard_LRS
```

6. Add role assignments for blob read/write access and table read/write access to the managed identity you created.
```
az role assignment create --role "Storage Blob Data Contributor" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Table Data Contributor" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
```

7. If you want to run the application locally with azure storage, add your identity as well
```
az role assignment create --role "Storage Blob Data Contributor" --assignee <your-azure-identity-email> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Table Data Contributor" --assignee <your-azure-identity-email> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
```

7. Load the input data into blob storage:
```
az storage container create --name <input-blob-container-name> --account-name <storage-account-name> --auth-mode login
az storage blob upload-batch --destination <input-blob-container-name> --source <input-txt-files-local-dir> --account-name <storage-account-name> --auth-mode login
```

8. Create the ACR:
```
az acr create --resource-group <resource-group-name> --name <acr-name> --sku Basic
```

9. Assign read access to the managed identity you created:
```
az role assignment create --role "AcrPull" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.ContainerRegistry/registries/<acr-name>
```

10. Build the application using the Dockerfile. Name the image with the name of the registry you created:
```
docker build -t <acr-name>.azurecr.io/<image-name>:<tag> .
```

11. Login to the ACR:
```
az acr login --name <acr-name>
```

12. Push the image to the ACR:
```
docker push <acr-name>.azurecr.io/<image-name>:<tag>
```

13. Create Application Insights:
```
az monitor log-analytics workspace create --resource-group <resource-group-name> --workspace-name <workspace-name>
az extension add -n application-insights
az monitor app-insights component create --app ta4hAdaptiveClient --location <location> --kind web --resource-group <resource-group-name>  --workspace "/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/microsoft.operationalinsights/workspaces/<workspace-name>"
```

14. Get the instrumentation key:
```
az monitor app-insights component show --app <app-name> --resource-group <resource-group-name> --query "instrumentationKey"
```

15. Use the aci-template.yml file and replace all the {{}} with the values according to the resources you created:
```
az deployment group create --resource-group <resource-group-name> --template-file aci-template.yml --parameters languageResourceName=<resource-name> languageResourceEndpoint=<billing-endpoint> languageResourceKey=<api-key> storageAccountName=<storage-account-name> storageAccountKey=<storage-account-key> containerRegistryName=<acr-name> containerRegistryUsername=<acr-username> containerRegistryPassword=<acr-password> containerImageName=<image-name> containerImageTag=<tag> containerGroupName=<container-group-name> containerGroupLocation=<location> appInsightsInstrumentationKey=<instrumentation-key> managedIdentityClientId=<managed-identity-client-id> managedIdentityResourceId=<managed-identity-resource-id>
```


