#=============================================================================
# ScheduleAdjust - Azure リソース削除スクリプト (PowerShell版)
#
# リソースグループごと全リソースを削除します。
# この操作は取り消せません。データベースの内容も失われます。
#=============================================================================

$ErrorActionPreference = "Stop"

$RESOURCE_GROUP = "rg-schedule-adjust"

Write-Host "=== ScheduleAdjust Azure環境クリーンアップ ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "リソースグループ '$RESOURCE_GROUP' と配下の全リソースを削除します。"
Write-Host ""
Write-Host "この操作は取り消せません！" -ForegroundColor Red
Write-Host "  - App Service"
Write-Host "  - App Service Plan"
Write-Host "  - SQL Database（データ含む）"
Write-Host "  - SQL Server"
Write-Host ""

$confirm = Read-Host "本当に削除しますか？ (yes/no)"
if ($confirm -ne "yes") {
    Write-Host "キャンセルしました。"
    exit 0
}

Write-Host ""
Write-Host "リソースグループを削除中... (数分かかります)"
az group delete --name $RESOURCE_GROUP --yes --no-wait

Write-Host ""
Write-Host "OK 削除リクエストを送信しました。" -ForegroundColor Green
Write-Host "  バックグラウンドで削除が進行します（完了まで数分かかります）。"
Write-Host ""
Write-Host "  状況確認: az group show --name $RESOURCE_GROUP 2>`$null; if (`$?) { '削除中...' } else { '削除完了' }"
Write-Host ""
