# Text Analytics for Health Container Async Batch Sample Setup

This setup wil help you provision: 
- Text Analytics for Health
- Azure Storage Account
- Azure Kubernetes Service 
- Azure Function.

## Prerequisites
- An Azure Subscription
- kubectl installed 
- az CLI installed 

## Setup Azure Kubernetes, Text Analytics for Health and Storage Account

### Create an SSH key pair

To access AKS nodes, you need to connect using an SSH key pair (public and private), which you generate using the ssh-keygen command. Before selecting the Deploy to Azure button, please ensure that a resource group and a SSH key for Azure has been created. You can create an SSH key by running the command shown below in the Azure CLI or by using the Azure Portal.

```
az sshkey create --name "ta4hclusterKey" --resource-group "<RESOURCE_GROUP_NAME>"
```

!["A screenshot that shows how to generate the ssh key"](/media/text-analytics-for-health-batch-async/ssh.png)

### Setup Azure Resources

When you have your SSH Key, you can setup your Azure Kubernetes Cluster, Text Analytics for Health and Storage Account.

[![Deploy TA4H and an Azure Storage Account to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fmicrosoft%2Fazure-health-AI-services-samples%2FTA4H-async-blueprint%2Fsamples%2Ftext-analytics-for-health-async%2Fazuredeploy-kubernetes-and-services.json)

After creating those resources, you will need to copy the following values:
- The Text Analytics for Health Endpoint
- The Text Analytics for Health Api Key
- The Storage Account connectionstring

These values will be used below to setup the Azure Kubernetes Service

!["A screenshot of the TA4H endpoint end key"](/media/text-analytics-for-health-batch-async/ta4h-keys.png)


## Configure Azure Kubernetes Service

When your cluster is provisioned, you can deploy the containers, configure the load balancer and setup the scaling rules.

### Connect to the cluster

After you generated your SSH key pair, you can connect to your Azure Kubernetes Service.
For this you need kubectl, if you don't have kubectl isntalled, you can install kubectl locally using the az aks install-cli command:
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

When your cluster has been provisioned, we need to deploy the pods and services.
Before you deploy the pods and service, you need to create 3 secrets.

**Don't surround the secrets with quotes, as this will give errors.**

- Storage Connection String
    ```cli
    kubectl create secret generic credentialdata --from-literal=storageconnectionstring=YOUR STORAGE CONNECTION STRING
    ```
- Text Analytics for Health API key
    ```cli
    kubectl create secret generic billing-api-key --from-literal=api-key=YOUR API KEY
    ```
- Text Analytics for Health billing endpoint
    ```cli
    kubectl create secret generic billing-endpoint --from-literal=billing=YOUR API ENDPOINT
    ```

> **Note**
> For this sample we are using environment variables to store and consume secrets. For production workloads we recommend to use an external key store, such as Azure Key Vault. There are plugins into Kubernetes to then mount the secrets. You can find more info [here](https://learn.microsoft.com/en-us/azure/aks/csi-secrets-store-driver)

After setting the secrets, you can now deploy the application using the kubectl apply command:

TA4H.yaml can be found [here](samples\text-analytics-for-health-async\TA4H.yaml).
```cli
kubectl apply -f "https://raw.githubusercontent.com/microsoft/azure-health-AI-services-samples/TA4H-async-blueprint/samples/text-analytics-for-health-async/TA4H.yaml"
```

When the deployment is succesfull, you should be seeing the following services

```cli
kubectl get services
```

!["A screenshot of the kubernetes services"](/media/text-analytics-for-health-batch-async/services.png)

You will need to have your Azure Kubenernetes service External IP to provide in the deployment below.

## Deploy the client applicaton to the Azure Function

The last step is to deploy the Client Function. This function will recieve the documents and send them to your cluster. 
Copy your External load balancer IP, as you will need this in the deployment below

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fmicrosoft%2Fazure-health-AI-services-samples%2FTA4H-async-blueprint%2Fsamples%2Ftext-analytics-for-health-async%2Fazuredeploy-function.json)


## Using the sample

You can find all the relevant information on how to use the sample [here](Usage.md)