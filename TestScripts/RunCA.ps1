#############################
#    UPDATE THESE VALUES    #
#############################
$ca_account = "1"
$ca_password = "4815162342"

$mnemonic = "young shoe immense usual faculty edge habit misery swarm tape viable toddler"
$mnemonicPassword = "node"
$coinType = 500
$addressPrefix = 0
#############################

$interval_time = 5
$long_interval_time = 10

$script_dir = Split-Path $script:MyInvocation.MyCommand.Path
$repo_folder = "$script_dir\.."
$path_to_ca = "$repo_folder\src\CertificateAuthority.API"
$root_datadir = "$script_dir\Data"
$ca_root = "$root_datadir"

cd $path_to_ca
Write-Host "Running CertificateAuthority.API..." -foregroundcolor "magenta"
start-process cmd -ArgumentList "/k color 0E && dotnet run -datadir=""$ca_root"""
timeout $long_interval_time

Write-Host "Initializing CA..." -foregroundcolor "magenta"
$params = @{ "mnemonic" = "$mnemonic"; "mnemonicPassword" = "$mnemonicPassword"; "coinType" = $coinType; "addressPrefix" = $addressPrefix; "accountId" = $ca_account; "adminPassword" = "$ca_password" }
Write-Host ($params|ConvertTo-Json)
Invoke-WebRequest -Uri https://localhost:5001/api/certificates/initialize_ca -Method post -Body ($params|ConvertTo-Json) -ContentType "application/json-patch+json"
timeout $interval_time