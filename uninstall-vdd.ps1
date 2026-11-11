# Removes everything install-vdd.ps1 added: the device node, the driver package,
# the publisher certificate it trusted, its files, and any monitor layout change is
# left to Windows (the virtual monitor simply disappears).
# Run elevated. Nothing here needs a reboot.
[CmdletBinding()]
param(
    [string]$DriverDir = "C:\VirtualDisplayDriver"
)

$ErrorActionPreference = "Continue"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script elevated."
}

# 1. The device node (and its monitor child). The instance is ROOT\DISPLAY\0000 with
#    hardware id Root\MttVDD, so match the driver's friendly name, which is stable.
$dev = Get-PnpDevice -Class Display -ErrorAction SilentlyContinue | Where-Object { $_.FriendlyName -eq "Virtual Display Driver" }
foreach ($d in $dev) {
    "removing device: $($d.InstanceId)"
    pnputil /remove-device "$($d.InstanceId)"
}
pnputil /scan-devices

# 2. The driver package in the driver store.
$enum = (pnputil /enum-drivers /class Display | Out-String)
foreach ($block in ($enum -split "`r?`n`r?`n")) {
    if ($block -match "Original Name:\s+MttVDD\.inf" -and $block -match "Published Name:\s+(oem\d+\.inf)") {
        "deleting driver package: $($Matches[1])"
        pnputil /delete-driver $Matches[1] /uninstall /force
    }
}

# 3. The publisher certificate trusted for it (only the SignPath one this install added).
Get-ChildItem Cert:\LocalMachine\TrustedPublisher | Where-Object { $_.Subject -like "*SignPath Foundation*" } | ForEach-Object {
    "removing trusted publisher: $($_.Thumbprint)"
    Remove-Item -Path ("Cert:\LocalMachine\TrustedPublisher\" + $_.Thumbprint) -Force
}

# 4. Files.
if (Test-Path $DriverDir) {
    "removing $DriverDir"
    Remove-Item -Path $DriverDir -Recurse -Force
}

# 5. State afterwards.
Get-PnpDevice -Class Monitor | Format-Table FriendlyName, Status, InstanceId -AutoSize

# SIG # Begin signature block
# MIIHYwYJKoZIhvcNAQcCoIIHVDCCB1ACAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBLrxDXp0k/0PWx
# hmWn3xV0hD2h7DgcXKUSjRw6rR1HsKCCBD4wggQ6MIICoqADAgECAhAvDKecr0EG
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
# AgEVMC8GCSqGSIb3DQEJBDEiBCDY9BEKJvnF7JibNrvdm0kjvm/j7CbU8PS/4r0c
# UhjoeTANBgkqhkiG9w0BAQEFAASCAYCNbScxstDGDm3QUT7Dq2l2KtO2U60UH7Nh
# Rldehw96dxIfHQy+cfj4k9EY7WPN7/TBu0dOrYrOYNuZ4iTUBGzs3Z3mxe8YbgJc
# Ou3xQagO/qq+QSmcAyRdbBPR0GzzrFiVjybhcXdGjIMABef5TsjqTQ5xK19Fuwu9
# Kmc1Ftm59p3Gs2Mtr2OShliIrBIssw0hbHCSepsJsKDh/iHWUfQu4po0n7Hg23W1
# os3yr9o5PLs7xS8UhcMMEV5uuoOQzT6oXZ8lgBwu+MdDqf/goq/NzPLCXNILUdb1
# mblULRYNQ5l3zsYDgSx150iNcdibGessUFT00eapqCNbcqsoB2TR2uSxrq+x0wzy
# 6tT+mbPyjbZZC+35orC3uXZSHuVqgmGNkCMWwS2xwJVTKxXfzIi54ScK/2Gt/a8H
# PYYqngdZXke7NmSsomc4zG46uyxZv8DMjRIuSUubSN973/lehdNgWxbayRGglqG0
# y2wrHdEyWlD8xm/59BccMa9w0epcSYg=
# SIG # End signature block
