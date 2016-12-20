<#
.SYNOPSIS 
A collection of functions to deploy to Azure. 

.DESCRIPTION
This code runs as VSTS (Visual Studio Azure PowerShell Script). The VSTS script requires that an Azure connection or principal be made.
So there is no need to login to Azure or select the subscription. These steps have already been done when we connected the step to Azure.

.EXAMPLE
.\Deploy.ps1 -clusterUrl localhost -appPackagePath "RateAggregatorApp\pkg" -appPkgName RateAggregatorAppTypePkg -appTypeName RateAggregatorAppType -version 1.0.0 -apps @{'ContosoRateAggregator' = @{'Service1' = @{'type' = 'WebServiceType'; 'name' = 'WebService'; 'model' = 'Stateless'; 'instances' = 1; 'partitions' = 0}; 'Service2' = @{'type' = 'RatesServiceType'; 'name' = 'RatesService'; 'model' = 'Stateful'; 'instances' = 3; 'partitions' = 4}}; 'FabricanRateAggregator' = @{'Service1' = @{'type' = 'WebServiceType'; 'name' = 'WebService'; 'model' = 'Stateless'; 'instances' = 1; 'partitions' = 0}; 'Service2' = @{'type' = 'RatesServiceType'; 'name' = 'RatesService'; 'model' = 'Stateful'; 'instances' = 3; 'partitions' = 4}}}

.LINK
No links
#>

<#
param
(
    [Parameter(Mandatory=$true, HelpMessage="Provide the URL i.e. localhost")]
    [string] $clusterUrl,

    [Parameter(Mandatory=$true, HelpMessage="Provide the app package path")]
    [string] $appPackagePath,

    [Parameter(Mandatory=$true, HelpMessage="Provide the application package name i.e. RateAggregatorAppTypePkg")]
    [string] $appPkgName,

    [Parameter(Mandatory=$true, HelpMessage="Provide the application type name i.e RateAggregatorAppType")]
    [string] $appTypeName,

    [Parameter(Mandatory=$true, HelpMessage="Provide the version number")]
    [string] $version,

    [Parameter(Mandatory=$true, HelpMessage="Provide a hashtable of the application names")]
    [hashtable] $apps
)

Function Connect-To-Cluster($clusterUrl)
{
	# Connect PowerShell session to a cluster
    Write-Host "connecting to cluster " $clusterUrl "...." -ForegroundColor green
	Connect-ServiceFabricCluster -ConnectionEndpoint ${clusterUrl}:19000
    Write-Host "connecting to cluster " $clusterUrl "succeeded!" -ForegroundColor green
}

Function Copy-N-Register($clusterUrl, $appPackagePath, $appPkgName, $version)
{
    Write-Host "App Package Path" $appPackagePath -ForegroundColor green
    Write-Host "App Package Name" $appPkgName -ForegroundColor green
	$imageStoreConnectionString = "file:C:\SfDevCluster\Data\ImageStoreShare"   # Use this with OneBox
	if ($clusterUrl -ne "localhost")
	{
		$imageStoreConnectionString = "fabric:ImageStore"   # Use this when not using OneBox
	}

	# Copy the application package to the cluster
	Copy-ServiceFabricApplicationPackage -ApplicationPackagePath "$appPackagePath\v$version" -ImageStoreConnectionString $imageStoreConnectionString -ApplicationPackagePathInImageStore $appPkgName

	# Register the application package's application type/version
	Register-ServiceFabricApplicationType -ApplicationPathInImageStore $appPkgName

	# After registering the package's app type/version, you can remove the package
	Remove-ServiceFabricApplicationPackage -ImageStoreConnectionString $imageStoreConnectionString -ApplicationPackagePathInImageStore $appPkgName
}

$imageStoreConnectionString = Connect-To-Cluster -clusterUrl $clusterUrl
Copy-N-Register -clusterUrl $clusterUrl -appPackagePath $appPackagePath -appPkgName $appPkgName -version $version
Write-Host "Copied and registered the package" -ForegroundColor green

$apps.Keys | % {
	$appName = $_
	$fabricAppName = "fabric:/$appName"
	# Create a named application from the registered app type/version
    Write-Host "Creating a named app instance " $fabricAppName -ForegroundColor green
	New-ServiceFabricApplication -ApplicationTypeName $appTypeName -ApplicationTypeVersion $version -ApplicationName $fabricAppName 

	$services = $apps.Item($_)
	$services.Keys | % {
		$serviceType = $services.Item($_).type
		$serviceName = $services.Item($_).name
		$serviceModel = $services.Item($_).model
		$serviceInstances = $services.Item($_).instances
        $servicePartitions = $services.Item($_).partitions

        Write-Host "Service type " $serviceType -ForegroundColor yellow
        Write-Host "Service name " $serviceName -ForegroundColor yellow
        Write-Host "Service model " $serviceModel -ForegroundColor yellow
        Write-Host "Service instances " $serviceInstances -ForegroundColor yellow
        Write-Host "Service paritions " $servicePartitions -ForegroundColor yellow

		$fabricServiceName = "$fabricAppName/$serviceName"
		# Create a named service within the named app from the service's type
        if ($serviceModel -eq "Stateless")
        {
            Write-Host "Creating a Stateless named service instance " $fabricServiceName -ForegroundColor green
		    New-ServiceFabricService -ApplicationName $appName -ServiceTypeName $serviceType -ServiceName $fabricServiceName -Stateless -PartitionSchemeSingleton -InstanceCount $serviceInstances
        }
        else
        {
            Write-Host "Creating a Stateful named service instance " $fabricServiceName -ForegroundColor green
		    New-ServiceFabricService -ApplicationName $appName -ServiceTypeName $serviceType -ServiceName $fabricServiceName -PartitionSchemeUniformInt64 $false -PartitionCount $servicePartitions -MinReplicaSetSize 1 -TargetReplicaSetSize $serviceInstances -LowKey 0 -HighKey 3
        }
	}
}
#>

$clusterUrl = "localhost"
$imageStoreConnectionString = "file:C:\SfDevCluster\Data\ImageStoreShare"   # Use this with OneBox
If ($clusterUrl -ne "localhost")
{
    $imageStoreConnectionString = "fabric:ImageStore"                       # Use this when not using OneBox
}

# Used only for the inmage store....it can be any name!!!
$appPkgName = "RateAggregatorAppTypePkg"

# Define the app and service types
$appTypeName = "RateAggregatorAppType"
$webServiceTypeName = "WebServiceType"
$ratesServiceTypeName = "RatesServiceType"

# Define the version
$version = "1.0.0"

# Connect PowerShell session to a cluster
Connect-ServiceFabricCluster -ConnectionEndpoint ${clusterUrl}:19000

# Copy the application package to the cluster
Copy-ServiceFabricApplicationPackage -ApplicationPackagePath "RateAggregatorApp\pkg\v$version" -ImageStoreConnectionString $imageStoreConnectionString -ApplicationPackagePathInImageStore $appPkgName

# Register the application package's application type/version
Register-ServiceFabricApplicationType -ApplicationPathInImageStore $appPkgName

# After registering the package's app type/version, you can remove the package
Remove-ServiceFabricApplicationPackage -ImageStoreConnectionString $imageStoreConnectionString -ApplicationPackagePathInImageStore $appPkgName

# Deploy the first aplication name (i.e. Contoso)
$appName = "fabric:/ContosoRateAggregatorApp"
$webServiceName = $appName + "/WebService"
$ratesServiceName = $appName + "/RatesService"

# Create a named application from the registered app type/version
New-ServiceFabricApplication -ApplicationTypeName $appTypeName -ApplicationTypeVersion $version -ApplicationName $appName -ApplicationParameter @{"RatesService_ProviderName" = "Contoso"}  

# Create a named service within the named app from the service's type
New-ServiceFabricService -ApplicationName $appName -ServiceTypeName $webServiceTypeName -ServiceName $webServiceName -Stateless -PartitionSchemeSingleton -InstanceCount 1

# Create a named service within the named app from the service's type
# For stateful services, it is important to indicate in the service manifest that the service is stateful and that it has a persisted state:
# <StatefulServiceType ServiceTypeName="RatesServiceType" HasPersistedState="true"/>
# Actually all of these switches are important on the PowerShell command:
# -PartitionSchemeUniformInt64 $true -PartitionCount 4 -MinReplicaSetSize 2 -TargetReplicaSetSize 3 -LowKey 0 -HighKey 3 -HasPersistedState
New-ServiceFabricService -ApplicationName $appName -ServiceTypeName $ratesServiceTypeName -ServiceName $ratesServiceName -PartitionSchemeUniformInt64 $true -PartitionCount 4 -MinReplicaSetSize 2 -TargetReplicaSetSize 3 -LowKey 0 -HighKey 3 -HasPersistedState

# Deploy the second aplication name (i.e. Fabrican)
$appName = "fabric:/FabricanRateAggregatorApp"
$webServiceName = $appName + "/WebService"
$ratesServiceName = $appName + "/RatesService"

# Create a named application from the registered app type/version
New-ServiceFabricApplication -ApplicationTypeName $appTypeName -ApplicationTypeVersion $version -ApplicationName $appName -ApplicationParameter @{"RatesService_ProviderName" = "Fabrican"}

# Create a named service within the named app from the service's type
New-ServiceFabricService -ApplicationName $appName -ServiceTypeName $webServiceTypeName -ServiceName $webServiceName -Stateless -PartitionSchemeSingleton -InstanceCount 1

# Create a named service within the named app from the service's type
New-ServiceFabricService -ApplicationName $appName -ServiceTypeName $ratesServiceTypeName -ServiceName $ratesServiceName -PartitionSchemeUniformInt64 $true -PartitionCount 4 -MinReplicaSetSize 2 -TargetReplicaSetSize 3 -LowKey 0 -HighKey 3 -HasPersistedState

