# Text Analytics for Health Container Async Batch Sample

This blueprint provides code examples and best practices on how to use the Text Analytics for Health in a scalable way when you are using the containerized version of the service.

This sample contains an Azure Function HTTP client application and deployment scripts to setup Text Analytics for Health, an Azure Storage Account and an Azure Kubernetes Service with several Text Analytics for Health nodes, all configured to be used in an scalable and asynchronous way. 


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

In the containerized version we recommend to use the following hardware specifications 

| | Minimal | Recommended 
| ---- | ---- | --- | 
| **CPU** | 4 | 6 
| **Memory** | 10 | 12 



## Architecture

The Azure Function recieves and sends documents to the kubernetes cluster. 
THis sample has been build to make sure the Azure Function and Azure Kubernetes Service can scale based on the user load. 
All documents and results will be asynchronous processed and stored on your Azure Storage Account. 
The processing will increase/decrease based on the number of nodes in your cluster and the Maximum instances

!["A diagram of the Intelligent dashboard architecture"](/media/text-analytics-for-health-batch-async/architecture.jpg)

## Setup the sample

The setup wil help you provision: 
- Text Analytics for Health
- Azure Storage Account
- Azure Kubernetes Service 
- Azure Function.

## Prerequisites
- An Azure Subscription
- kubectl 
- az CLI installed 

Click [here](Setup.md) to setup the needed resources.

## Using the sample

You can find all the relevant information on how to use the sampel [here](Usage.md)