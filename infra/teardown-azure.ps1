#=============================================================================
# ScheduleAdjust - Azure resource cleanup script (PowerShell)
#
# Deletes the entire resource group and all resources within it.
# This operation cannot be undone. Database contents will be lost.
#=============================================================================

$ErrorActionPreference = "Stop"

$RESOURCE_GROUP = "rg-schedule-adjust"

Write-Host "=== ScheduleAdjust Azure Cleanup ===" -ForegroundColor Cyan
Write-Host ""
Write-Host ("Resource group '" + $RESOURCE_GROUP + "' and all resources will be deleted.")
Write-Host ""
Write-Host "This operation cannot be undone!" -ForegroundColor Red
Write-Host "  - App Service"
Write-Host "  - App Service Plan"
Write-Host "  - SQL Database (including data)"
Write-Host "  - SQL Server"
Write-Host ""

$confirm = Read-Host "Are you sure you want to delete? (yes/no)"
if ($confirm -ne "yes") {
    Write-Host "Cancelled."
    exit 0
}

Write-Host ""
Write-Host "Deleting resource group... (this may take a few minutes)"
az group delete --name $RESOURCE_GROUP --yes --no-wait

Write-Host ""
Write-Host "OK Delete request submitted." -ForegroundColor Green
Write-Host "  Deletion is running in the background (may take a few minutes)."
Write-Host ""
Write-Host ("  Check status: az group show --name " + $RESOURCE_GROUP)
Write-Host ""
