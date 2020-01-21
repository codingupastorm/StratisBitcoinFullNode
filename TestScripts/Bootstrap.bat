rmdir c:\test /s /q %~dp0\Data

powershell.exe -File "RunCA.ps1"
powershell.exe -File "RunNode.ps1" 1 1
powershell.exe -File "RunNode.ps1" 2 0
powershell.exe -File "RunNode.ps1" 3 0

