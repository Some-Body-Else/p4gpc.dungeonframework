# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/p4gpc.dungeonloader/*" -Force -Recurse
dotnet publish "./p4gpc.dungeonloader.csproj" -c Release -o "$env:RELOADEDIIMODS/p4gpc.dungeonloader" /p:OutputPath="./bin/Release" /p:RobustILLink="true"

# Restore Working Directory
Pop-Location