# Intelligent Dashboard powered by Text Analytics for Health

The Intelligent Dashboard is designed to help healthcare professionals and organizations efficiently gather insights from unstructured healthcare information. By utilizing [Text Analytics for Health](https://learn.microsoft.com/azure/azure-health-insights), our tool is able to receive unstructured healthcare information and receive structured insights. The insights generated from Text Analytics for Health are then used to create population insights dashboards, which are accessible in a single PowerApp. With this innovative solution, healthcare professionals can easily access and analyze data in real-time. Say goodbye to the tedious process of manually analyzing healthcare data and hello to our Intelligent Dashboard  sample!

**Upload your documents with the help of a customizable Power App**
!["A screenshot of the Intelligent Dashboard PowerApp - Document screen"](/media/intelligent-dashboard-ta4h/document-uploader.png)

**Get insights with our population insights dashboard, that can be modified to your needs**
!["A screenshot of the Intelligent Dashboard PowerApp - PowerBI screen"](/media/intelligent-dashboard-ta4h/dashboard.png)


## Architecture
!["A diagram of the Intelligent dashboard architecture"](/media/intelligent-dashboard-ta4h/Architecture.png)

### Architecture Flow
1. Upload one or more documents via the Power App.
2. The document(s) are uploaded to a Azure Storage Account
3. The Azure Function picks up the uploaded text files
4. The Azure Function sends the unstructured documents to Text Analytics for Health for processing
5. The results, in FHIR format, are being pushed to the Azure Health Data Service
6. The PowerBI provides insights from the unstructured data, via the FHIR connector

# Getting started

## Setup Azure Resources

### Creating an App Registration

**Before Deploying to Azure:**
1. Go to "Azure Active Directory"
2. Click App registrations
3. Create new registration
4. Create new secret
5. Copy ClientID and ClientSecret for the deployment step in Azure

### Deploy the Azure Resources

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fmicrosoft%2Fazure-health-AI-services-samples%2Fmain%2Fsamples%2Fintelligent-dashboard-ta4H%2Fazuredeploy.json)

### Give the new App Registration access to theAzure Health Data FHIR Service

After deployment Add the "Fhir Contributor" Role for the newly created "App regestration" in "Azure Health Data FHIR Service" resource. More info can be found [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/configure-azure-rbac)

## Setup and Configure the Power BI Report

1. Download the Power BI pbix [here](/samples/intelligent-dashboard-ta4H/Power%20BI%20Dashboard/Clinical%20Insights%20FHIR.pbix). 
2. Open the Power BI dashboard with [Power BI Destkop](https://powerbi.microsoft.com/desktop/) 
3. Click on **Transform data**, select a FHIR table and click on **Advanced Editor**
4. Change the FHIR url with the endpoint of your FHIR endpoint
!["A diagram of the Intelligent dashboard architecture"](/media/intelligent-dashboard-ta4h/setup-powerbi-intelligent-dashboard.png)
5. Publish the Power BI Dashboard to your workspace, to learn how to do this, click [here](https://learn.microsoft.com/power-bi/create-reports/desktop-upload-desktop-files)


## Setup and Configure the Power App

1. Download the Power App [here](/samples/intelligent-dashboard-ta4H/Power%20App/Intelligent%20Dashboard.msapp)
2. Import the Power App in your Power Platform environment by going to Make.PowerPlatform.com, go to **Apps** and click on **Import Canvas App**
3. Open the Intelligent Dashboard Power App.
4. Connect your Storage Account to the PowerApp on importing
5. Make sure to update the **OnVisible** property of the **OverviewScreen** with the right ID of your Folder. The same goes for the Upload Documents button. 
More info can be found [here](https://learn.microsoft.com/power-apps/maker/canvas-apps/connections/connection-azure-blob-storage)
5. Click on the **VisualizerScreen** on the left side of the screen and select the **Power BI** element.
6. Update the TitleUri with the Published Web URI from Powerbi. You can find detailed instructions [here](https://learn.microsoft.com/power-bi/collaborate-share/service-publish-to-web) on how to do this.
!["A diagram of the Intelligent dashboard architecture"](/media/intelligent-dashboard-ta4h/connect-powerbi-to-powerapp.png)