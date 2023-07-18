## Text Analytics for Health Adaptive Client for Processing Large Volumes of Data

This sample provides code and setup instructions for processing large volumes of medical data using Text Analytics for Health.
The TextAnlytics SDKs provide a convenient way to call the TextAnalytics API and work with the analysis results programatically.
However, when you have large volumes of data that you want to process using TextAnalytics for Health and store the results,
it is important to have a solution that is optimized for efficiency and throughput. This sample aims to do exactly that.
It tries to minimize the overall time for processing a large number of documents given the capacity of the servers at any given moment (whether you use the hosted API or a self-hosted solution) and the service limits (i.e. max allowed API calls per minute).


#### Solution Architecture
This solution is composed of several components:
-  **Input Storage** - this is where text documents are stored. This could be a local file system storage, a mounted volume, an azure blob storage or any other implementation of the IFileStorage interface.
 The default implmentation assumes that every .txt file under the input storage location should be processed by TA4H.
-  **Output Storage** - this is where the results of TA4H analysis will be stored as json files. Like the input storage is can have different implementations.
-  **Metadata Storage** - this is where the metadata about each documents is stored (like its status, e.g. not started, processing, completed etc.). Could be an Azure table storage, a SQL database or in-memory implementation.
-  **Text Analytics for Health Endpoint** - where the request will be sent to. This could be a hosted entpoint or and onprem
-  **Client Application** - A dotnet 6 Console Application that reads the documents from the Input storage, sends them to processing and stores the results once they're ready.


#### Application Runtime Flow
- Find all text files in the Input Storage
- Create an entry in the Metadata Storage for each document. Note: Documents added to the Input Storage after the application has started will not be processed.
- While there are still documents to be processed:
  - load a batch of text documents
  - group the documents into "payloads" of multiple documents (the Text Analytics API can handle up to 25 documents in one request) to maximize the throughput
  - send each payload to the Text Analytics for Health Endpoint for processing. Note: the TA4H API is based on an asynchronous flow, so these calls do not return the rpocessing results, but instead return a job id that can can be used to poll the API for the job status and get the analysis results when they are ready.
- On a different thread, poll the API for the jobs that have been created and not completed yet.
  - When a job is done, store the results in the Output Storage and update the Metadata Storage with its status.
- Continously monitor the pending jobs and the average time to completion, to adapt the number of concurrent jobs. This adaptivity is important because it allows to utilize the maximun capacity of the server without overloading it.
- When all documents are processed and all jobs completed, exit the app.

#### How to run the console application locally
It is possible to run the application locally with no need to create any resources in Azure (except for the Language Resource itself).
This is reccomended in case of smaller datasets that you can complete processesing within a few hours or less.
*requirements*:
dotnet 6 runtime
Visual Studio (optional)
 

1. Clone the repository from GitHub: https://github.com/Microsoft/azure-health-ai-services-samples/tree/main/samples/ta4h-adaptive-client-blueprint
2. Navigate to the `ta4h-adaptive-client-blueprint` directory.
3. Create a launchSetting.json file under the 'Properties' directory, and copy the "Ta4hAdaptiveClient-Local" profile definition from the the launchSetting-template.json.
4. Populate the missing values:
   - api key and endpoint of your azure language resource. Note that if you are using Text Analytics for Health Container and not the hosted API, you need to specify the url of the container / cluster you are running it and not the language resource billing endpoint.
   - paths to local directories for input (text documents) and output (json results).
5. You can now build, run and debug the application in Visual Studio.
6. Alernatively you can run the application from command line using: `dotnet run --launch-profile Ta4hAdaptiveClient-Local`

#### How to deploy and run in Azure
When working with large volumes of data, the processing can take many hours or days, and then it is recommended to have a more resilient solution that can run on the cloud.
The suggested solution is using the following components:

- Azure Language Resource: This is the account that is used for calling the Text Analytics for Health API.
- Docker: The Text Analytics for Health Adaptive Client dotnet application should be built into a Docker container image.
- Azure Container Instances: The container image will be run using Azure Container Instances (ACI). ACI supports an "Always On" restart policy, which guarantees that if the application failed or stopped for any reason, the container will be restarted.
Also, running the application on Azure in the same region as the Language Resource and other Azure components means minimizes the network latency.
- Azure Blob Storage: Will be used to store (as text files) the input documents that should be sent to TA4H, and to then store the analysis results returned from TA4H for these in JSON format.
- Azure Table Storage: Will be used to store the metadata of the documents and help manage the workflow.
- Application Insights (optional): Can store the logs written from the application - can help investigate any issues and gather statistics about errors and latency.

The following instruction describe step-by-step how to deploy this solution on Azure. We use the azure cli here but you can also do almost all of the steps directly the azure portal.
As you go throgh the steps, copy the values for the parameters that appear in \<angle brackets> and write them in the Deployment\deployment-params.json file. You will need to use them in the next steps.


- Set the azure subscription you are going to create all the reosurce in (if you hav emore than one)
```
az account set --subscription <subscription-id>
```

- Create a Resource Group to contain the Azure resources that will be used to deploy this solution:
```
az group create --name <resource-group-name> --location <location>
```

-  Create the Language Resource.
*Note: if you already have a language resource in this region, you can skip this step and just use the api key of the existing resource*  
```
az cognitiveservices account create --name <language-resource-name> --resource-group <resource-group-name> --kind TextAnalytics --sku S --location <location> --yes
```

-  Get the language resource apiKey and write its value in Deployment\deployment-params.json
```
az cognitiveservices account keys list --name <language-resource-name> --resource-group <resource-group-name>
```

-  Create a Managed Identity. You will later assign this identity to the azure container instance running the application. This will allow the application to authenticate to the other azure resources without the need to use passwords or connection strings.
```
az identity create --name <identity-name> --resource-group <resource-group-name>
```
- Get the clientId of the managed identity you have just created and copy it to Deployment\deployment-params.json as managed-identity-client-id
```
 az identity show --name <identity-name> --resource-group <resource-group-name>  --query "clientId"
```

- Create an Azure Storage account:
```
az storage account create --name <storage-account-name> --resource-group <resource-group-name> --sku Standard_LRS
```

- Add role assignments for blob read/write access and table read/write access to the managed identity you created.
```
az role assignment create --role "Storage Blob Data Contributor" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Table Data Contributor" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
```

- If you would like to run the application locally with azure storage, add the same role assignment to your aad identity as well
```
az role assignment create --role "Storage Blob Data Contributor" --assignee <your-azure-identity-email> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
az role assignment create --role "Storage Table Data Contributor" --assignee <your-azure-identity-email> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Storage/storageAccounts/<storage-account-name>
```

- Load the input data into blob storage:
```
az storage container create --name <input-blob-container-name> --account-name <storage-account-name> --auth-mode login
az storage blob upload-batch --destination <input-blob-container-name> --source <input-txt-files-local-dir> --account-name <storage-account-name> --auth-mode login
```

- Create an Azure Container Registry (ACR) to host the application docker image:
```
az acr create --resource-group <resource-group-name> --name <acr-name> --sku Basic
```

-  Add read permissions to the managed identity you created to pull from the ACR. This will enable the Azure container instance to pull the image without using a user+password login:
```
az role assignment create --role "AcrPull" --assignee <managed-identity-client-id> --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.ContainerRegistry/registries/<acr-name>
```

-  Build the application using the Dockerfile. Navigate tot he root directory of the project and run:
```
docker build -t <acr-name>.azurecr.io/<image-name>:<tag> .
```

- Login to the ACR:
```
az acr login --name <acr-name>
```

- Push the image to the ACR:
```
docker push <acr-name>.azurecr.io/<image-name>:<tag>
```

- Optional: Create Application Insights to store the application telemetry:

``` az monitor log-analytics workspace create --resource-group <resource-group-name> --workspace-name <workspace-name> ```

``` az extension add -n application-insights ``` 

``` az monitor app-insights component create --app ta4hAdaptiveClient --location <location> --kind web --resource-group <resource-group-name>  --workspace "/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/microsoft.operationalinsights/workspaces/<workspace-name>"``` 


- Get the Application Insights Connection String and copy it to Deployment\deployment-params.json as appinsights-connection-string
```
az monitor app-insights component show --app <app-name> --resource-group <resource-group-name> --query "connectionString"
```

- Under the \Deployment folder, copy the aci_template.yaml file into a new file called aci_deployment.
find and replace all the parts in square brackets with the values you stored in deployment-params.json. You can do it manually, or automatically by running the utility python script
```
python -m populate_with_parameters --template-file aci_temlate.yaml
```

- Validate that all parameterized parts of the aci_temlate.yaml are populated correctly. Then, create the Azure Container Instance to run the application. 

```
az container create --resource-group <resource-group-name> --file aci_deployment.yml
```


