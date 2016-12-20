$clusterUrl = "localhost"

# Deploy the first aplication name (i.e. Contoso)
$appName = "fabric:/ContosoRateAggregatorApp"
$webServiceName = $appName + "/WebService"

# Dynamically change the named service's number of instances (the cluster must have at least 5 nodes)
Update-ServiceFabricService -ServiceName $webServiceName -Stateless -InstanceCount 5 -Force

# Deploy the first aplication name (i.e. Fabrican)
$appName = "fabric:/FabricanRateAggregatorApp"
$webServiceName = $appName + "/WebService"

# Dynamically change the named service's number of instances (the cluster must have at least 5 nodes) 
Update-ServiceFabricService -ServiceName $webServiceName -Stateless -InstanceCount 5 -Force
