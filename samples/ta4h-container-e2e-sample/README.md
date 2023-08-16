# Text Analytics for Health Container Async Batch Sample

This sample provides code examples and best practices on how to use the containerized version of Text Analytics for Health in a scalable way.

After completing the setup steps for this sample you will have: 
- A simple .Net Core console application to help you stress test the service.
    For a more stable and long-running client application please use our [Adaptive Client Sample](/samples/ta4h-adaptive-client-blueprint/README.md). 
- A Kubernetes cluster that with several Text Analytics for Health Containers.
- Azure Storage service with containers, Queues and Azure Table Storage. 

All these elements can be setup through a guided tutorial with several deployment scripts. With the goal to be used in an scalable and asynchronous way. 

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

**IMPORTANT: The setup can currently only process documents where the processing does not take longer then 24 hours. The service will clear the Queue after 24 hours, this will be fixed in the upcoming version **

## Architecture

The .Net Core Client Console Application sends one or more documents to the Azure Kubernetes Cluster (max 25). 
All documents and processed results are asynchronous processed and stored on an Azure Storage Account. 
If you are looking to process many documents, we recommend looking at the [Azure Kubernetes Autoscaler](https://learn.microsoft.com/en-us/azure/aks/cluster-autoscaler) to dynamically increase/decrease nodes based on requests
!["A diagram of the Intelligent dashboard architecture"](/media/text-analytics-for-health-batch-async/architecture.jpg)

## Prerequisites
- An Azure Subscription
- kubectl 
- az CLI installed 

## Setup the sample

The setup wil help you provision: 
- Text Analytics for Health
- Azure Storage Account
- Azure Kubernetes Service 

Click [here](Setup.md) to setup the resources.

## Using the sample

You can find all the relevant information on how to use the client sample for the containerized setup [here](Usage.md)