#!/bin/bash
set -euo pipefail

#=============================================================================
# ScheduleAdjust - Azure リソース削除スクリプト
#
# リソースグループごと全リソースを削除します。
# ⚠ この操作は取り消せません。データベースの内容も失われます。
#=============================================================================

RESOURCE_GROUP="rg-schedule-adjust"

echo "=== ScheduleAdjust Azure環境クリーンアップ ==="
echo ""
echo "リソースグループ '$RESOURCE_GROUP' と配下の全リソースを削除します。"
echo ""
echo "⚠ この操作は取り消せません！"
echo "  - App Service"
echo "  - App Service Plan"
echo "  - SQL Database（データ含む）"
echo "  - SQL Server"
echo ""

read -p "本当に削除しますか？ (yes/no): " CONFIRM
if [ "$CONFIRM" != "yes" ]; then
    echo "キャンセルしました。"
    exit 0
fi

echo ""
echo "リソースグループを削除中... (数分かかります)"
az group delete \
    --name "$RESOURCE_GROUP" \
    --yes \
    --no-wait

echo ""
echo "✓ 削除リクエストを送信しました。"
echo "  バックグラウンドで削除が進行します（完了まで数分かかります）。"
echo ""
echo "  状況確認: az group show --name $RESOURCE_GROUP 2>/dev/null && echo '削除中...' || echo '削除完了'"
echo ""
