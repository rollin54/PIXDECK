# Arma el instalador completo de PixDeck Hub:  dist\PixDeckHub_Setup_<version>.exe
# Uso (PowerShell, en esta carpeta):   .\build_installer.ps1
$ErrorActionPreference = "Stop"
$root  = $PSScriptRoot
$dist  = Join-Path $root "dist"
$app   = Join-Path $dist "app"
$setup = Join-Path $root "PixDeckSetup"

Write-Host "1/4  Compilando PixDeck Hub..." -ForegroundColor Cyan
if (Test-Path $app) { Remove-Item $app -Recurse -Force }
dotnet publish (Join-Path $root "PixDeckHub\PixDeckHub.csproj") -c Release -r win-x64 --self-contained false `
    -p:DebugType=none -o $app --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion de PixDeck Hub" }

Write-Host "2/4  Agregando temas de ejemplo y LEEME..." -ForegroundColor Cyan
Copy-Item (Join-Path $root "temas") (Join-Path $app "temas") -Recurse -Force
Copy-Item (Join-Path $root "LEEME.md") $app -Force

Write-Host "3/4  Comprimiendo..." -ForegroundColor Cyan
$zip = Join-Path $setup "payload.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $app "*") -DestinationPath $zip -CompressionLevel Optimal

Write-Host "4/4  Compilando el instalador..." -ForegroundColor Cyan
$out = Join-Path $dist "setup"
dotnet publish (Join-Path $setup "PixDeckSetup.csproj") -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:DebugType=none -o $out --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion del instalador" }

$version = (Get-Item (Join-Path $app "PixDeckHub.exe")).VersionInfo.ProductVersion -replace '\+.*$', ''
$final = Join-Path $dist "PixDeckHub_Setup_$version.exe"
Copy-Item (Join-Path $out "PixDeckHub_Setup.exe") $final -Force
Write-Host ""
Write-Host "Listo: $final  ($([math]::Round((Get-Item $final).Length / 1MB, 1)) MB)" -ForegroundColor Green
