#Requires -Version 7
# Destruye toda la infraestructura borrando el Resource Group completo.
# Nota: el nombre del MySQL queda en soft-delete ~5 dias; por eso 00-config.ps1
#       usa $AZ_DATENOW en los nombres y cada reprovisionamiento genera nombres nuevos.
$ErrorActionPreference = "Stop"
$env:PYTHONWARNINGS = "ignore"
$root_application = "C:\SourceCode\BigSchool-TFM\deploy\azure"
. "$root_application/00-config.ps1"

Write-Host "==> Borrando Resource Group $AZ_RG (y TODOS sus recursos)"
Write-Host "    MySQL : $AZ_MYSQL"
Write-Host "    Backend : $AZ_BACKEND"
Write-Host "    Frontend: $AZ_FRONTEND"
az group delete --name $AZ_RG --yes --no-wait --only-show-errors

Write-Host ""
Write-Host "Borrado iniciado (--no-wait). Azure completa la eliminacion en segundo plano."
Write-Host "El nombre del MySQL queda reservado en soft-delete unos dias; reprovisiona con"
Write-Host "01-provision.ps1 en otro dia (o cambia \$AZ_DATENOW) para un nombre nuevo."
