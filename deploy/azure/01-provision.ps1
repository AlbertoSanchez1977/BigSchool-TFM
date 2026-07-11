#Requires -Version 7
# Aprovisiona RG + MySQL Flexible + BD (esquema/seed) + App Service Plan + 2 Web Apps.
# Idempotencia: re-ejecutar tras un fallo es seguro (los create fallan "ya existe"; usar 99-teardown para empezar de cero).
$ErrorActionPreference = "Stop"
$root = "C:\SourceCode\BigSchool-TFM\deploy\azure"
$repo = Resolve-Path "$root/../.."

Write-Host "==> Resource Group $AZ_RG"
az group create --name $AZ_RG --location $AZ_LOCATION --output none

Write-Host "==> MySQL Flexible Server (Burstable B1ms)"
az mysql flexible-server create `
  --resource-group $AZ_RG --name $AZ_MYSQL --location $AZ_LOCATION `
  --admin-user $MYSQL_ADMIN_USER --admin-password $MYSQL_ADMIN_PASSWORD `
  --sku-name Standard_B1ms --tier Burstable `
  --version 8.0.21 --storage-size 20 --public-access None --yes --output none

Write-Host "==> Reglas de firewall (Azure + tu IP)"
az mysql flexible-server firewall-rule create --resource-group $AZ_RG --name $AZ_MYSQL `
  --rule-name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 --output none
$myip = (Invoke-RestMethod -Uri "https://api.ipify.org")
az mysql flexible-server firewall-rule create --resource-group $AZ_RG --name $AZ_MYSQL `
  --rule-name AllowMyIP --start-ip-address $myip --end-ip-address $myip --output none
az mysql flexible-server firewall-rule create --resource-group $AZ_RG --name $AZ_MYSQL `
  --rule-name AllowMyIP_Alternative --start-ip-address 163.116.243.139 --end-ip-address 163.116.243.139 --output none

  

Write-Host "==> Base de datos $MYSQL_DB"
az mysql flexible-server db create --resource-group $AZ_RG --server-name $AZ_MYSQL --database-name $MYSQL_DB --output none

$MYSQL_HOST = "$AZ_MYSQL.mysql.database.azure.com"
Write-Host "==> Cargar init.sql + seed.sql en $MYSQL_HOST (cliente mysql dockerizado)"
Get-Content "$repo/infra/docker/mysql/init.sql", "$repo/infra/docker/mysql/seed.sql" -Raw |
  docker run --rm -i mysql:8.0 mysql -h $MYSQL_HOST -u $MYSQL_ADMIN_USER -p"$MYSQL_ADMIN_PASSWORD" --ssl-mode=REQUIRED $MYSQL_DB

Write-Host "==> App Service Plan Linux B1"
az appservice plan create --resource-group $AZ_RG --name $AZ_PLAN --location $AZ_LOCATION --is-linux --sku B1 --output none

Write-Host "==> Web App backend (.NET 8)"
az webapp create --resource-group $AZ_RG --plan $AZ_PLAN --name $AZ_BACKEND --runtime "DOTNETCORE:8.0" --output none
az webapp config appsettings set --resource-group $AZ_RG --name $AZ_BACKEND --output none --settings `
  "ConnectionStrings__DefaultConnection=Server=$MYSQL_HOST;Port=3306;Database=$MYSQL_DB;User=$MYSQL_ADMIN_USER;Password=$MYSQL_ADMIN_PASSWORD;SslMode=Required;" `
  "Jwt__Secret=$JWT_SECRET" "Jwt__Issuer=$JWT_ISSUER" "Jwt__Audience=$JWT_AUDIENCE" "Jwt__ExpirationMinutes=$JWT_EXPIRATION_MINUTES" `
  "ASPNETCORE_ENVIRONMENT=Production"

Write-Host "==> Web App frontend (Node 24)"
az webapp create --resource-group $AZ_RG --plan $AZ_PLAN --name $AZ_FRONTEND --runtime "NODE:24-lts" --output none
az webapp config set --resource-group $AZ_RG --name $AZ_FRONTEND --startup-file "node server.js" --output none
az webapp config appsettings set --resource-group $AZ_RG --name $AZ_FRONTEND --output none --settings "HOSTNAME=0.0.0.0" "SCM_DO_BUILD_DURING_DEPLOYMENT=true" --only-show-errors

Write-Host ""
Write-Host "Provisión completa:"
Write-Host "  Backend : https://$AZ_BACKEND.azurewebsites.net  (/health)"
Write-Host "  Frontend: https://$AZ_FRONTEND.azurewebsites.net"
Write-Host "Siguiente: ./02-deploy.ps1"
