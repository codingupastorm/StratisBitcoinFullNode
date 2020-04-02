$interval_time = 5
$long_interval_time = 10

$script_dir = Split-Path $script:MyInvocation.MyCommand.Path
$repo_folder = "$script_dir\.."
$path_to_msd = "$repo_folder\src\MembershipServices.Cli"
$root_datadir = "$script_dir\Data\msd"
$msd_root = "$root_datadir"

cd $path_to_msd
Write-Host "Running MerbershipServices.Cli..." -foregroundcolor "magenta"
start-process cmd -ArgumentList "/k color 0E && dotnet run generate --datadir=""$msd_root"" --commonname=TestName --organization=TestOrganization --organizationunit=TestOrganizationUnit --locality=London --stateorprovince=London --emailaddress=test@example.com --country=UK --password=test --requestedpermissions Send Mine"
timeout $long_interval_time

Write-Host "Completed..." -foregroundcolor "magenta"
timeout $interval_time
