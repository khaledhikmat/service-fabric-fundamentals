$clusterUrl = "localhost"

# Define the app and service types
$applicationTypes = "RateAggregatorAppType"

# Connect PowerShell session to a cluster
Connect-ServiceFabricCluster -ConnectionEndpoint ${clusterUrl}:19000

# Remove all application names instances and their services
Get-ServiceFabricApplication | Where-Object { $applicationTypes -contains $_.ApplicationTypeName } | Remove-ServiceFabricApplication -Force
Get-ServiceFabricApplicationType | Where-Object { $applicationTypes -contains $_.ApplicationTypeName } | Unregister-ServiceFabricApplicationType -Force
