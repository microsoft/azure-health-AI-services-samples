# Text Analytics for Health Container Async Batch Sample

This blueprint provides code examples and best practices on how to use the Text Analytics for Health in a scalable way when you are using the containerized version of the Service.

The service contains an Azure Function HTTP client application and deployment scripts to setup Text Analytics for Health, an Azure Storage Account and an Azure Kubernetes Service  with several Text Analytics for Health nodes, configured to be used in an asynchronous way. 


## Throughput and hardware recommendation 

Before setting up the sample, it is important to understand the difference between the **hosted** and **containerized** service. 

Text Analytics for Health has a **hosted** and **containerized** version. There is a difference on throughput and limits for these deployment options

### Throughput recommendations

| | Hosted | Container 
| ---- | ---- | --- | 
| **Max Documents Per Request** | 25 | 1000 
| **Max characters Per Request** | 125,000 | 125,000  
| **Document Throughput per second (S Tier)** | // | 1000 characters per second

### Harware recommendations

In the containerized version we recommend to use the following specificaitons 

| | Minimal | Recommended 
| ---- | ---- | --- | 
| **CPU** | 4 | 6 
| **Memory** | 10 | 12 



## Architecture
When completing the steps in this tutorial, you will have an Azure Function that connects to your Azure Kubernetes Cluster that contains one or more Text Analytics for Health pods. 

The Azure Function can scale and will store all results asynchronous on your Azure Storage Account. Based on the number of nodes you want to scale out, you can process more documents per second. 

!["A diagram of the Intelligent dashboard architecture"](/media/text-analytics-for-health-batch-async/architecture.jpg)

# Getting started

This tutorial wil help you install: 
- Text Analytics for Health
- Azure Storage Account
- Azure Kubernetes Service 
- Azure Function.

## Prerequisites
- An Azure Subscription
- kubectl 
- az CLI installed 

## Setup TA4H and Azure Storage

Before setting up the Azure Kubernetes Service, you need a Text Analytics for Health Service and a storage account.
After creating those 2 resources, you will need to copy the following values:
- The Text Analytics for Health Endpoint
- The Text Analytics for Health Api Key
- The Storage Account connectionstring

These values will be used below to setup the Azure Kubernetes Service

Deploy TA4H and Storage account to Azure

[![Deploy TA4H and Storage account to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fmicrosoft%2Fazure-health-AI-services-samples%2Fmain%2Fsamples%2Ftext-analytics-for-health-async%2Fazuredeploy-services.json)


## Setup Azure Kubernetes Service

### Create an SSH key pair

To access AKS nodes, you connect using an SSH key pair (public and private), which you generate using the ssh-keygen command. By default, these files are created in the ~/.ssh directory. Running the ssh-keygen command will overwrite any SSH key pair with the same name already existing in the given location.

Before selecting the Deploy to Azure button, please ensure that a resource group and a SSH key for Azure has been created. You can create both by running the commands shown below in the Azure CLI or by using the Azure Portal.

```
az sshkey create --name "ta4hclusterKey" --resource-group "<RESOURCE_GROUP_NAME>"
```

!["A screenshot that shows how to generate the ssh key"](/media/text-analytics-for-health-batch-async/ssh.png)

### Setup Azure resources

After you created your new SSH key pair, you can deploy AKS on Azure via the button below.

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fmicrosoft%2Fazure-health-AI-services-samples%2Fmain%2Fsamples%2Fintelligent-dashboard-ta4H%2Fazuredeploy-kubernetes.json)


### Connect to the cluster

If you don't have kubectl isntalled, you can install kubectl locally using the az aks install-cli command:
```cli
az aks install-cli
```

Next step is to configure kubectl to connect to your Kubernetes cluster using the az aks get-credentials command. This command downloads credentials and configures the Kubernetes CLI to use them.

```cli
az aks get-credentials --resource-group <RESOURCE_GROUP_NAME> --name <CLUSTER NAME>
```

Verify the connection to your cluster using the kubectl get command. This command returns a list of the service.

```cli
kubectl get services
```

### Deploy the TA4H container to the cluster

When your AKS has been deployed, we need to deploy the pods and services.
Before you can deploy the yaml, you will need to create 3 secrets.

**Don't surround the secrets with quotes, as this will give errors.**

- Storage Connection String
    ```cli
    kubectl create secret generic credentialdata --from-literal=YOUR STORAGE CONNECTION STRING
    ```
- Text Analytics for Health API key
    ```cli
    kubectl create secret generic billing-api-key --from-literal=api-key=YOUR API KEY
    ```
- Text Analytics for Health billing endpoint
    ```cli
    kubectl create secret generic billing-endpoint --from-literal=YOUR API ENDPOINT
    ```

> [!IMPORTANT] 
> for this sample we are using environment variables to store and consume secrets. For production workloads we recommend to yse an external key store, such as Azure Key Vault. There are plugins into Kubernetes to then mount the secrets. You can find more info [here](https://learn.microsoft.com/en-us/azure/aks/csi-secrets-store-driver)

After setting the variables, you can deploy the application using the kubectl apply command and specify the name of your YAML manifest:

TA4H.yaml can be found [here](samples\text-analytics-for-health-async\TA4H.yaml).
```cli
kubectl apply -f TA4H.yaml
```

When the deployment is succesfull, you should be seeing the following services

```cli
kubectl get services
```

!["A screenshot of the kubernetes services"](/media/text-analytics-for-health-batch-async/services.png)

Copy the external-ip, as you will need it for the Azure Function.

## Deploy the Azure Function

