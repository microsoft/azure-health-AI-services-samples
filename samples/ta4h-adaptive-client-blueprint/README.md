## Text Analytics for Health Adaptive Client for Processing Large Volumes of Data

This sample provides code and setup instructions for processing large volumes of medical data using Text Analytics for Health. While the TextAnalytics SDKs offer a convenient way to call the TextAnalytics API and work with the analysis results programmatically, processing large volumes of data requires a solution optimized for efficiency and throughput. This sample is designed to minimize overall processing time for a large number of documents, considering server capacity and service limits (i.e., max allowed API calls per minute).

### Solution Architecture

The solution comprises several components:

-  **Input Storage**: Stores text documents. This could be a local file system storage, a mounted volume, an Azure blob storage, or any other implementation of the IFileStorage interface. The default implementation assumes that every .txt file under the input storage location should be processed by TA4H.

-  **Output Storage**: Stores the results of TA4H analysis as JSON files. Like input storage, it can have different implementations.

-  **Metadata Storage**: Stores metadata about each document, such as its status (e.g., not started, processing, completed, etc.). This could be Azure table storage, a SQL database, or an in-memory implementation.

-  **Text Analytics for Health Endpoint**: Where the request will be sent. This could be the hosted API endpoint or an on-prem container.

-  **Client Application**: A dotnet 6 Console Application that reads documents from Input storage, sends them for processing, and stores the results once they're ready.

### Application Runtime Flow

1. Find all text files in the Input Storage.
2. Create an entry in the Metadata Storage for each document. (Note: Documents added to the Input Storage after the application has started will not be processed.)
3. While there are still documents to be processed, perform the following steps:
    - Load a batch of text documents.
    - Group documents into "payloads" of multiple documents (the Text Analytics API can handle up to 25 documents in one request) to maximize throughput.
    - Send each payload to the Text Analytics for Health Endpoint for processing. (Note: the TA4H API is based on an asynchronous flow, so these calls return a job ID, which can be used to poll the API for job status and retrieve the analysis results when ready.)
4. On a separate thread, poll the API for the jobs that haven't been completed yet. When a job is done, store the results in the Output Storage and update the Metadata Storage with its status.
5. Continuously monitor pending jobs and average completion time to adapt the number of concurrent jobs. This adaptivity is crucial because it allows the server to utilize its maximum capacity without overload.
6. Exit the app when all documents are processed, and all jobs are completed.

### Run as Local Console Application

You can run the application locally with no need to create any resources in Azure (except for the Language Resource itself). This is recommended for smaller datasets that you can process within a few hours or less.

**Requirements**: Dotnet 6 runtime, Visual Studio (optional)

To run as a local console application, follow these steps:

1. Clone the repository from GitHub: https://github.com/Microsoft/azure-health-ai-services-samples
2. Navigate to the `samples/ta4h-adaptive-client-blueprint` directory.
3. Create a `launchSetting.json` file under the 'Properties' directory and copy the "Ta4hAdaptiveClient-Local" profile definition from the `launchSetting-template.json`.
4. Populate the missing values:
   - API key and endpoint of your Azure language resource. Note that if you are using Text Analytics for Health Container and not the hosted API, you need to specify the URL of the container/cluster you are running it on and not the language resource billing endpoint.
   - Paths to local directories for input (text documents) and output (JSON results).
5. You can now build, run, and debug the application in Visual Studio.
6. Alternatively, you can run the application from the command line using:
 ```
dotnet run --launch-profile Ta4hAdaptiveClient-Local
```

### Run in Azure

Processing large volumes of data can take many hours or days. In such cases, it is recommended to have a resilient solution that can run on the cloud. The suggested solution comprises the following components:

- Docker: The Text Analytics for Health Adaptive Client dotnet application should be built into a Docker container image.
- Azure Container Instances (ACI): The container image will be run using ACI. ACI supports an "Always On" restart policy, which ensures that the container will be restarted if the application fails or stops for any reason. Running the application on Azure in the same region as the Language Resource and other Azure components minimizes network latency.
- Azure Language Resource: This is the account used to call the Text Analytics for Health API.
- Azure Blob Storage: Used to store the input documents (as text files) that should be sent to TA4H, and to store the analysis results returned from TA4H in JSON format.
- Azure Table Storage: Used to store the metadata of the documents and manage the workflow.
- Application Insights (optional): Can store the logs written from the application - helpful for investigating any issues and gathering statistics about errors and latency.

### Run in Azure - Step by Step Instructions

The following instructions describe how to deploy this solution on Azure using the Azure CLI. As you go through the steps, copy the values for the parameters that appear in \<angle brackets> and write them in the `Deployment\deployment-params.json` file. You will need these in the next steps. (You can also perform most of these steps directly through the Azure portal.)

**1. Prerequisites**

- Set the Azure subscription you are going to create all the resources in (if you have more than one)
```
az account set --subscription <subscription-id>
```
- Create a Resource Group to contain the Azure resources that will be used to deploy this solution:
```
az group create --name <resource-group-name> --location <location>
```
- Create the Language Resource. (Note: if you already have a language resource in this region, you can skip this step and just use the API key of the existing resource.)
```
az cognitiveservices account create --name <language-resource-name> --resource-group <resource-group-name> --kind TextAnalytics --sku S --location <location> --yes
```
- Get the language resource apiKey and write its value in `Deployment\deployment-params.json`


###### 2. Azure Storage

- Create an Azure Storage account:
```
az storage account create --name <storage-account-name> --resource-group <resource-group-name> --sku Standard_LRS
```

- Load the input data into blob storage:
```
az storage container create --name <input-blob-container-name> --account-name <storage-account-name> --auth-mode login
az storage blob upload-batch --destination <input-blob-container-name> --source <input-txt-files-local-dir> --account-name <storage-account-name> --auth-mode login
```

###### 3. Container Registry

- Create an Azure Container Registry (ACR) to host the application Docker image:
```
az acr create --resource-group <resource-group-name> --name <acr-name> --sku Basic
```

###### 4. Managed Identity and Role Assignment

- Create a Managed Identity. This will be assigned to the Azure Container Instance running the application, allowing the application to authenticate to other Azure resources without the need for passwords or connection strings.
```
az identity create --name <identity-name> --resource-group <resource-group-name>
```
- Retrieve the client ID of the managed identity just created and copy it to Deployment\deployment-params.json as managed-identity-client-id:
```
 az identity show --name <identity-name> --resource-group <resource-group-name>  --query "clientId"
```

- Add role assignments for blob read/write access and table read/write access to the managed identity:
```
az role assignment create --role "Storage Blob Data Contributor" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Table Data Contributor" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
```

- If you plan to run the application locally with Azure Storage, add the same role assignment to your AAD identity:
```
az role assignment create --role "Storage Blob Data Contributor" --assignee <your-azure-identity-email> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Table Data Contributor" --assignee <your-azure-identity-email> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
```

- Add read permissions to the managed identity to pull from the ACR, enabling the Azure Container Instance to pull the image without using user-password login:
```
az role assignment create --role "AcrPull" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.ContainerRegistry/registries/<acr-name>
```

###### 5. Application Insights (Optional)

- Create Application Insights to store the application telemetry:

``` 
az monitor log-analytics workspace create --resource-group <resource-group-name> --workspace-name <workspace-name> 
az extension add -n application-insights
az monitor app-insights component create --app ta4hAdaptiveClient --location <location> --kind web --resource-group <resource-group-name>  --workspace "/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/microsoft.operationalinsights/workspaces/<workspace-name>"
``` 

- Retrieve the Application Insights Connection String and copy it to Deployment\deployment-params.json as appinsights-connection-string:
```
az monitor app-insights component show --app <app-name> --resource-group <resource-group-name> --query "connectionString"
```

###### 6. Build and Push Docker Image

- Build the application using the Dockerfile. Navigate to the root directory of the project and run:
```
docker build -t <acr-name>.azurecr.io/<image-name>:<tag> .
```

- Log in to the ACR:
```
az acr login --name <acr-name>
```

- Push the image to the ACR:
```
docker push <acr-name>.azurecr.io/<image-name>:<tag>
```

###### 7. Azure Container Instance

- Under the \Deployment folder, copy the aci_template.yaml file into a new file called aci_deployment. Find and replace all the parts in square brackets with the values you stored in deployment-params.json. You can do this manually or automatically by running the utility Python script:
```
python -m populate_with_parameters --template-file aci_template.yaml
```

- Validate that all parameterized parts of the aci_template.yaml are populated correctly. Then, create the Azure Container Instance to run the application. 
```
az container create --resource-group <resource-group-name> --file aci_deployment.yaml
```
---
