$clusterUrl = "localhost"
$imageStoreConnectionString = "file:C:\SfDevCluster\Data\ImageStoreShare"   # Use this with OneBox
If ($clusterUrl -ne "localhost")
{
    $imageStoreConnectionString = "fabric:ImageStore"                       # Use this when not using OneBox
}

# Used only for the inmage store....it can be any name!!!
$appPkgName = "RateAggregatorAppTypePkg"

# Define the new version
$version = "1.1.0"

# Connect PowerShell session to a cluster
Connect-ServiceFabricCluster -ConnectionEndpoint ${clusterUrl}:19000

# Copy the application package to the cluster
Copy-ServiceFabricApplicationPackage -ApplicationPackagePath "RateAggregatorApp\pkg\v$version" -ImageStoreConnectionString $imageStoreConnectionString -ApplicationPackagePathInImageStore $appPkgName

# Register the application package's application type/version
Register-ServiceFabricApplicationType -ApplicationPathInImageStore $appPkgName

# After registering the package's app type/version, you can remove the package
Remove-ServiceFabricApplicationPackage -ImageStoreConnectionString $imageStoreConnectionString -ApplicationPackagePathInImageStore $appPkgName

# Upgrade the first aplication name (i.e. Contoso)
$appName = "fabric:/ContosoRateAggregatorApp"

# Upgrade the application to the new version
Start-ServiceFabricApplicationUpgrade -ApplicationName $appName -ApplicationTypeVersion $version -Monitored -UpgradeReplicaSetCheckTimeoutSec 100

# Upgrade the second aplication name (i.e. Fabrican)
$appName = "fabric:/FabricanRateAggregatorApp"

# Upgrade the application to the new version
Start-ServiceFabricApplicationUpgrade -ApplicationName $appName -ApplicationTypeVersion $version -Monitored -UpgradeReplicaSetCheckTimeoutSec 100
