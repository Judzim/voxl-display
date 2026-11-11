# Prints everything needed to tell why the virtual display is missing.
# Run it on the machine that has the problem and paste the output.
#
#   .\diagnose.ps1
#
# Reading is enough; no administrator rights are required.
[CmdletBinding()]
param()

function Line($text) { Write-Host $text }

Line "=== Windows ==="
$os = Get-CimInstance Win32_OperatingSystem
Line ("  {0} build {1} ({2})" -f $os.Caption, $os.BuildNumber, $os.OSArchitecture)
Line ("  IddCx needs Windows 10 1809 or newer; ARM64 is not supported")

Line ""
Line "=== GPU ==="
Get-CimInstance Win32_VideoController | ForEach-Object { Line ("  {0}  driver {1}" -f $_.Name, $_.DriverVersion) }

Line ""
Line "=== display devices (class Display) ==="
$virtual = Get-PnpDevice -Class Display -ErrorAction SilentlyContinue |
    Where-Object { $_.FriendlyName -eq "Virtual Display Driver" }
if ($virtual) {
    $virtual | ForEach-Object { Line ("  {0} | {1} | {2}" -f $_.FriendlyName, $_.Status, $_.InstanceId) }
} else {
    Line "  no 'Virtual Display Driver' device: the driver is not installed"
}

Line ""
Line "=== monitors (present) ==="
Get-PnpDevice -Class Monitor -PresentOnly -ErrorAction SilentlyContinue |
    ForEach-Object { Line ("  {0} | {1}" -f $_.FriendlyName, $_.InstanceId) }
$vddMonitor = Get-PnpDevice -Class Monitor -ErrorAction SilentlyContinue |
    Where-Object { $_.InstanceId -like "*MTT1337*" }
if (-not $vddMonitor) { Line "  no MTT1337 monitor at all" }

Line ""
Line "=== driver package in the driver store ==="
$enum = pnputil /enum-drivers /class Display 2>&1 | Out-String
$blocks = $enum -split "`r?`n`r?`n" | Where-Object { $_ -match "MttVDD" }
if ($blocks) { $blocks | ForEach-Object { Line ("  " + ($_ -replace "`r?`n", " / ")) } }
else { Line "  no MttVDD package in the driver store" }

Line ""
Line "=== files ==="
Line ("  C:\VirtualDisplayDriver : {0}" -f (Test-Path "C:\VirtualDisplayDriver\MttVDD.inf"))
Line ("  app folder              : {0}" -f (Test-Path "$env:LOCALAPPDATA\VoxlDisplay\VoxlDisplay.exe"))
Line ("  vendor packages         : {0}" -f (Test-Path "$env:LOCALAPPDATA\VoxlDisplay\vendor"))

Line ""
Line "=== signing certificate trusted? ==="
$cert = Get-ChildItem Cert:\LocalMachine\TrustedPublisher -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -like "*SignPath*" }
if ($cert) { Line ("  yes: " + $cert.Subject) } else { Line "  no SignPath certificate in TrustedPublisher" }

Line ""
Line "=== firewall rule ==="
$rule = Get-NetFirewallRule -DisplayName "voxl 6901 (LAN)" -ErrorAction SilentlyContinue
if ($rule) { Line ("  " + (($rule | Get-NetFirewallApplicationFilter).Program)) } else { Line "  missing" }

Line ""
Line "=== attached displays (what voxl can stream) ==="
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Screen]::AllScreens | ForEach-Object { Line ("  {0} primary={1} {2}" -f $_.DeviceName, $_.Primary, $_.Bounds) }

Line ""
Line "=== last install log, if any ==="
$log = "$env:LOCALAPPDATA\VoxlDisplay\install.log"
if (Test-Path $log) { Get-Content $log -Tail 20 | ForEach-Object { Line ("  " + $_) } }
else { Line "  none (install.ps1 does not write one; the app's own messages are in the window)" }

# SIG # Begin signature block
# MIIHYwYJKoZIhvcNAQcCoIIHVDCCB1ACAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCAje+ubQV/3QaEL
# /dwc0JXqqCTfiKa65xmmLCi+S+XZtqCCBD4wggQ6MIICoqADAgECAhAvDKecr0EG
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
# AgEVMC8GCSqGSIb3DQEJBDEiBCCXCe9ke5QBlPfM0k6+vme5sPxZVQy41XTmJC36
# zR+g8TANBgkqhkiG9w0BAQEFAASCAYCOffy2UOpRipilGHvWMAidc6YbC2MpI0VQ
# fgvuMqakZiWmi1vJ6YHJ3tYl6mkO01utwk1hcBmZF+BXBewD3Pkeg5gYH/Q6qPc+
# aDEoUG7xIz6eFc1yD58jha1Zru90znZGAOUN0k2fAo0ca6qegAuBYXSViPChD7qj
# 5gybxjX7WluM0u63VTSKncWvltCUUH7trMqwGEPSoGxKvVc/yhxe+MhbnAQpgFVQ
# IWNzWew2hjz58A19Xas6dPCJIla9sqaQcpvthj33LzenwlfibFMDBR5pzBLYiDvt
# 4CXQfx49i4dkU6QairBR5Z6d3+bYgg7rI7rqY62q0C0gJeeoubaenJHO5FxatV++
# PLV0HhM9ZEhrcKa28Tc8WTXbQXvewaEYm+QHMCWo2xS+B5BClVGmtEpIqg5INHQk
# LGqUQ7v0oaCb9C8sHqKddeMdbaTJSyGCI2iZj8XiKxXQ6qWIbcYC5P4y5774C//x
# jeiDk6Uy4Bzru30QIByaMHzCKWLQKLw=
# SIG # End signature block
