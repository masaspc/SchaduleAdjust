#=============================================================================
# ScheduleAdjust - Azure resource provisioning script (PowerShell)
#
# Usage:
#   1. Install Azure CLI
#   2. az login
#   3. Edit variables below
#   4. .\infra\setup-azure.ps1
#=============================================================================

$ErrorActionPreference = "Stop"

# ========================
# Configuration
# ========================
$RESOURCE_GROUP = "rg-schedule-adjust"
$LOCATION = "japaneast"
$SUFFIX = -join ((48..57) + (97..102) | Get-Random -Count 8 | ForEach-Object { [char]$_ })
$APP_NAME = "schedule-adjust-$SUFFIX"
$APP_SERVICE_PLAN = "plan-schedule-adjust"
$APP_SERVICE_SKU = "B1"

$SQL_SERVER_NAME = "sql-schedule-adjust-$SUFFIX"
$SQL_DB_NAME = "ScheduleAdjust"
$SQL_ADMIN_USER = "sqladmin"
$SQL_ADMIN_PASSWORD = ""
$SQL_SKU = "Basic"

$AZURE_AD_TENANT_ID = ""
$AZURE_AD_CLIENT_ID = ""
$AZURE_AD_CLIENT_SECRET = ""

# ========================
# Pre-flight checks
# ========================
Write-Host "=== ScheduleAdjust Azure Setup ===" -ForegroundColor Cyan
Write-Host ""

try {
    az --version | Out-Null
} catch {
    Write-Host "Error: Azure CLI is not installed" -ForegroundColor Red
    exit 1
}

$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Please login to Azure..."
    az login
    $account = az account show | ConvertFrom-Json
}

Write-Host ("Subscription: " + $account.name) -ForegroundColor Green
Write-Host ""

# Auto-generate SQL password if empty
if ([string]::IsNullOrEmpty($SQL_ADMIN_PASSWORD)) {
    $chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
    $random = -join (1..16 | ForEach-Object { $chars[(Get-Random -Maximum $chars.Length)] })
    $SQL_ADMIN_PASSWORD = "P@${random}!"
    Write-Host "SQL admin password auto-generated"
}

# ========================
# 1. Resource Group
# ========================
Write-Host ""
Write-Host "[1/7] Creating resource group..." -ForegroundColor Yellow
az group create --name $RESOURCE_GROUP --location $LOCATION --output none
Write-Host ("  OK " + $RESOURCE_GROUP + " (" + $LOCATION + ")") -ForegroundColor Green

# ========================
# 2. SQL Server
# ========================
Write-Host ""
Write-Host "[2/7] Creating SQL Server..." -ForegroundColor Yellow
az sql server create `
    --name $SQL_SERVER_NAME `
    --resource-group $RESOURCE_GROUP `
    --location $LOCATION `
    --admin-user $SQL_ADMIN_USER `
    --admin-password $SQL_ADMIN_PASSWORD `
    --output none
Write-Host ("  OK " + $SQL_SERVER_NAME) -ForegroundColor Green

# ========================
# 3. SQL Database
# ========================
Write-Host ""
Write-Host "[3/7] Creating SQL Database..." -ForegroundColor Yellow
az sql db create `
    --name $SQL_DB_NAME `
    --resource-group $RESOURCE_GROUP `
    --server $SQL_SERVER_NAME `
    --edition Basic `
    --capacity 5 `
    --max-size 2GB `
    --output none
Write-Host ("  OK " + $SQL_DB_NAME) -ForegroundColor Green

# ========================
# 4. Firewall rules
# ========================
Write-Host ""
Write-Host "[4/7] Configuring firewall..." -ForegroundColor Yellow
az sql server firewall-rule create `
    --name AllowAzureServices `
    --resource-group $RESOURCE_GROUP `
    --server $SQL_SERVER_NAME `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0 `
    --output none

try {
    $LOCAL_IP = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 5)
    az sql server firewall-rule create `
        --name AllowLocalIP `
        --resource-group $RESOURCE_GROUP `
        --server $SQL_SERVER_NAME `
        --start-ip-address $LOCAL_IP `
        --end-ip-address $LOCAL_IP `
        --output none
    Write-Host ("  OK Firewall: Azure services + local IP (" + $LOCAL_IP + ")") -ForegroundColor Green
} catch {
    Write-Host "  OK Firewall: Azure services only" -ForegroundColor Green
}

# ========================
# 5. App Service Plan
# ========================
Write-Host ""
Write-Host "[5/7] Creating App Service Plan..." -ForegroundColor Yellow
az appservice plan create `
    --name $APP_SERVICE_PLAN `
    --resource-group $RESOURCE_GROUP `
    --location $LOCATION `
    --sku $APP_SERVICE_SKU `
    --is-linux `
    --output none
Write-Host ("  OK " + $APP_SERVICE_PLAN + " (" + $APP_SERVICE_SKU + ")") -ForegroundColor Green

# ========================
# 6. App Service
# ========================
Write-Host ""
Write-Host "[6/7] Creating App Service..." -ForegroundColor Yellow
az webapp create `
    --name $APP_NAME `
    --resource-group $RESOURCE_GROUP `
    --plan $APP_SERVICE_PLAN `
    --runtime DOTNETCORE:8.0 `
    --output none
Write-Host ("  OK " + $APP_NAME) -ForegroundColor Green

# ========================
# 7. App settings
# ========================
Write-Host ""
Write-Host "[7/7] Configuring app settings..." -ForegroundColor Yellow

$connStr = "Server=tcp:" + $SQL_SERVER_NAME + ".database.windows.net,1433;Initial Catalog=" + $SQL_DB_NAME + ";Persist Security Info=False;User ID=" + $SQL_ADMIN_USER + ";Password=" + $SQL_ADMIN_PASSWORD + ";MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

$connSetting = "DefaultConnection=" + $connStr
az webapp config connection-string set `
    --name $APP_NAME `
    --resource-group $RESOURCE_GROUP `
    --connection-string-type SQLAzure `
    --settings $connSetting `
    --output none

$baseUrl = "https://" + $APP_NAME + ".azurewebsites.net"
$settings = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "UseStubGraphApi=false",
    ("BaseUrl=" + $baseUrl),
    "AzureAd__Instance=https://login.microsoftonline.com/",
    ("AzureAd__TenantId=" + $AZURE_AD_TENANT_ID),
    ("AzureAd__ClientId=" + $AZURE_AD_CLIENT_ID),
    ("AzureAd__ClientSecret=" + $AZURE_AD_CLIENT_SECRET),
    "AzureAd__CallbackPath=/signin-oidc"
)

az webapp config appsettings set `
    --name $APP_NAME `
    --resource-group $RESOURCE_GROUP `
    --settings @settings `
    --output none

$healthJson = '{"healthCheckPath": "/health"}'
az webapp config set `
    --name $APP_NAME `
    --resource-group $RESOURCE_GROUP `
    --generic-configurations $healthJson `
    --output none

Write-Host "  OK App settings configured" -ForegroundColor Green

# ========================
# Table creation
# ========================
Write-Host ""
$SQL_SCRIPT = Join-Path $PSScriptRoot "..\sql\001_CreateTables.sql"

if ((Test-Path $SQL_SCRIPT) -and (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Host "Creating tables..."
    sqlcmd `
        -S ("tcp:" + $SQL_SERVER_NAME + ".database.windows.net,1433") `
        -d $SQL_DB_NAME `
        -U $SQL_ADMIN_USER `
        -P $SQL_ADMIN_PASSWORD `
        -i $SQL_SCRIPT `
        -C
    Write-Host "  OK Tables created" -ForegroundColor Green
} else {
    Write-Host "Note: Skipped automatic table creation" -ForegroundColor DarkYellow
    if (-not (Test-Path $SQL_SCRIPT)) {
        Write-Host ("  Reason: " + $SQL_SCRIPT + " not found")
    }
    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        Write-Host "  Reason: sqlcmd not installed"
        Write-Host ("  Run manually: sqlcmd -S tcp:" + $SQL_SERVER_NAME + ".database.windows.net,1433 -d " + $SQL_DB_NAME + " -U " + $SQL_ADMIN_USER + " -P '<password>' -i sql\001_CreateTables.sql -C")
    }
}

# ========================
# Done
# ========================
Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  Setup Complete" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host ("App Service URL:    " + $baseUrl)
Write-Host ("Health Check:       " + $baseUrl + "/health")
Write-Host ""
Write-Host ("SQL Server:         " + $SQL_SERVER_NAME + ".database.windows.net")
Write-Host ("SQL Database:       " + $SQL_DB_NAME)
Write-Host ("SQL Admin User:     " + $SQL_ADMIN_USER)
Write-Host ("SQL Admin Password: " + $SQL_ADMIN_PASSWORD) -ForegroundColor Yellow
Write-Host ""
Write-Host ("Resource Group:     " + $RESOURCE_GROUP)
Write-Host ""
Write-Host "----------------------------------------------"
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "----------------------------------------------"
Write-Host ""
Write-Host "1. Register Entra ID app (if not done):"
Write-Host "   - Azure Portal -> Entra ID -> App registrations -> New"
Write-Host ("   - Redirect URI: " + $baseUrl + "/signin-oidc")
Write-Host "   - API permissions: Calendars.ReadWrite, OnlineMeetings.ReadWrite, User.Read.All, Mail.Send"
Write-Host ""
Write-Host "2. Set Entra ID info on App Service:"
Write-Host ("   az webapp config appsettings set --name " + $APP_NAME + " --resource-group " + $RESOURCE_GROUP + " ``")
Write-Host "     --settings AzureAd__TenantId='<tid>' AzureAd__ClientId='<cid>' AzureAd__ClientSecret='<secret>'"
Write-Host ""
Write-Host "3. Set up GitHub Actions CI/CD:"
Write-Host ("   - Update AZURE_WEBAPP_NAME to '" + $APP_NAME + "' in .github/workflows/deploy.yml")
Write-Host "   - Download publish profile from Azure Portal -> App Service -> Deployment"
Write-Host "   - Add to GitHub -> Settings -> Secrets -> AZURE_WEBAPP_PUBLISH_PROFILE"
Write-Host ""
Write-Host "4. Deploy:"
Write-Host "   git push origin main"
Write-Host ""
Write-Host "To delete all resources: .\infra\teardown-azure.ps1" -ForegroundColor DarkYellow
Write-Host ""
