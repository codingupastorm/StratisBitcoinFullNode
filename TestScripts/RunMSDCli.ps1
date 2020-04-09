$node = $args[0]

$interval_time = 5
$long_interval_time = 10

$script_dir = Split-Path $script:MyInvocation.MyCommand.Path
$repo_folder = "$script_dir\.."
$path_to_msd = "$repo_folder\src\MembershipServices.Cli"
$root_datadir = "$script_dir\Data\"
$msd_root = "$root_datadir\msd"

$admin_account_id = "1"
$admin_password = "4815162342"

$organization = "Stratis"
$organizationUnit = "TestScripts"
$locality = "London"
$state = "London"

# Create CA account

$params = @{ "commonName" = "node$node"; "newAccountPasswordHash" = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08"; "requestedAccountAccess" = 255; "organizationUnit" = $organizationUnit; "organization" = $organization; "locality" = $locality; "stateOrProvince" = $state; "emailAddress" = "node$node@example.com"; "country" = "UK"; "requestedPermissions" = @(@{"name" = "Send"}, @{"name" = "CallContract"}, @{"name" = "CreateContract"}) }
Write-Host ($params|ConvertTo-Json)
$result = Invoke-WebRequest -Uri https://localhost:5001/api/accounts/create -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"

$ca_account = $result|ConvertFrom-Json
$ca_account = [convert]::ToInt32($ca_account)
$admin_account_id = [convert]::ToInt32($admin_account_id)

timeout 1

# Approve the created account

$params = @{ "accountId" = $admin_account_id; "password" = $admin_password; "targetAccountId" = $ca_account }
Write-Host ($params|ConvertTo-Json)
Invoke-WebRequest -Uri https://localhost:5001/api/accounts/approve -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json"

Copy-Item $root_datadir\ca\CaMain\CaCertificate.crt -Destination $msd_root\tokenless\TokenlessMain\CaCertificate.crt

cd $path_to_msd
Write-Host "Running MerbershipServices.Cli..." -foregroundcolor "magenta"
start-process cmd -ArgumentList "/k color 0E && dotnet run generate --datadir=""$msd_root"" --caaccountid=$ca_account --commonname=node$node --organization=$organization --organizationunit=$organizationUnit --locality=$locality --stateorprovince=$state --emailaddress=test@example.com --country=UK --capassword=test --password=test --requestedpermissions Send CallContract CreateContract"
timeout $long_interval_time

Write-Host "Completed..." -foregroundcolor "magenta"
timeout $interval_time
