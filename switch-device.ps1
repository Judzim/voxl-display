# Switches the virtual display device on or off. The app calls this when it is not
# running elevated; pnputil needs administrator rights.
#
#   .\switch-device.ps1 -State off
#
# Exit codes: 0 done, 1 failed.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet("on", "off")][string]$State
)

$ErrorActionPreference = "Continue"

$device = Get-PnpDevice -Class Display -ErrorAction SilentlyContinue |
    Where-Object { $_.FriendlyName -eq "Virtual Display Driver" } |
    Select-Object -First 1
if (-not $device) {
    Write-Host "the virtual display device was not found"
    exit 1
}

$verb = if ($State -eq "on") { "/enable-device" } else { "/disable-device" }
$out = & pnputil $verb "$($device.InstanceId)" 2>&1 | Out-String
$log = Join-Path $PSScriptRoot "device-switch.log"
"$(Get-Date -Format o)  $State  $($device.InstanceId)`r`n$($out.Trim())" | Out-File -FilePath $log -Append -Encoding utf8
Write-Host $out.Trim()

if ($LASTEXITCODE -ne 0) { exit 1 }
Start-Sleep -Seconds 2
exit 0

# SIG # Begin signature block
# MIIHYwYJKoZIhvcNAQcCoIIHVDCCB1ACAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCD/R+T1a4Q27Yo/
# EAXe/OLdd48zhr00zHXp0t0UmBE7W6CCBD4wggQ6MIICoqADAgECAhAvDKecr0EG
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
# AgEVMC8GCSqGSIb3DQEJBDEiBCA8WZF6y6rW4CqshRBfAmNmwPPNmCGDzwn5AN+u
# CB98jzANBgkqhkiG9w0BAQEFAASCAYB0MU7fVu+XyO+qKMKwhq+YvbuAxjeWfTyw
# hJMlPoUZJMLVfQbMzdikZTQsUiiTU+UKTBMoB34drGeYvzlHHz9eyM7t09vm/7b0
# 9T5fMILQumHe1UD+cj64O7eJQ5IHaWvG3+IpxmughvYdP/lGWRZBGOB3Y6i+FOuE
# GX7fy8jlJkMDq95d7fOzENqCPJgxL0woD4uJcjMRdo4B5m6jyA/+f+xghvoOIigp
# fcNHpR5TxzrhEzlGSTR2Z8vCbmaq1dClRG0NCGBg8hNu8ZxKLpJgN+rpJMiV2DXA
# F2si3BXz8UxYgQb03js/IkBfE/RLsKHZ01ORLwGfCgHT73ytlW4yOTp1s2vwr+8V
# bCr4q24A4Qj/uqtbDl7VBwJCe9xu/Y+I682lez1Lzr3lxP/P4uPpGa3KOx974f1d
# WUrAZHLXLzfnJzQUiWIjkMzQq500AFptgCsCu4PSwh3G2IR7u2lhBW+ciKywgGgp
# EWjf9FmexHElXAY9ou5XPwx5QArVXCs=
# SIG # End signature block
