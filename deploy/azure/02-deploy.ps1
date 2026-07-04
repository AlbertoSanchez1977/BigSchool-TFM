#Requires -Version 7
# Compila y despliega backend (.NET 8) y frontend (Next.js standalone) a las Web Apps
# creadas por 01-provision.ps1.
#
# Backend : dotnet publish para linux-x64 (compilamos en Windows, destino Linux) -> zip -> deploy.
# Frontend: next build (standalone) -> zip SIN node_modules -> Azure ejecuta `npm install`
#           en el servidor (SCM_DO_BUILD_DURING_DEPLOYMENT=true). Evita:
#             - symlinks de pnpm en .next/standalone/node_modules (no copiables en Windows)
#             - rutas > 260 chars (por eso ensamblamos en C:\bsd, no en %TEMP%)
#             - zip gigante que daba timeout 504 al subir al App Service
$ErrorActionPreference = "Stop"
$env:PYTHONWARNINGS = "ignore"     # silencia el warning de cryptography 32-bit de la Azure CLI
$root_application = "C:\SourceCode\BigSchool-TFM\deploy\azure"
. "$root_application/00-config.ps1"
$repo = Resolve-Path "$root_application/../.."
$tmp = "C:\bsd"                    # ruta corta: evita el limite de 260 chars de Windows
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

# Zip con separador "/" (estandar ZIP). En Windows PowerShell 5.1 (el que usa el ISE) tanto
# Compress-Archive como ZipFile.CreateFromDirectory escriben rutas con "\", que en Linux se
# interpretan como parte del nombre y rompen el rsync de Oryx (error "failed to stat .next\BUILD_ID").
# Aqui creamos cada entrada a mano forzando "/", asi funciona en PS 5.1 y en PS 7.
Add-Type -AssemblyName System.IO.Compression.FileSystem
function New-DeployZip([string]$sourceDir, [string]$zipPath) {
    if (Test-Path $zipPath) { [System.IO.File]::Delete($zipPath) }
    $root = (Resolve-Path $sourceDir).Path.TrimEnd('\', '/')
    $zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object {
            $rel = $_.FullName.Substring($root.Length + 1) -replace '\\', '/'
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $_.FullName, $rel,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $zip.Dispose() }
}

# ============================================================
# Backend: dotnet publish (linux-x64) -> zip -> deploy
# ============================================================
Write-Host "==> Build backend (dotnet publish linux-x64)"
$beOut = Join-Path $tmp "backend"
if (Test-Path $beOut) { [System.IO.Directory]::Delete($beOut, $true) }
dotnet publish "$repo/src/backend/src/BigSchool.WebApi/BigSchool.WebApi.csproj" `
  -c Release -r linux-x64 --no-self-contained -o $beOut
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo con codigo $LASTEXITCODE" }

$beZip = Join-Path $tmp "backend.zip"
New-DeployZip $beOut $beZip

Write-Host "==> Deploy backend (zip)"
az webapp deploy --resource-group $AZ_RG --name $AZ_BACKEND --type zip --src-path $beZip `
  --output none --only-show-errors

# ============================================================
# Frontend: next build (standalone) -> zip sin node_modules -> deploy
# ============================================================
Write-Host "==> Build frontend (next build standalone)"
$apiUrl = "https://$AZ_BACKEND.azurewebsites.net/api/v1"
Push-Location "$repo/src/frontend-web"
$env:NEXT_PUBLIC_API_URL = $apiUrl
pnpm install --frozen-lockfile
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "pnpm install fallo con codigo $LASTEXITCODE" }
pnpm build
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "pnpm build fallo con codigo $LASTEXITCODE" }
Pop-Location

$standalone = "$repo\src\frontend-web\.next\standalone"
if (-not (Test-Path $standalone)) {
    throw "No existe $standalone. Revisa que next.config.mjs tenga output: 'standalone'."
}

Write-Host "==> Ensamblar salida standalone (sin node_modules)"
$feOut = Join-Path $tmp "frontend"
if (Test-Path $feOut) { [System.IO.Directory]::Delete($feOut, $true) }
New-Item -ItemType Directory -Force -Path $feOut | Out-Null

# Codigo standalone (server.js + .next server) EXCLUYENDO node_modules y junctions de pnpm.
# /XD node_modules -> no copia esa carpeta ; /XJ -> ignora junctions (symlinks de pnpm)
robocopy $standalone $feOut /E /XJ /XD node_modules /NFL /NDL /NJH /NJS /nc /ns /np
if ($LASTEXITCODE -gt 7) { throw "robocopy (standalone) fallo con codigo $LASTEXITCODE" }

# Assets estaticos: van a .next/static (standalone no los incluye)
robocopy "$repo\src\frontend-web\.next\static" "$feOut\.next\static" /E /NFL /NDL /NJH /NJS /nc /ns /np
if ($LASTEXITCODE -gt 7) { throw "robocopy (static) fallo con codigo $LASTEXITCODE" }

# Carpeta public (si existe)
if (Test-Path "$repo\src\frontend-web\public") {
    robocopy "$repo\src\frontend-web\public" "$feOut\public" /E /NFL /NDL /NJH /NJS /nc /ns /np
    if ($LASTEXITCODE -gt 7) { throw "robocopy (public) fallo con codigo $LASTEXITCODE" }
}

# package.json recortado: solo dependencies + script start (SIN build/devDependencies).
# Azure (Oryx) hara `npm install` de estas deps en el servidor; al no haber script "build"
# NO intentara recompilar Next (que fallaria: no subimos el codigo fuente).
Write-Host "==> Generar package.json de produccion para npm install en Azure"
$pkg = Get-Content "$repo\src\frontend-web\package.json" -Raw | ConvertFrom-Json
$deployPkg = [ordered]@{
    name         = $pkg.name
    version      = $pkg.version
    private      = $true
    scripts      = [ordered]@{ start = "node server.js" }
    dependencies = $pkg.dependencies
}
$deployPkg | ConvertTo-Json -Depth 20 | Set-Content "$feOut\package.json" -Encoding utf8

$feZip = Join-Path $tmp "frontend.zip"
New-DeployZip $feOut $feZip

Write-Host "==> Deploy frontend (zip; Azure hara npm install en el servidor)"
az webapp deploy --resource-group $AZ_RG --name $AZ_FRONTEND --type zip --src-path $feZip `
  --output none --only-show-errors

Write-Host ""
Write-Host "Despliegue completo:"
Write-Host "  Backend : https://$AZ_BACKEND.azurewebsites.net/health"
Write-Host "  Frontend: https://$AZ_FRONTEND.azurewebsites.net"
Write-Host ""
Write-Host "Nota: el primer arranque del frontend tarda (Azure instala node_modules)."
Write-Host "      Logs de build: https://$AZ_FRONTEND.scm.azurewebsites.net/api/deployments/latest/log"
