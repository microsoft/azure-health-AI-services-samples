# Azure Health AI Services Samples

This repository contains different samples applications and code to help you get started with the different Microsoft Health-AI services.
Based on these samples you will learn how to use our services, and accelerate your implementations.

This project hosts open-source samples for services developed by the Microsoft Health-AI team. 
To learn more about the Health-AI Services, please refer to the managed service documentation: 

- Azure Health bot [Documentation](https://learn.microsoft.com/azure/health-bot/)
- Azure Health Insights [Documentation](https://learn.microsoft.com/azure/azure-health-insights)
- Text Analytics for Health [Documentation](https://learn.microsoft.com/en-us/azure/cognitive-services/language-service/text-analytics-for-health/overview?tabs=ner)



# Samples

This project provides samples outlining implementations of various use cases across health. 
Currently we have 4 samples

|Sample|Technology Components||
| --- | --- | --- | 
| Intelligent Dashboard powered by Text Analytics for Health | TA4H - PowerApps - PowerBI - FHIR | [I want to try this sample](/samples/intelligent-dashboard-ta4H/README.md) | 
| Text Analytics for Health Container Async sample | TA4H - AKS | [I want to try this sample](/samples/ta4h-container-e2e-sample/README.md) | 
| Text Analytics for Health Adaptive Client Application | TA4H - ACI - C# |  [I want to try this sample](/samples/ta4h-adaptive-client-blueprint/README.md) | 
| Day in the life of a Nurse | TA4H - PowerApps - Nuance - FHIR | [I want to try this sample](https://github.com/microsoft/nurseempowerment) | 
| Trials Matching integration into Azure Health Bot  | Azure Health Bot - Azure Health Insights - Clinical Trial Matcher |  [I want to try this sample](https://github.com/microsoft/ClinicalTrialsBlueprint) | 


## 1. Intelligent Dashboard powered by Text Analytics for Health [TA4H - PowerApps - PowerBI - FHIR]

The Intelligent Dashboard is designed to help healthcare professionals and organizations efficiently gather insights from unstructured healthcare information. By utilizing [Text Analytics for Health] (https://learn.microsoft.com/azure/azure-health-insights), the sample application is able to receive unstructured healthcare information and generate structured insights. The insights generated from Text Analytics for Health are then used to create population insights dashboards, which are accessible in a single Power App. With this innovative sample, healthcare professionals can easily access and analyze data in real-time. Say goodbye to the tedious process of manually analyzing healthcare data and hello to our Intelligent Dashboard  sample!

!["A screenshot of the Intelligent Dashboard PowerApp - PowerBI screen"](/media/intelligent-dashboard-ta4h/dashboard.png)

[I want to try this sample](/samples/intelligent-dashboard-ta4H/README.md)

## 2. Text Analytics for Health Container Async sample [TA4H - AKS]

This sample provides code examples and best practices on how to use the containerized version of Text Analytics for Health in a scalable way.
After completing the tutorial you will have: 
- A Kubernetes cluster that can scale with one or many Text Analytics for Health Nodes.
- Several supporting services such as storage accounts, Queues, ... 

This can be setup through a guided tutorial with several deployment scripts. With the goal to be used in an scalable and asynchronous way.

!["A screenshot of the Intelligent Dashboard PowerApp - PowerBI screen"](/media/text-analytics-for-health-batch-async/architecture.jpg)

[I want to try this sample](/samples/ta4h-container-e2e-sample/README.md)

## 3. Text Analytics for Health Adaptive Client Application [TA4H - ACI - C#]

This sample provides code and setup instructions for processing large volumes of medical data using Text Analytics for Health (TA4H). While the TextAnalytics SDKs offer a convenient way to call the TextAnalytics API and work with the analysis results programmatically, processing large volumes of data requires a solution optimized for efficiency and throughput. This sample is designed to minimize overall processing time for a large number of documents, considering server capacity and service limits (i.e., max allowed API calls per minute). This client application can work with the Managed Azure Hosted service and the Containerized setup of Text Analytics for Health.

[I want to try this sample](/samples/ta4h-adaptive-client-blueprint/README.md)

## 4. Day in the life of a Nurse [TA4H - PowerApps - Nuance - FHIR]

This repository contains several open-source example [Power Apps](https://make.powerapps.com/) which were created based on a study called 'The Day in the Life of a Nurse'. One of the outcomes were several minimal viable products that could support nurses in their daily job. These starter Power Apps solutions are enhanced with [Nuance Speech to Text](https://www.nuancehealthcaredeveloper.com/?q=Dragon-Medical-SpeechKit-Home), and utilize [Text Analytics for Health](https://docs.microsoft.com/en-us/azure/cognitive-services/language-service/text-analytics-for-health/overview ) for medical structuring. The data is being served from [FHIR API](https://docs.microsoft.com/en-us/azure/healthcare-apis/healthcare-apis-overview) and utilize the [FHIRBase](https://docs.microsoft.com/en-us/connectors/fhirbase/) and [FHIRClinical](https://docs.microsoft.com/en-us/connectors/fhirclinical/) Power Platform connectors. The application can also be linked to [Microsoft Shifts](https://support.microsoft.com/en-us/office/get-started-in-shifts-5f3e30d8-1821-4904-be26-c3cd25a497d6) where you can get real-time shift info from your colleagues.


!["A screenshot of the easy reporting power app for nurses"](/media/day-in-the-life-of-a-nurse/easy-reporting.png)

[I want to try this sample](https://github.com/microsoft/nurseempowerment)

## 5. Trials Matching integration into Azure Health Bot [Azure Health Bot - Azure Health Insights - Clinical Trial Matcher]

This repository allows you to deploy a [Clinical Trials Matching Bot](https://learn.microsoft.com/en-us/azure/azure-health-insights/trial-matcher/overview#azure-health-bot-integration). It uses the Trial Matcher engine, which is an AI model offered within Project Health Insights. Trial Matcher is designed to match patients to potentially suitable clinical trials or find a group of potentially eligible patients to a list of clinical trials. Read more about Azure Trial Matcher [here](https://learn.microsoft.com/en-us/azure/azure-health-insights/trial-matcher/overview). This sample will generate an Azure Health Bot with built-in Clinical Trial Matching integration, which enables end-user to find relevant clinical trials for their conditions, based on eligibility.

!["A screenshot that shows an example on how to use the Trial Matcher in the Azure Health Bot"](/media/azure-health-insights-integrated-healthbot/example-chat-scenario.png)

[I want to try this sample](https://github.com/microsoft/ClinicalTrialsBlueprint)


## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Disclaimers

The Health-AI Samples is an open-source project. It is not a managed service, and it is not part of the Health-AI Services. The sample apps and sample code provided in this repo are used as examples only. You bear sole responsibility for compliance with applicable law and for any data you use when using these samples. Please review the information and licensing terms on this GitHub website before using the Health-AI Services Samples repo. 

The Azure Health-AI Samples Github repo is intended only for use in transferring and formatting data. It is not intended for use as a medical device or to perform any analysis or any medical function and the performance of the software for such purposes has not been established. You bear sole responsibility for any use of this software, including incorporation into any product intended for a medical purpose. 

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
