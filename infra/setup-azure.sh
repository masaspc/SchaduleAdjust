#!/bin/bash
set -euo pipefail

#=============================================================================
# ScheduleAdjust - Azure リソース作成スクリプト
#
# 使い方:
#   1. Azure CLI をインストール: https://learn.microsoft.com/ja-jp/cli/azure/install-azure-cli
#   2. az login でサインイン
#   3. 下記の変数を環境に合わせて編集
#   4. bash infra/setup-azure.sh
#
# 月額コスト目安:
#   App Service B1:      約 ¥1,900/月
#   SQL Database Basic:  約 ¥750/月
#   合計:                約 ¥2,650/月
#
# ※ 不要になったら infra/teardown-azure.sh で全リソースを削除できます
#=============================================================================

# ========================
# 設定（必要に応じて変更）
# ========================
RESOURCE_GROUP="rg-schedule-adjust"
LOCATION="japaneast"
APP_NAME="schedule-adjust-$(openssl rand -hex 4)"  # 一意な名前を自動生成
APP_SERVICE_PLAN="plan-schedule-adjust"
APP_SERVICE_SKU="B1"                                # B1: 約¥1,900/月, F1: 無料(テスト用)

SQL_SERVER_NAME="sql-schedule-adjust-$(openssl rand -hex 4)"
SQL_DB_NAME="ScheduleAdjust"
SQL_ADMIN_USER="sqladmin"
SQL_ADMIN_PASSWORD=""                               # 空の場合、スクリプトが自動生成
SQL_SKU="Basic"                                     # Basic: 約¥750/月, S0: 約¥2,200/月

# Entra ID（Azure AD）設定 — Azure Portalでアプリ登録後に設定
AZURE_AD_TENANT_ID=""
AZURE_AD_CLIENT_ID=""
AZURE_AD_CLIENT_SECRET=""

# ========================
# 前提条件チェック
# ========================
echo "=== ScheduleAdjust Azure環境セットアップ ==="
echo ""

if ! command -v az &> /dev/null; then
    echo "エラー: Azure CLI がインストールされていません"
    echo "https://learn.microsoft.com/ja-jp/cli/azure/install-azure-cli"
    exit 1
fi

# ログイン確認
if ! az account show &> /dev/null; then
    echo "Azure にログインしてください..."
    az login
fi

SUBSCRIPTION=$(az account show --query name -o tsv)
echo "サブスクリプション: $SUBSCRIPTION"
echo ""

# SQL管理者パスワードを自動生成（未設定の場合）
if [ -z "$SQL_ADMIN_PASSWORD" ]; then
    SQL_ADMIN_PASSWORD="P@$(openssl rand -base64 16 | tr -dc 'a-zA-Z0-9' | head -c 16)!"
    echo "SQL管理者パスワードを自動生成しました（後で表示します）"
fi

# ========================
# 1. リソースグループ作成
# ========================
echo ""
echo "[1/7] リソースグループを作成中..."
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --output none
echo "  ✓ $RESOURCE_GROUP ($LOCATION)"

# ========================
# 2. Azure SQL Server + Database 作成
# ========================
echo ""
echo "[2/7] SQL Serverを作成中..."
az sql server create \
    --name "$SQL_SERVER_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --admin-user "$SQL_ADMIN_USER" \
    --admin-password "$SQL_ADMIN_PASSWORD" \
    --output none
echo "  ✓ $SQL_SERVER_NAME"

echo ""
echo "[3/7] SQL Databaseを作成中..."
az sql db create \
    --name "$SQL_DB_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --edition "Basic" \
    --capacity 5 \
    --max-size "2GB" \
    --output none
echo "  ✓ $SQL_DB_NAME ($SQL_SKU)"

# Azureサービスからのアクセスを許可
echo ""
echo "[4/7] SQLファイアウォールルールを設定中..."
az sql server firewall-rule create \
    --name "AllowAzureServices" \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0 \
    --output none

# ローカルIPからのアクセスも許可（テーブル作成用）
LOCAL_IP=$(curl -s https://api.ipify.org 2>/dev/null || echo "")
if [ -n "$LOCAL_IP" ]; then
    az sql server firewall-rule create \
        --name "AllowLocalIP" \
        --resource-group "$RESOURCE_GROUP" \
        --server "$SQL_SERVER_NAME" \
        --start-ip-address "$LOCAL_IP" \
        --end-ip-address "$LOCAL_IP" \
        --output none
    echo "  ✓ ファイアウォール: Azureサービス + ローカルIP ($LOCAL_IP)"
else
    echo "  ✓ ファイアウォール: Azureサービスのみ"
    echo "  ⚠ ローカルIPの取得に失敗しました。テーブル作成時はAzure Portalからファイアウォールルールを追加してください"
fi

# ========================
# 3. App Service Plan + App Service 作成
# ========================
echo ""
echo "[5/7] App Service Planを作成中..."
az appservice plan create \
    --name "$APP_SERVICE_PLAN" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --sku "$APP_SERVICE_SKU" \
    --is-linux \
    --output none
echo "  ✓ $APP_SERVICE_PLAN ($APP_SERVICE_SKU)"

echo ""
echo "[6/7] App Serviceを作成中..."
az webapp create \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --plan "$APP_SERVICE_PLAN" \
    --runtime "DOTNETCORE:8.0" \
    --output none
echo "  ✓ $APP_NAME"

# ========================
# 4. アプリケーション設定
# ========================
echo ""
echo "[7/7] アプリケーション設定を登録中..."

SQL_CONNECTION_STRING="Server=tcp:${SQL_SERVER_NAME}.database.windows.net,1433;Initial Catalog=${SQL_DB_NAME};Persist Security Info=False;User ID=${SQL_ADMIN_USER};Password=${SQL_ADMIN_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

az webapp config connection-string set \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --connection-string-type SQLAzure \
    --settings DefaultConnection="$SQL_CONNECTION_STRING" \
    --output none

az webapp config appsettings set \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --settings \
        ASPNETCORE_ENVIRONMENT="Production" \
        UseStubGraphApi="false" \
        BaseUrl="https://${APP_NAME}.azurewebsites.net" \
        AzureAd__Instance="https://login.microsoftonline.com/" \
        AzureAd__TenantId="${AZURE_AD_TENANT_ID}" \
        AzureAd__ClientId="${AZURE_AD_CLIENT_ID}" \
        AzureAd__ClientSecret="${AZURE_AD_CLIENT_SECRET}" \
        AzureAd__CallbackPath="/signin-oidc" \
    --output none

# ヘルスチェック設定
az webapp config set \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --generic-configurations '{"healthCheckPath": "/health"}' \
    --output none

echo "  ✓ アプリケーション設定完了"

# ========================
# 5. テーブル作成
# ========================
echo ""
SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SQL_SCRIPT="$SCRIPT_DIR/sql/001_CreateTables.sql"

if [ -f "$SQL_SCRIPT" ] && command -v sqlcmd &> /dev/null; then
    echo "テーブルを作成中..."
    sqlcmd \
        -S "tcp:${SQL_SERVER_NAME}.database.windows.net,1433" \
        -d "$SQL_DB_NAME" \
        -U "$SQL_ADMIN_USER" \
        -P "$SQL_ADMIN_PASSWORD" \
        -i "$SQL_SCRIPT" \
        -C
    echo "  ✓ テーブル作成完了"
else
    echo "⚠ テーブルの自動作成をスキップしました"
    if [ ! -f "$SQL_SCRIPT" ]; then
        echo "  理由: $SQL_SCRIPT が見つかりません"
    fi
    if ! command -v sqlcmd &> /dev/null; then
        echo "  理由: sqlcmd がインストールされていません"
        echo "  手動で実行してください: sqlcmd -S tcp:${SQL_SERVER_NAME}.database.windows.net,1433 -d $SQL_DB_NAME -U $SQL_ADMIN_USER -P '<password>' -i sql/001_CreateTables.sql -C"
    fi
fi

# ========================
# 完了
# ========================
echo ""
echo "=============================================="
echo "  セットアップ完了"
echo "=============================================="
echo ""
echo "App Service URL:   https://${APP_NAME}.azurewebsites.net"
echo "Health Check:      https://${APP_NAME}.azurewebsites.net/health"
echo ""
echo "SQL Server:        ${SQL_SERVER_NAME}.database.windows.net"
echo "SQL Database:      $SQL_DB_NAME"
echo "SQL Admin User:    $SQL_ADMIN_USER"
echo "SQL Admin Password: $SQL_ADMIN_PASSWORD"
echo ""
echo "リソースグループ:   $RESOURCE_GROUP"
echo ""
echo "----------------------------------------------"
echo "次のステップ:"
echo "----------------------------------------------"
echo ""
echo "1. Entra ID アプリ登録（まだの場合）:"
echo "   - Azure Portal > Entra ID > アプリの登録 > 新規登録"
echo "   - リダイレクトURI: https://${APP_NAME}.azurewebsites.net/signin-oidc"
echo "   - API権限: Calendars.ReadWrite, OnlineMeetings.ReadWrite, User.Read.All, Mail.Send"
echo ""
echo "2. Entra ID情報をApp Serviceに設定:"
echo "   az webapp config appsettings set --name $APP_NAME --resource-group $RESOURCE_GROUP \\"
echo "     --settings AzureAd__TenantId='<tenant-id>' AzureAd__ClientId='<client-id>' AzureAd__ClientSecret='<secret>'"
echo ""
echo "3. GitHub ActionsでCI/CDを設定:"
echo "   - .github/workflows/deploy.yml の AZURE_WEBAPP_NAME を '$APP_NAME' に変更"
echo "   - Azure Portal > App Service > デプロイ > 発行プロファイルをダウンロード"
echo "   - GitHub > Settings > Secrets > AZURE_WEBAPP_PUBLISH_PROFILE に登録"
echo ""
echo "4. デプロイ:"
echo "   git push origin main"
echo ""
echo "⚠ 不要になったら: bash infra/teardown-azure.sh"
echo ""
