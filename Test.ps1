Function Generate-RateRequests($appName = 'Contoso', $iterations = 20)
{
    Try {
        Write-Host "Generating $iterations random rate requests against $appName ...." -ForegroundColor Green

        $url = "Http://localhost:8082/$appName" + "RateAggregatorApp/api/requests"

        foreach($i in 1..$iterations)
        {
            $checkInDate = get-date -Year (get-random -minimum 2012 -maximum 2016) -Month (get-random -minimum 1 -maximum 12) -Day (get-random -minimum 1 -maximum 28)
            $nights = get-random -minimum 1 -maximum 30
            $checkOutDate = $checkInDate.AddDays($nights)
            $hotelId = get-random -input "1", "2", "3" -count 1
            $body = @{
                CheckInDate = get-date $checkInDate -Format "yyy-MM-ddTHH:mm:ss";
                CheckOutDate = get-date $checkOutDate -Format "yyy-MM-ddTHH:mm:ss";
                HotelId = $hotelId;
                HotelName = "Hotel$hotelId";
                City = "City$hotelId";
                Country = get-random -input "USA", "USA", "USA", "CAN", "CAN", "CAN", "AUS", "AUS", "AUS", "FRA", "GER", "UAE" -count 1
            }
            Write-Host "This is the JSON we are generating for iteration # $i...." -ForegroundColor yellow
            $json = ConvertTo-Json $body -Depth 3
            $json

	        $result = Invoke-RestMethod -Uri $url -Headers @{"Content-Type"="application/json" } -Body $json -Method POST -TimeoutSec 600
        }        
    } Catch {
        Write-Host "Failure message: $_.Exception.Message" -ForegroundColor red
        Write-Host "Failure stack trace: $_.Exception.StackTrace" -ForegroundColor red
        Write-Host "Failure inner exception: $_.Exception.InnerException" -ForegroundColor red
    }
}

Function View-QueueLength($appName = 'Contoso')
{
    Try {
        Write-Host "View Queue Length for $appName...." -ForegroundColor Green

        $url = "Http://localhost:8082/$appName" + "RateAggregatorApp/api/stats/queue/length"
	    $result = Invoke-RestMethod -Uri $url -Headers @{"Content-Type"="application/json" } -Method GET -TimeoutSec 600
        $json = ConvertTo-Json $result -Depth 3
        $json
    } Catch {
        Write-Host "Failure message: $_.Exception.Message" -ForegroundColor red
        Write-Host "Failure stack trace: $_.Exception.StackTrace" -ForegroundColor red
        Write-Host "Failure inner exception: $_.Exception.InnerException" -ForegroundColor red
    }
}

Function View-Cities($appName = 'Contoso')
{
    Try {
        Write-Host "View cities for $appName...." -ForegroundColor Green

        $url = "Http://localhost:8082/$appName" + "RateAggregatorApp/api/stats/cities"
	    $result = Invoke-RestMethod -Uri $url -Headers @{"Content-Type"="application/json" } -Method GET -TimeoutSec 600
        $json = ConvertTo-Json $result -Depth 3
        $json
    } Catch {
        Write-Host "Failure message: $_.Exception.Message" -ForegroundColor red
        Write-Host "Failure stack trace: $_.Exception.StackTrace" -ForegroundColor red
        Write-Host "Failure inner exception: $_.Exception.InnerException" -ForegroundColor red
    }
}

Generate-RateRequests -appName Contoso -iterations 100
Generate-RateRequests -appName Fabrican -iterations 100

View-QueueLength -appName Contoso
View-QueueLength -appName Fabrican

View-Cities -appName Contoso
View-Cities -appName Fabrican
