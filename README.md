#Seagate Log Ingestion to Azure Monitor

This sample project provide an intergation betweeen Seagate LyveCloud storage logs and Azure Monitor (Azure Log Analytics).
The application is build using Azure Functions that periodically scan for new log files in LyveCloud and then stream them into Azure Log Analytics workspace.

# Prerequisites
1. Create a Log Analytics workspace in Azure - https://portal.azure.com/#blade/HubsExtension/BrowseResource/resourceType/Microsoft.OperationalInsights%2Fworkspaces
2. Copy the Workspace ID and Primary/Secondary key of your workspace from the Agents management settings


# Deployment 
0. Set the settings inside the appsettings.json file (or set the values in Azure after the deployment)
1. In VS, right click on the project SegateLogScanner and select Publish.
2. Choose Azure as the publish target and set the Azure Storage dependency

# Settings
The app use the following setting which you can set inside the appsettings.json or inside Azure Functions config
* LyveUrl - the LyveCloud REST API base url (e.g. https://s3.us-east-1.lyvecloud.seagate.com)
* LyveAccessKey - The Access Key to the LyveCloud API
* LyveSecretKey - The Secret Key to the LyveCloud API
* BucketName - The name of the Bucket in LyveCloud that contains the logs
*
* LogAnalyticsWorkspaceID - the workspace id of the Azure Log Analytics workspace (can be found inside the Agents management settings of the workspace)
* LogAnalyticsKey - the Log Analytics Primary/Secondary key (can be found inside the Agents management settings of the workspace)
* LogAnalyticsLogName - The name of the log inside the Log Analytics workspace to which the LyveCloud Logs will be ingested
* LogAnalyticsTimestampField": The name of the field from the LyveCloud logs which should be used as the timestamp (e.g. auditEntry_time)

# Sample Log Analytics Queries
Once the logs are inside the Azure Log Analytics you can run various queries and create visualization 
The name LyveLogs_CL will be used in the examples as the name of the log inside Azure Log Analytics


## Objects usage over time 
```
LyveLogs_CL| 
where auditEntry_api_bucket_s<>"" |
summarize count() by bin(auditEntry_time_t, 5m), auditEntry_api_bucket_s, auditEntry_api_object_s
```


## Bucket access chart
```
LyveLogs_CL
|where auditEntry_api_bucket_s<>"" 
|summarize count() by bin(auditEntry_time_t, 30m), auditEntry_api_bucket_s, auditEntry_api_object_s
|render columnchart 
```

## AverageTimeForFirstByte chart 
```
LyveLogs_CL
| summarize AverageTimeForFirstByte=avg(toint(replace(@'ns', @'', auditEntry_api_timeToFirstByte_s))) by bin(auditEntry_time_t, 1hr )
|render timechart 
```