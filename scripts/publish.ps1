param(
    [string]$Output = ".\publish"
)

$ErrorActionPreference = "Stop"

dotnet restore .\HTX586CONTRACT.slnx
dotnet build .\HTX586CONTRACT.slnx -c Release
dotnet publish .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj -c Release -o $Output

Write-Host "Published to $Output"
