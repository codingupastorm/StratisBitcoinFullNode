#############################
#    UPDATE THESE VALUES    #
#############################
$ca_account = "1"
$ca_password = "4815162342"

$mnemonics = 
    "lava frown leave wedding virtual ghost sibling able mammal liar wide wisdom",
    "idle power swim wash diesel blouse photo among eager reward govern menu",
    "high neither night category fly wasp inner kitchen phone current skate hair"

#############################

$script_dir = Split-Path $script:MyInvocation.MyCommand.Path
$repo_folder = "$script_dir\.."
$root_datadir = "$script_dir\Data"
$daemon_name = "Stratis.TokenlessD"
$daemon_file = "$daemon_name.dll"
$path_to_tokenlessd = "$repo_folder\src\$daemon_name"
$path_to_tokenlessdll = "$path_to_tokenlessd\bin\Debug\netcoreapp2.1"
$node = $args[0]
$mnemonic = $mnemonics[$node - 1]
$node_root = "$root_datadir\node$node"
$port = 36200 + $node
$api_port = 30000 + $node
$long_interval_time = 10

# Create the folder in case it doesn't exist.
New-Item -ItemType directory -Force -Path $node_root
New-Item -ItemType directory -Force -Path $node_root\tokenless
New-Item -ItemType directory -Force -Path $node_root\tokenless\TokenlessMain

Copy-Item $root_datadir\ca\CaMain\CaCertificate.crt -Destination $node_root\tokenless\TokenlessMain\CaCertificate.crt
Copy-Item $path_to_tokenlessdll\*.* -Destination $node_root

cd $node_root
Write-Host "Running Stratis.TokenlessD..." -foregroundcolor "magenta"

$bootstrap = $args[1]

# Require initial run to create files?
$initial = ""

$node_data = "$node_root\tokenless\TokenlessMain"
$client_certificate = "$node_data\ClientCertificate.pfx"
$federation_key_file = "$node_data\miningKey.dat"
$transaction_key_file = "$node_data\transactionSigning.dat"
$wallet_file = "$node_data\nodeid.json"

if (!(Test-Path $client_certificate) -or !(Test-Path $federation_key_file) -or !(Test-Path $transaction_key_file) -or !(Test-Path $wallet_file))
{
    $initial = "& dotnet $daemon_file -datadir=""$node_root"" -port=$port -apiport=$api_port -password=test -capassword=test -certificatepassword=test -mnemonic=""$mnemonic"""
}

# Run
start-process cmd -ArgumentList "/k color 0E $initial && dotnet $daemon_file -bootstrap=$bootstrap -datadir=""$node_root"" -port=$port -apiport=$api_port -capassword=$ca_password -caaccountid=$ca_account -certificatepassword=test -iprangefiltering=0 -whitelist=0.0.0.0 -addnode=127.0.0.1:36201 -addnode=127.0.0.1:36202 -addnode=127.0.0.1:36203"
