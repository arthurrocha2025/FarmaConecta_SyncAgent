#!/bin/bash
mkdir -p FarmaConecta_SyncAgent
cd FarmaConecta_SyncAgent

dotnet new sln -n FarmaConecta_SyncAgent

# Core project for shared models and logic
dotnet new classlib -n SyncAgent_Core -f net8.0
dotnet sln add SyncAgent_Core/SyncAgent_Core.csproj

# Setup project (WinForms)
dotnet new winforms -n SyncAgent_Setup -f net8.0
sed -i 's/<TargetFramework>net8.0-windows<\/TargetFramework>/<TargetFramework>net8.0-windows<\/TargetFramework>\n    <EnableWindowsTargeting>true<\/EnableWindowsTargeting>/g' SyncAgent_Setup/SyncAgent_Setup.csproj
dotnet sln add SyncAgent_Setup/SyncAgent_Setup.csproj
dotnet add SyncAgent_Setup/SyncAgent_Setup.csproj reference SyncAgent_Core/SyncAgent_Core.csproj

# Service project (Worker)
dotnet new worker -n SyncAgent_Service -f net8.0
dotnet sln add SyncAgent_Service/SyncAgent_Service.csproj
dotnet add SyncAgent_Service/SyncAgent_Service.csproj reference SyncAgent_Core/SyncAgent_Core.csproj
