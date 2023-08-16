# Text Analytics for Health .Net Core Client Console Application

This samples uses a [.Net Core Console Application](/samples/ta4h-container-e2e-sample/StressTestConsoleClient/) that can send one or more documents to the Azure Kubernetes Cluster. The .Net Core Client Console Application sends all the documents, in an async way to the cluster. The Text Analytics for Health containers  will process the documents in an asynchronous way and If all the containers are seeded, all new documents will be added to the Queue.

We recommended to not use this client application for long-running jobs, for a more stable and long-running client application please use our [Adaptive Client Sample.](/samples/ta4h-adaptive-client-blueprint/README.md). 

The high level client architecture can be seen below:

!["Diagram of the client sample setup"](../../media/text-analytics-for-health-batch-async/client-architecture.png)

The incoming documents can be found in the `input` container. All the processed documents can be found in the `result` container. All documents from the `input` and `result` container are  corelated by an unique identifier.

When all containers are busy processing, all new documents will be put on the queue and picked up by a container as soon as its done processing previous documents. 

All the information of the jobs can be found in the `taJobs` Table in Azure Table Storage. The Status field shows the status of the document (`Queued, Processing, Succeeded, Failed`)

An example of the `taJobs` can be seen below
!["Text Analytics for Health Queue"](../../media/text-analytics-for-health-batch-async/job-status.png)

## Start using the sample

The .Net Core Client Console Application will send documents to the Azure Kubernetes Cluster. Open the .Net Core Console application in your IDE and change the `TextAnalyticsEndpoint` variable with the public IP of your Azure Kubernetes Cluster.

!["Screenshot of the .Net Core Application with the endpoint url"](../../media/text-analytics-for-health-batch-async/console-app.png)

For every request you can send up to 25 documents, with a max of 125 000 characters in total.
The Client application contains several synthetic patient documents that you can use to test the endpoint. When starting the [.Net Core Client Console Application](/samples/ta4h-container-e2e-sample/StressTestConsoleClient/) you will need to provide the number of requests and documents you want to send to the cluster.

!["Screenshot of the .Net Core Client Console Application with the number of requests and documents"](../../media/text-analytics-for-health-batch-async/client-console-application.png)

When all documents have been send to the cluster, you can track the proccess in the Azure Table Storage (`taJobs`)

!["Screenshot of the .Net Core Client Console Application with the number of requests and documents"](../../media/text-analytics-for-health-batch-async/console-app-finished.png)