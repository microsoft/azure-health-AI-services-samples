# Text Analytics for Health Container Async Batch Usage

To start interacting with the cluster you can send HTTP requests, up to 25 documents, with a max of 125 000 characters in total, per request to the Azure Function.
You do this by sending one or more HTTP POST requests to the Client Azure Function

A C# and Curl Example can be found below 

```C#
var client = new HttpClient();
var request = new HttpRequestMessage(HttpMethod.Post, "https://function-YOUR-FUNCTION-NAME.azurewebsites.net/api/RuntTA4HWorkloadFunction?code=Q==");
var content = new StringContent("[\r\n    {\r\n        \"id\": \"1\",\r\n        \"text\": \"The patient is taking Metformin 500mg daily for diabetes\"\r\n    },\r\n    {\r\n        \"id\": \"2\",\r\n        \"text\":   \"The patient has been diagnosed with hypertension and is taking Amlodipine 5mg daily\"\r\n    }\r\n]", null, "application/json");
request.Content = content;
var response = await client.SendAsync(request);
response.EnsureSuccessStatusCode();
Console.WriteLine(await response.Content.ReadAsStringAsync());
```


```cURL
curl --location 'https://function-ta4h-test.azurewebsites.net/api/RuntTA4HWorkloadFunction?code=QT3_Lcgr0U-G66H2fIbogv5o4NrNpA9secMxgOydt3JmAzFuQk1eKQ%3D%3D' \
--header 'Content-Type: application/json' \
--data '[
    {
        "id": "1",
        "text": "The patient is taking Metformin 500mg daily for diabetes"
    },
    {
        "id": "2",
        "text":   "The patient has been diagnosed with hypertension and is taking Amlodipine 5mg daily"
    }
]'
```

the payload of the message is a JSON object that contains an `id` and `text` property

```JSON
[
    {
        "id": "1",
        "text": "The patient is taking Metformin 500mg daily for diabetes"
    },
    {
        "id": "2",
        "text":   "The patient has been diagnosed with hypertension and is taking Amlodipine 5mg daily"
    }
]
``` 

You can, but don't need to wait on the response of the HTTP request.
When the function has rocessed the documents, every documents and associated results will be stored on your storage account. 
For every document in your request, there will be a seperate file. 
All results are stored in a container named `healthcareentitiesresults` on the storage account defined when creating your Azure Function.
The naming convention of the file is the current DateTime followed with an underscore and the `id` of the document 

!["A screenshot of storage account container"](/media/text-analytics-for-health-batch-async/storage-account-container.png)

every file should contain the following structure


```JSON
{
    "id": "1",
    "text": "The patient is taking Metformin 500mg daily for diabetes",
    "healthcareEntitiesResult": [
        {
            "Text": "Metformin",
            "Category": {},
            "SubCategory": null,
            "ConfidenceScore": 1.0,
            "Offset": 22,
            "Length": 9,
            "DataSources": [
                {
                    "EntityId": "C0025598",
                    "Name": "UMLS"
                },
                {
                    "EntityId": "A10BA02",
                    "Name": "ATC"
                },
                {
                    "EntityId": "0000008019",
                    "Name": "CHV"
                },
                {
                    "EntityId": "4007-0083",
                    "Name": "CSP"
                },
                {
                    "EntityId": "DB00331",
                    "Name": "DRUGBANK"
                },
                {
                    "EntityId": "4442",
                    "Name": "GS"
                },
                {
                    "EntityId": "sh2007006278",
                    "Name": "LCH_NW"
                },
                {
                    "EntityId": "LP33332-5",
                    "Name": "LNC"
                },
                {
                    "EntityId": "d03807",
                    "Name": "MMSL"
                },
                {
                    "EntityId": "D008687",
                    "Name": "MSH"
                },
                {
                    "EntityId": "9100L32L2N",
                    "Name": "MTHSPL"
                },
                {
                    "EntityId": "C61612",
                    "Name": "NCI"
                },
                {
                    "EntityId": "004534",
                    "Name": "NDDF"
                },
                {
                    "EntityId": "x01Li",
                    "Name": "RCD"
                },
                {
                    "EntityId": "6809",
                    "Name": "RXNORM"
                },
                {
                    "EntityId": "372567009",
                    "Name": "SNOMEDCT_US"
                },
                {
                    "EntityId": "4023979",
                    "Name": "VANDF"
                }
            ],
            "Assertion": null,
            "NormalizedText": "metformin"
        },
        {
            "Text": "500mg",
            "Category": {},
            "SubCategory": null,
            "ConfidenceScore": 1.0,
            "Offset": 32,
            "Length": 5,
            "DataSources": [],
            "Assertion": null,
            "NormalizedText": null
        },
        {
            "Text": "daily",
            "Category": {},
            "SubCategory": null,
            "ConfidenceScore": 1.0,
            "Offset": 38,
            "Length": 5,
            "DataSources": [],
            "Assertion": null,
            "NormalizedText": null
        },
        {
            "Text": "diabetes",
            "Category": {},
            "SubCategory": null,
            "ConfidenceScore": 1.0,
            "Offset": 48,
            "Length": 8,
            "DataSources": [
                {
                    "EntityId": "C0011849",
                    "Name": "UMLS"
                },
                {
                    "EntityId": "DIABT",
                    "Name": "AIR"
                },
                {
                    "EntityId": "0000005999",
                    "Name": "AOD"
                },
                {
                    "EntityId": "BI00008",
                    "Name": "BI"
                },
                {
                    "EntityId": "1018264",
                    "Name": "CCPSS"
                },
                {
                    "EntityId": "0000003834",
                    "Name": "CHV"
                },
                {
                    "EntityId": "230",
                    "Name": "COSTAR"
                },
                {
                    "EntityId": "0862-6160",
                    "Name": "CSP"
                },
                {
                    "EntityId": "DIABETES MELL",
                    "Name": "CST"
                },
                {
                    "EntityId": "U000960",
                    "Name": "DXP"
                },
                {
                    "EntityId": "HP:0000819",
                    "Name": "HPO"
                },
                {
                    "EntityId": "E10-E14.9",
                    "Name": "ICD10"
                },
                {
                    "EntityId": "E10-E14.9",
                    "Name": "ICD10AM"
                },
                {
                    "EntityId": "E08-E13",
                    "Name": "ICD10CM"
                },
                {
                    "EntityId": "250",
                    "Name": "ICD9CM"
                },
                {
                    "EntityId": "T90",
                    "Name": "ICPC"
                },
                {
                    "EntityId": "T90002",
                    "Name": "ICPC2P"
                },
                {
                    "EntityId": "MTHU020781",
                    "Name": "LNC"
                },
                {
                    "EntityId": "10012601",
                    "Name": "MDR"
                },
                {
                    "EntityId": "30479",
                    "Name": "MEDCIN"
                },
                {
                    "EntityId": "4",
                    "Name": "MEDLINEPLUS"
                },
                {
                    "EntityId": "D003920",
                    "Name": "MSH"
                },
                {
                    "EntityId": "U000263",
                    "Name": "MTH"
                },
                {
                    "EntityId": "250.0",
                    "Name": "MTHICD9"
                },
                {
                    "EntityId": "00385",
                    "Name": "NANDA-I"
                },
                {
                    "EntityId": "C2985",
                    "Name": "NCI"
                },
                {
                    "EntityId": "MTHU036798",
                    "Name": "OMIM"
                },
                {
                    "EntityId": "CDR0000685852",
                    "Name": "PDQ"
                },
                {
                    "EntityId": "13970",
                    "Name": "PSY"
                },
                {
                    "EntityId": "R0121582",
                    "Name": "QMR"
                },
                {
                    "EntityId": "C10..",
                    "Name": "RCD"
                },
                {
                    "EntityId": "D-2381",
                    "Name": "SNM"
                },
                {
                    "EntityId": "DB-61000",
                    "Name": "SNMI"
                },
                {
                    "EntityId": "73211009",
                    "Name": "SNOMEDCT_US"
                },
                {
                    "EntityId": "0371",
                    "Name": "WHO"
                }
            ],
            "Assertion": null,
            "NormalizedText": "Diabetes Mellitus"
        }
    ]
}
``` 