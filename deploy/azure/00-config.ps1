#Requires -Version 7
# Configuración compartida por 01-provision.ps1, 02-deploy.ps1 y 99-teardown.ps1.
# NO subir a git: contiene secretos (password MySQL + JWT). Añadir a .gitignore.

# Sufijo de fecha para nombres de recurso (evita colisiones con soft-delete al reprovisionar).
# El RG NO lleva sufijo: se reutiliza / se borra entero en el teardown.
$AZ_DATENOW = (Get-Date).ToString("yyyy-MM-dd")

# ---- Azure: recursos ----
$AZ_RG       = "rg-bstfminvesting-demo"
$AZ_LOCATION = "spaincentral"          # <-- AJUSTA a tu región (northeurope, eastus, ...)
$AZ_PLAN     = "asp-bstfminvesting-demo-$AZ_DATENOW"
$AZ_BACKEND  = "bstfminvesting-demo-$AZ_DATENOW-backend"
$AZ_FRONTEND = "bstfminvesting-demo-$AZ_DATENOW-frontend"

# ---- MySQL Flexible Server ----
$AZ_MYSQL            = "bstfminvesting-demo-$AZ_DATENOW-mysql"
$MYSQL_ADMIN_USER    = "sadmin"
$MYSQL_ADMIN_PASSWORD = "*******************************"
$MYSQL_DB            = "bstfminvesting"

# ---- JWT (deben coincidir con appsettings del backend) ----
$JWT_SECRET            = "*******************************"
$JWT_ISSUER            = "BigSchool"
$JWT_AUDIENCE          = "BigSchoolUsers"
$JWT_EXPIRATION_MINUTES = "60"
