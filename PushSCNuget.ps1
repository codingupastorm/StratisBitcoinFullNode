$packageNames = @("Stratis.Bitcoin", "Stratis.Bitcoin.Features.Consensus", "Stratis.Bitcoin.Features.PoA", "Stratis.Bitcoin.Features.SmartContracts", "Stratis.SmartContracts.CLR", "Stratis.SmartContracts.CLR.Validation", "Stratis.SmartContracts.Networks", "Stratis.SmartContracts.Core")

# A little gross to have to enter src/ and then go back after, but this is where the file is atm 
cd "src"

foreach ($packageName in $packageNames){
	cd $packageName
	rm "bin\debug\" -Recurse -Force -ErrorAction Ignore
	dotnet pack --configuration Debug --include-source --include-symbols 
	Copy-Item -Path "C:\Users\jorda\source\repos\StratisBitcoinFullNode\CredentialProvider.VSS.exe" -Destination "bin\Debug"
	Copy-Item -Path "C:\Users\jorda\source\repos\StratisBitcoinFullNode\nuget.exe" -Destination "bin\Debug"
	cd "bin\Debug"
	./nuget.exe push -Source "SmartContractsNuGet" -ApiKey AzureDevOps "*.symbols.nupkg"
	cd ..
	cd ..
	cd ..
}

cd ..
