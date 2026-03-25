#=============================================================================
# ScheduleAdjust - Azure リソース作成スクリプト (PowerShell版)
#
# 使い方:
#   1. Azure CLI をインストール: https://learn.microsoft.com/ja-jp/cli/azure/install-azure-cli
#   2. az login でサインイン
#   3. 下記の変数を環境に合わせて編集
#   4. .\infra\setup-azure.ps1
#
# 月額コスト目安:
#   App Service B1:      約 ¥1,900/月
#   SQL Database Basic:  約 ¥750/月
#   合計:                約 ¥2,650/月
#
# 不要になったら .\infra\teardown-azure.ps1 で全リソースを削除できます
#=============================================================================

$ErrorActionPreference = "Stop"

# ========================
# 設定（必要に応じて変更）
# ========================
$RESOURCE_GROUP = "rg-schedule-adjust"
$LOCATION = "japaneast"
$SUFFIX = -join ((48..57) + (97..102) | Get-Random -Count 8 | ForEach-Object { [char]$_ })
$APP_NAME = "schedule-adjust-$SUFFIX"
$APP_SERVICE_PLAN = "plan-schedule-adjust"
$APP_SERVICE_SKU = "B1"                # B1: 約¥1,900/月, F1: 無料(テスト用)

$SQL_SERVER_NAME = "sql-schedule-adjust-$SUFFIX"
$SQL_DB_NAME = "ScheduleAdjust"
$SQL_ADMIN_USER = "sqladmin"
$SQL_ADMIN_PASSWORD = ""               # 空の場合、スクリプトが自動生成
$SQL_SKU = "Basic"                     # Basic: 約¥750/月, S0: 約¥2,200/月

# Entra ID（Azure AD）設定 — Azure Portalでアプリ登録後に設定
$AZURE_AD_TENANT_ID = ""
$AZURE_AD_CLIENT_ID = ""
$AZURE_AD_CLIENT_SECRET = ""

# ========================
# 前提条件チェック
# ========================
Write-Host "=== ScheduleAdjust Azure環境セットアップ ===" -ForegroundColor Cyan
Write-Host ""

try {
    az --version | Out-Null
} catch {
    Write-Host "エラー: Azure CLI がインストールされていません" -ForegroundColor Red
    Write-Host "https://learn.microsoft.com/ja-jp/cli/azure/install-azure-cli"
    exit 1
}

# ログイン確認
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Azure にログインしてください..."
    az login
    $account = az account show | ConvertFrom-Json
}

Write-Host "サブスクリプション: $($account.name)" -ForegroundColor Green
Write-Host ""

# SQL管理者パスワードを自動生成（未設定の場合）
if ([string]::IsNullOrEmpty($SQL_ADMIN_PASSWORD)) {
    $chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
    $random = -join (1..16 | ForEach-Object { $chars[(Get-Random -Maximum $chars.Length)] })
    $SQL_ADMIN_PASSWORD = "P@${random}!"
    Write-Host "SQL管理者パスワードを自動生成しました（後で表示します）"
}

# ========================
# 1. リソースグループ作成
# ========================
Write-Host ""
Write-Host "[1/7] リソースグループを作成中..." -ForegroundColor Yellow
az group create --name $RESOURCE_GROUP --location $LOCATION --output none
Write-Host "  OK $RESOURCE_GROUP ($LOCATION)" -ForegroundColor Green

# ========================
# 2. Azure SQL Server + Database 作成
# ========================
Write-Host ""
Write-Host "[2/7] SQL Serverを作成中..." -ForegroundColor Yellow
az sql server create `
    --name $SQL_SERVER_NAME `
    --resource-group $RESOURCE_GROUP `
    --location $LOCATION `
    --admin-user $SQL_ADMIN_USER `
    --admin-password $SQL_ADMIN_PASSWORD `
    --output none
Write-Host "  OK $SQL_SERVER_NAME" -ForegroundColor Green

Write-Host ""
Write-Host "[3/7] SQL Databaseを作成中..." -ForegroundColor Yellow
az sql db create `
    --name $SQL_DB_NAME `
    --resource-group $RESOURCE_GROUP `
    --server $SQL_SERVER_NAME `
    --edition "Basic" `
    --capacity 5 `
    --max-size "2GB" `
    --output none
Write-Host "  OK $SQL_DB_NAME ($SQL_SKU)" -ForegroundColor Green

# Azureサービスからのアクセスを許可
Write-Host ""
Write-Host "[4/7] SQLファイアウォールルールを設定中..." -ForegroundColor Yellow
az sql server firewall-rule create `
    --name "AllowAzureServices" `
    --resource-group $RESOURCE_GROUP `
    --server $SQL_SERVER_NAME `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0 `
    --output none

# ローカルIPからのアクセスも許可
try {
    $LOCAL_IP = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 5)
    az sql server firewall-rule create `
        --name "AllowLocalIP" `
        --resource-group $RESOURCE_GROUP `
        --server $SQL_SERVER_NAME `
        --start-ip-address $LOCAL_IP `
        --end-ip-address $LOCAL_IP `
        --output none
    Write-Host "  OK ファイアウォール: Azureサービス + ローカルIP ($LOCAL_IP)" -ForegroundColor Green
} catch {
    Write-Host "  OK ファイアウォール: Azureサービスのみ" -ForegroundColor Green
    Write-Host "  注意: ローカルIPの取得に失敗。テーブル作成時はAzure Portalからファイアウォールルールを追加してください" -ForegroundColor DarkYellow
}

# ========================
# 3. App Service Plan + App Service 作成
# ========================
Write-Host ""
Write-Host "[5/7] App Service Planを作成中..." -ForegroundColor Yellow
az appservice plan create `
    --name $APP_SERVICE_PLAN `
    --resource-group $RESOURCE_GROUP `
    --location $LOCATION `
    --sku $APP_SERVICE_SKU `
    --is-linux `
    --output none
Write-Host "  OK $APP_SERVICE_PLAN ($APP_SERVICE_SKU)" -ForegroundColor Green

Write-Host ""
Write-Host "[6/7] App Serviceを作成中..." -ForegroundColor Yellow
az webapp create `
    --name $APP_NAME `
    --resource-group $RESOURCE_GROUP `
    --plan $APP_SERVICE_PLAN `
    --runtime "DOTNETCORE:8.0" `
    --output none
Write-Host "  OK $APP_NAME" -ForegroundColor Green

# ========================
# 4. アプリケーション設定
# ========================
Write-Host ""
Write-Host "[7/7] アプリケーション設定を登録中..." -ForegroundColor Yellow

$SQL_CONNECTION_STRING = "Server=tcp:${SQL_SERVER_NAME}.database.windows.net,1433;Initial Catalog=${SQL_DB_NAME};Persist Security Info=False;User ID=${SQL_ADMIN_USER};Password=${SQL_ADMIN_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

az webapp config connection-string set `
    --name $APP_NAME `
    --resource-group $RESOURCE_GROUP `
    --connection-string-type SQLAzure `
    --settings DefaultConnection="$SQL_CONNECTION_STRING" `
    --output none

az webapp config appsettings set `
    --name $APP_NAME `
    --resource-group $RESOURCE_GROUP `
    --settings `
        ASPNETCORE_ENVIRONMENT="Production" `
        UseStubGraphApi="false" `
        BaseUrl="https://${APP_NAME}.azurewebsites.net" `
        AzureAd__Instance="https://login.microsoftonline.com/" `
        AzureAd__TenantId="$AZURE_AD_TENANT_ID" `
        AzureAd__ClientId="$AZURE_AD_CLIENT_ID" `
        AzureAd__ClientSecret="$AZURE_AD_CLIENT_SECRET" `
        AzureAd__CallbackPath="/signin-oidc" `
    --output none

# ヘルスチェック設定
az webapp config set `
    --name $APP_NAME `
    --resource-group $RESOURCE_GROUP `
    --generic-configurations '{\"healthCheckPath\": \"/health\"}' `
    --output none

Write-Host "  OK アプリケーション設定完了" -ForegroundColor Green

# ========================
# 5. テーブル作成
# ========================
Write-Host ""
$SQL_SCRIPT = Join-Path $PSScriptRoot "..\sql\001_CreateTables.sql"

if ((Test-Path $SQL_SCRIPT) -and (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Host "テーブルを作成中..."
    sqlcmd `
        -S "tcp:${SQL_SERVER_NAME}.database.windows.net,1433" `
        -d $SQL_DB_NAME `
        -U $SQL_ADMIN_USER `
        -P $SQL_ADMIN_PASSWORD `
        -i $SQL_SCRIPT `
        -C
    Write-Host "  OK テーブル作成完了" -ForegroundColor Green
} else {
    Write-Host "注意: テーブルの自動作成をスキップしました" -ForegroundColor DarkYellow
    if (-not (Test-Path $SQL_SCRIPT)) {
        Write-Host "  理由: $SQL_SCRIPT が見つかりません"
    }
    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        Write-Host "  理由: sqlcmd がインストールされていません"
        Write-Host "  手動で実行: sqlcmd -S tcp:${SQL_SERVER_NAME}.database.windows.net,1433 -d $SQL_DB_NAME -U $SQL_ADMIN_USER -P '<password>' -i sql\001_CreateTables.sql -C"
    }
}

# ========================
# 完了
# ========================
Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  セットアップ完了" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "App Service URL:    https://${APP_NAME}.azurewebsites.net"
Write-Host "Health Check:       https://${APP_NAME}.azurewebsites.net/health"
Write-Host ""
Write-Host "SQL Server:         ${SQL_SERVER_NAME}.database.windows.net"
Write-Host "SQL Database:       $SQL_DB_NAME"
Write-Host "SQL Admin User:     $SQL_ADMIN_USER"
Write-Host "SQL Admin Password: $SQL_ADMIN_PASSWORD" -ForegroundColor Yellow
Write-Host ""
Write-Host "リソースグループ:    $RESOURCE_GROUP"
Write-Host ""
Write-Host "----------------------------------------------"
Write-Host "次のステップ:" -ForegroundColor Cyan
Write-Host "----------------------------------------------"
Write-Host ""
Write-Host "1. Entra ID アプリ登録（まだの場合）:"
Write-Host "   - Azure Portal > Entra ID > アプリの登録 > 新規登録"
Write-Host "   - リダイレクトURI: https://${APP_NAME}.azurewebsites.net/signin-oidc"
Write-Host "   - API権限: Calendars.ReadWrite, OnlineMeetings.ReadWrite, User.Read.All, Mail.Send"
Write-Host ""
Write-Host "2. Entra ID情報をApp Serviceに設定:"
Write-Host "   az webapp config appsettings set --name $APP_NAME --resource-group $RESOURCE_GROUP ``"
Write-Host "     --settings AzureAd__TenantId='<tenant-id>' AzureAd__ClientId='<client-id>' AzureAd__ClientSecret='<secret>'"
Write-Host ""
Write-Host "3. GitHub ActionsでCI/CDを設定:"
Write-Host "   - .github/workflows/deploy.yml の AZURE_WEBAPP_NAME を '$APP_NAME' に変更"
Write-Host "   - Azure Portal > App Service > デプロイ > 発行プロファイルをダウンロード"
Write-Host "   - GitHub > Settings > Secrets > AZURE_WEBAPP_PUBLISH_PROFILE に登録"
Write-Host ""
Write-Host "4. デプロイ:"
Write-Host "   git push origin main"
Write-Host ""
Write-Host "不要になったら: .\infra\teardown-azure.ps1" -ForegroundColor DarkYellow
Write-Host ""
