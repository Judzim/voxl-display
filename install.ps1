# Installs Voxl Display: the app, the virtual display driver (if missing), the firewall
# rule and the shortcuts. Run elevated (install.cmd does that for you).
#
#   .\install.ps1                 install or repair
#   .\install.ps1 -Uninstall      remove the app, shortcuts and firewall rule
#
# The driver is only installed when the MTT1337 monitor is missing, so running this
# twice never adds a second virtual monitor.
[CmdletBinding()]
param(
    [switch]$Uninstall,
    [string]$Source = $PSScriptRoot,
    [string]$Target = "$env:LOCALAPPDATA\VoxlDisplay"
)

$ErrorActionPreference = "Stop"

function Test-Admin {
    ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Write-Step($text) { Write-Host "  $text" -ForegroundColor Cyan }

$shortcutName = "Voxl Display.lnk"
$legacyShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "Druhy displej.lnk"
$desktop = [Environment]::GetFolderPath("Desktop")
$startMenu = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs"

if ($Uninstall) {
    if (-not (Test-Admin)) { throw "Run this elevated." }
    Write-Host "Removing Voxl Display" -ForegroundColor White
    Get-Process VoxlDisplay, voxl -ErrorAction SilentlyContinue | Stop-Process -Force
    foreach ($p in @((Join-Path $desktop $shortcutName), (Join-Path $startMenu $shortcutName), $legacyShortcut)) {
        if (Test-Path $p) { Remove-Item $p -Force; Write-Step "removed $p" }
    }
    $rule = Get-NetFirewallRule -DisplayName "voxl 6901 (LAN)" -ErrorAction SilentlyContinue
    if ($rule) { $rule | Remove-NetFirewallRule; Write-Step "removed the firewall rule" }
    if (Test-Path $Target) { Remove-Item $Target -Recurse -Force; Write-Step "removed $Target" }
    Write-Host ""
    Write-Host "The virtual display driver was left in place." -ForegroundColor Yellow
    Write-Host "Remove it with: $PSScriptRoot\uninstall-vdd.ps1 (elevated)" -ForegroundColor Yellow
    return
}

if (-not (Test-Admin)) { throw "Run this elevated (install.cmd does that for you)." }

Write-Host "Installing Voxl Display" -ForegroundColor White

# 1. Files.
if (-not (Test-Path $Target)) { New-Item -ItemType Directory -Path $Target -Force | Out-Null }
foreach ($f in @("VoxlDisplay.exe", "voxl.exe")) {
    $src = Join-Path $Source $f
    if (-not (Test-Path $src)) { throw "$f is missing next to install.ps1" }
    Copy-Item $src (Join-Path $Target $f) -Force
    Write-Step "copied $f"
}

# 2. Virtual display driver: only when the monitor is not there yet.
$monitor = Get-PnpDevice -Class Monitor -ErrorAction SilentlyContinue |
    Where-Object { $_.InstanceId -like "*MTT1337*" -and $_.Status -eq "OK" }
if ($monitor) {
    Write-Step "virtual display already installed"
} else {
    # The driver script and its packages ship next to this one, so the install works
    # offline. Missing files are a hard error: skipping here would leave a "Virtual
    # display not found" app with no clue why.
    $driverScript = Join-Path $Source "install-vdd.ps1"
    if (-not (Test-Path $driverScript)) { $driverScript = Join-Path (Split-Path $Source -Parent) "install-vdd.ps1" }
    if (-not (Test-Path $driverScript)) { throw "install-vdd.ps1 is missing next to install.ps1" }
    Write-Step "installing the virtual display driver (a few seconds)"
    & $driverScript -VendorDir (Join-Path $Source "vendor") -Settings (Join-Path $Source "vdd_settings.xml")
    if (-not $?) { throw "the virtual display driver did not install, see the messages above" }
}

# 3. Firewall: the app is reachable from the LAN only, never from the internet.
# Recreated every time: an upgrade moves the program, and a rule pointing at the old
# path silently blocks the tablet.
$rule = Get-NetFirewallRule -DisplayName "voxl 6901 (LAN)" -ErrorAction SilentlyContinue
if ($rule) { $rule | Remove-NetFirewallRule }
New-NetFirewallRule -DisplayName "voxl 6901 (LAN)" -Direction Inbound -Action Allow `
    -Protocol TCP -LocalPort 6901 -RemoteAddress LocalSubnet -Profile Private, Public `
    -Program (Join-Path $Target "voxl.exe") | Out-Null
Write-Step "firewall rule set for $Target\voxl.exe (LAN only)"

# 4. Context icon, before the shortcuts point at it.
$icon = Join-Path $Source "voxl.ico"
if (Test-Path $icon) { Copy-Item $icon (Join-Path $Target "voxl.ico") -Force }

# 5. Shortcuts.
$ws = New-Object -ComObject WScript.Shell
foreach ($dir in @($desktop, $startMenu)) {
    $link = $ws.CreateShortcut((Join-Path $dir $shortcutName))
    $link.TargetPath = Join-Path $Target "VoxlDisplay.exe"
    $link.WorkingDirectory = $Target
    $link.IconLocation = (Join-Path $Target "voxl.ico") + ",0"
    $link.Description = "Second monitor for the tablet"
    $link.Save()
    Write-Step "shortcut in $dir"
}
if (Test-Path $legacyShortcut) { Remove-Item $legacyShortcut -Force; Write-Step "removed the old shortcut" }

Write-Host ""
Write-Host "Done. Start it from the desktop shortcut 'Voxl Display'." -ForegroundColor Green
Write-Host "The password is in the app; the tablet opens https://<this-PC-IP>:6901" -ForegroundColor Gray

# SIG # Begin signature block
# MIIHYwYJKoZIhvcNAQcCoIIHVDCCB1ACAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBrWebBnnS3UNq5
# CnzgcshuW/z5tFNJXjId1TKjdVUQPKCCBD4wggQ6MIICoqADAgECAhAvDKecr0EG
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
# AgEVMC8GCSqGSIb3DQEJBDEiBCCzK1xZspY23qFh1QfVfJwE7ptJMae4RmYYZTlr
# iNZmTjANBgkqhkiG9w0BAQEFAASCAYBdOzhTfT/LBSzd2Dnc6FkAo8FHQV5Aent5
# y9H0PxlCRpiGiasQ5/5QNl/u0iR8xeKEHOrnKGWmwtH8T+d/He89kN3KCFFXleoo
# 3FDLbVvr2KPWyE0k8hETr0VxZh2FylT0UjZV0s0NEb3WQB/27qGlh5URiIas/oiT
# x9KZPyeC34nP/K3Bv9xWVpa8Ooh9sLSMXb+SNLvPoR3B9GDIV+luBlBdIMTJUTmI
# mXumzKtOBr18GrKOKIAVG8mp2FjgDbK+661m+6K/twdJ96e4nW5AGuDUuMVcOeGe
# 2A319rgr74ePa1yt74RKMrTKKvWj/2v7o2/wWGF/OqCC4biZcJHEMe0dGk9rsGsQ
# CO/FGmkj/zyF0SUCRVNYclfbYZGhBUuci/71vYn4Ahg1lUHKySk+vIEdaxEaShCw
# M+ibKyfyn/dnAZ8p+zyJLsUIFq47haJZtORw6XQCmSJrpOByfv/UuQasxMikDUZC
# UQWfpH9O7FlOpcR7zkBB6jmcSCtVf0w=
# SIG # End signature block
