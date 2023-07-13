## Text Analytics for Health Adaptive Client for Processing Large Volumes of Data


#### prerequeisites

#### components
- Azure Language Resource
- Azure Container Registry (ACR)
- Azure Container Instances (ACI)
- Application Insights
- Azure Storage
- Azure sql database (??)


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

2. Create the Language Resource:
```
az cognitiveservices account create --name <resource-name> --resource-group <resource-group-name> --kind TextAnalytics --sku <sku-name> --location <location> --yes
```

3. Get the billing endpoint and apiKey:
```
az cognitiveservices account keys list --name <resource-name> --resource-group <resource-group-name>
```

4. Create a Managed Identity that will be used for the container instance:
```
az identity create --name <identity-name> --resource-group <resource-group-name>
```

5. Create an Azure Storage account:
```
az storage account create --name <storage-account-name> --resource-group <resource-group-name> --location <location> --sku Standard_LRS
```

6. Add role assignments for blob read/write access and table read/write access to the managed identity you created. Also, recommended to add your personal Azure identity for debug purposes:
```
az role assignment create --role "Storage Blob Data Contributor" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Blob Data Reader" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Table Data Contributor" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Table Data Reader" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Blob Data Contributor" --assignee <your-azure-identity-email> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Blob Data Reader" --assignee <your-azure-identity-email> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Table Data Contributor" --assignee <your-azure-identity-email> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Table Data Reader" --assignee <your-azure-identity-email> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
```

7. Load the input data into blob storage:
```
az storage blob upload-batch --destination <container-name> --source <local-folder-path> --account-name <storage-account-name>
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
az monitor app-insights component create --app <app-name> --resource-group <resource-group-name> --location <location> --kind web --application-type web --retention-time 30 --tags <tags>
```

14. Get the instrumentation key:
```
az monitor app-insights component show --app <app-name> --resource-group <resource-group-name> --query "instrumentationKey"
```

15. Use the aci-template.yml file and replace all the {{}} with the values according to the resources you created:
```
az deployment group create --resource-group <resource-group-name> --template-file aci-template.yml --parameters languageResourceName=<resource-name> languageResourceEndpoint=<billing-endpoint> languageResourceKey=<api-key> storageAccountName=<storage-account-name> storageAccountKey=<storage-account-key> containerRegistryName=<acr-name> containerRegistryUsername=<acr-username> containerRegistryPassword=<acr-password> containerImageName=<image-name> containerImageTag=<tag> containerGroupName=<container-group-name> containerGroupLocation=<location> appInsightsInstrumentationKey=<instrumentation-key> managedIdentityClientId=<managed-identity-client-id> managedIdentityResourceId=<managed-identity-resource-id>
```


