$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'SNAPPY.CCTV.DiskCalculator\SNAPPY.CCTV.DiskCalculator.csproj'
$out = Join-Path $PSScriptRoot 'publish'

Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue

dotnet restore $project
dotnet publish $project `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  --output $out

Write-Host "Published: $out\SNAPPY-CCTV-Disk-Calculator.exe"
