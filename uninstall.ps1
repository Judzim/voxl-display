# Removes everything this project put on the machine.
#
#   .\uninstall.ps1              app, shortcuts, firewall rules, scheduled task, driver
#   .\uninstall.ps1 -KeepDriver  leave the virtual display installed
#
# Run elevated. Nothing here needs a reboot.
[CmdletBinding()]
param(
    [switch]$KeepDriver,
    [string]$Target = "$env:LOCALAPPDATA\VoxlDisplay"
)

$ErrorActionPreference = "Continue"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this elevated."
}

function Step($text) { Write-Host "  $text" -ForegroundColor Cyan }

Write-Host "Removing Voxl Display" -ForegroundColor White

# 1. Processes.
Get-Process VoxlDisplay, voxl -ErrorAction SilentlyContinue | Stop-Process -Force
Step "stopped the app and voxl"

# 2. Scheduled task, if one was ever created.
$task = Get-ScheduledTask -TaskName "voxl second display" -ErrorAction SilentlyContinue
if ($task) {
    Unregister-ScheduledTask -TaskName "voxl second display" -Confirm:$false
    Step "removed the scheduled task"
}

# 3. Firewall rules: ours by name, plus any auto-created rule for a voxl.exe.
$removed = 0
foreach ($rule in Get-NetFirewallRule -ErrorAction SilentlyContinue) {
    $app = $rule | Get-NetFirewallApplicationFilter -ErrorAction SilentlyContinue
    if ($rule.DisplayName -eq "voxl 6901 (LAN)" -or ($app.Program -and $app.Program -like "*voxl.exe")) {
        $rule | Remove-NetFirewallRule -ErrorAction SilentlyContinue
        $removed++
    }
}
Step "removed $removed firewall rule(s)"

# 4. Shortcuts.
foreach ($dir in @([Environment]::GetFolderPath("Desktop"),
                   (Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs"))) {
    foreach ($name in @("Voxl Display.lnk", "Druhy displej.lnk")) {
        $path = Join-Path $dir $name
        if (Test-Path $path) { Remove-Item $path -Force; Step "removed $path" }
    }
}

# 5. The app itself.
if (Test-Path $Target) { Remove-Item $Target -Recurse -Force; Step "removed $Target" }

# 6. The TLS certificate voxl keeps for the self-signed HTTPS.
$pem = Join-Path $env:LOCALAPPDATA "voxl\voxl-tls.pem"
if (Test-Path $pem) { Remove-Item $pem -Force; Step "removed the TLS certificate" }

# 7. The virtual display driver.
if ($KeepDriver) {
    Write-Host "  driver kept (-KeepDriver)" -ForegroundColor Yellow
} else {
    $driverScript = Join-Path $PSScriptRoot "uninstall-vdd.ps1"
    if (Test-Path $driverScript) {
        & $driverScript 2>&1 | ForEach-Object { "  $_" }
    } else {
        Write-Host "  uninstall-vdd.ps1 not found, the virtual display is still installed" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Done. The self-signed code signing certificate stays in your user store;" -ForegroundColor Green
Write-Host "remove it with: Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -like '*Voxl Display*' | Remove-Item" -ForegroundColor Gray
