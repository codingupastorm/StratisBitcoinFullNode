rmdir "%~dp0\Data" /s /q

powershell.exe -File "RunCA.ps1"
powershell.exe -File "RunNode.ps1" 1 1 1
powershell.exe -File "RunNode.ps1" 2 0 1
powershell.exe -File "RunNode.ps1" 3 0 1

