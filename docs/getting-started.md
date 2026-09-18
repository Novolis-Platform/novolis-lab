# Getting started

## Prerequisites

- .NET SDK 10.0.100+ (`global.json`)
- Git 2.40+
- GitHub CLI (`gh`) logged in (`configure-gpr-user-nuget.ps1` uses your current token)
- For graphical samples: a desktop environment (Raylib apps)

## Clone and build

```powershell
git clone https://github.com/Novolis-Platform/novolis-lab.git
cd novolis-lab
..\novolis-governance\scripts\configure-gpr-user-nuget.ps1
dotnet restore
dotnet build --no-restore
dotnet run --project labs/MathGridDemo
```
