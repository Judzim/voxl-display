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

# SIG # Begin signature block
# MIIHYwYJKoZIhvcNAQcCoIIHVDCCB1ACAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCChfpfLWbSRwOZQ
# VlJsjy2nxsYQjkWP+zr9yXDjYAJ3x6CCBD4wggQ6MIICoqADAgECAhAvDKecr0EG
# o08oGq2g8HmKMA0GCSqGSIb3DQEBCwUAMDUxCzAJBgNVBAYTAlNLMRUwEwYDVQQK
# DAxWb3hsIERpc3BsYXkxDzANBgNVBAMMBkp1ZHppbTAeFw0yNjExMTExNzIwMzRa
# Fw0zNjExMTExNzMwMzRaMDUxCzAJBgNVBAYTAlNLMRUwEwYDVQQKDAxWb3hsIERp
# c3BsYXkxDzANBgNVBAMMBkp1ZHppbTCCAaIwDQYJKoZIhvcNAQEBBQADggGPADCC
# AYoCggGBAJxk5T3h/ea+5VCfAb+gT2e4ogLYDZWNkpq1AnvpyETS1a1sQJOwRDmE
# sj6BoZ9Tt2VEIV9DT28Oeey5XqGRk29M93BHNrrBqRicEW3O2JLxlu6IDM8Iq7MX
# 1XRHy7Mqjbz3UsGA1g7KCjn+QvFTEenm4D/0QFQxZnjIrm0ioWbibDA0uY47kj41
# aJksD9MmheLSnObtkiFMyuypriSZpcifwZpcYbUsq4KobhzG+ioQh/bBOidRdPf8
# +aRgAmEZPPSCMCubSv9Ly6wifr4wWy0qS+AsRwAiYqyZaeEUldYRtEUfihniTQ6n
# R9BXln1NPYi2OFfhiQ+YbhptTc7hJ7Jj25vVH+nwAZ8wQNryjnnz13jdrGumBWG2
# PSDdQ+3x30KXsq5kFHe8CDZ0FAel8X8KBv7JP5j4E28PNIJGyjdroSoPmOgIWh5p
# /BpvoeQXqSSwcwJJDAxlr0zTRiatJOVEd1ttlSFEzfOLEFhFcoLGTr5pM/UNStnW
# NjeqNYoNIQIDAQABo0YwRDAOBgNVHQ8BAf8EBAMCB4AwEwYDVR0lBAwwCgYIKwYB
# BQUHAwMwHQYDVR0OBBYEFMg3v6dBWMQZGJuKpQTyHPviigupMA0GCSqGSIb3DQEB
# CwUAA4IBgQBzLs5G9LFYZOy0eTZcYATwBsYFJsEh2xi22Lb4pRACAYLrajQofAKK
# Z5n9JRhPTm3Kq2mTx/yOXQys/JBiiRmZz3ARMnUX08x/n7JuGfPDGi9U0MjVvQsm
# LSswkhm+h7gbiqeC2rrNDrAeJb5riU2lJH/qmwn+E21t7dn6bDhuXaSv6ZlaZdB9
# BrudXo8gVL+GA+If+8jM+PtAUGpqCnZ5fhEGERUwLNUbuD3FTbLmOiJqgC0+eqz+
# nun21wqATSoj/uUA/07Y5Gmv9tyDl78fL+cJApLU6fjHhZVlgMCKQWGOCXdh/50O
# QQE8b8oLAttPwcMRsYj9Ty4X2kA992obh+aQDsx+nJ5G53kZUssBdfEX25/ZwZTd
# 7qRWnPRYXSXIn77HaD1nfZrONzRYq4wpwI0iOJ4CzWAXhYlVDosKgtG6u0GF/4vO
# hRDvNMmunxocAzNGJnyKFedGtjQECKQCXdIZ1/cL/cACfyK3FRAFtyn5v4z4t1k3
# pEkE4umVbqYxggJ7MIICdwIBATBJMDUxCzAJBgNVBAYTAlNLMRUwEwYDVQQKDAxW
# b3hsIERpc3BsYXkxDzANBgNVBAMMBkp1ZHppbQIQLwynnK9BBqNPKBqtoPB5ijAN
# BglghkgBZQMEAgEFAKCBhDAYBgorBgEEAYI3AgEMMQowCKACgAChAoAAMBkGCSqG
# SIb3DQEJAzEMBgorBgEEAYI3AgEEMBwGCisGAQQBgjcCAQsxDjAMBgorBgEEAYI3
# AgEVMC8GCSqGSIb3DQEJBDEiBCAKuae8adW5vvYJVBmM2ja5v/HoWjsRxA3WWzI3
# eimnVzANBgkqhkiG9w0BAQEFAASCAYBqnmaHAsjjY/HUIO8e4/G91FPboNpF3IVm
# I86+7tZYRp0X5z8mYlMsPxGoACsU/ASTAmLUFsGUrKwjpXemYBWRsuJsHrnM6uXY
# CE07NSn/1wN7SgofTgTdwST7M5wK3xqX0BKDo658VYB/CPA3iBCmfyc+oPgOA3M8
# wgBemrmI7Y2JweZXsR3LmtjRkR6V45lFwtrXto+KGzrPxSuS3iayfOeKqJg+BpTa
# Wg7PUHvhn7HRvjH56n7R9Zg6m2eceFtR2rX64506aA1iC/uoUoL7opg4PtgSmHyg
# dDM+qgcE1GJu8rBf1LU4u9LvNkQbrDq8YujNEpkZW64e/OYXJq5I9qehrh0rGqW5
# A0y00Af+5f+4sZpxQdDDCl8tMjrrk3H5pjVQEfn9zRXmRu/CuF+81bzMnYNGJBwb
# P7XZehPrpwvf3Q6aAIKpcidg91ZXwjLvSbDiHcCR13gchgb13nmQ+CtvGBl9YOHN
# vAnRMIlTe9+MH6QLnLnoDSWt3kdRXjE=
# SIG # End signature block
