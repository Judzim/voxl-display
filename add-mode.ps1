# Adds a resolution to the virtual display driver's mode list and restarts the device so
# the driver picks it up. Run elevated (the app starts it with -Verb RunAs).
#
#   .\add-mode.ps1 -Width 2000 -Height 1200
#
# Exit codes: 0 the mode is there, 1 something went wrong.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][int]$Width,
    [Parameter(Mandatory = $true)][int]$Height,
    [int]$Refresh = 60,
    [string]$Settings = "C:\VirtualDisplayDriver\vdd_settings.xml"
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "Run this elevated."
    exit 1
}

if (-not (Test-Path $Settings)) {
    Write-Error "not found: $Settings"
    exit 1
}

try {
    [xml]$doc = Get-Content $Settings -Raw
    $resolutions = $doc.vdd_settings.resolutions
    if (-not $resolutions) { throw "the settings file has no <resolutions> section" }

    $existing = $resolutions.resolution | Where-Object {
        [int]$_.width -eq $Width -and [int]$_.height -eq $Height -and [int]$_.refresh_rate -eq $Refresh
    }
    if ($existing) {
        Write-Host "the mode $Width x $Height @ $Refresh Hz is already listed"
    } else {
        $node = $doc.CreateElement("resolution")
        foreach ($pair in @(@("width", $Width), @("height", $Height), @("refresh_rate", $Refresh))) {
            $child = $doc.CreateElement($pair[0])
            $child.InnerText = [string]$pair[1]
            $node.AppendChild($child) | Out-Null
        }
        $resolutions.AppendChild($node) | Out-Null
        $doc.Save($Settings)
        Write-Host "added $Width x $Height @ $Refresh Hz to $Settings"
    }
} catch {
    Write-Error $_.Exception.Message
    exit 1
}

# The driver reads the mode list when the device starts.
$device = Get-PnpDevice -Class Display -ErrorAction SilentlyContinue |
    Where-Object { $_.FriendlyName -eq "Virtual Display Driver" } |
    Select-Object -First 1
if ($device) {
    pnputil /restart-device "$($device.InstanceId)" | Out-Null
    Start-Sleep -Seconds 2
    Write-Host "restarted $($device.InstanceId)"
} else {
    Write-Host "the virtual display is not installed; the mode applies when it is"
}

exit 0

# SIG # Begin signature block
# MIIHYwYJKoZIhvcNAQcCoIIHVDCCB1ACAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBfNZ19qj1EFi3L
# mP+z+WDXxaLQMiq1zi4s2tdl+WUMxKCCBD4wggQ6MIICoqADAgECAhAvDKecr0EG
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
# AgEVMC8GCSqGSIb3DQEJBDEiBCCzZ4xfkJWCN/PAgJG4YMpqs5mqtG9SMl9O6fJU
# k8UsADANBgkqhkiG9w0BAQEFAASCAYB1iWU9j151sOz38h+1GAPm9UNuGslNHrpy
# qJMW7Au1+3ZVxbjei9S6pudGsx573ufJwhDEM+oy1pHzuhdL2v+I+TmSXGFfXkfV
# 07EqMWKp53k/NIFO2suYkt5ivRxcTxwgbYju7lIpxwrclsdD/FTmwyAtZvqJjdV3
# sLo+AtAXni9Uuhr9hJRwK72Y8U60UFsuQSnpjjbhOA4QI07rtYTPQ1/GPhHRzr7o
# +RhukHimJim78FQ8YZd3HppQqZXHPIk42i/HbJwiBmJfe/AIN95nf48NVRN4BVCb
# hUKPji5YC0X1TOz8ejbqcAg+ZwJEOctqbXhcbKR3eyfnV0eQfdO8wCbIK3t/6+rU
# QigBaqecDw7+xJeSl6rM3FakxOaaO1Tm5O+MtR+NVKBfGPesWN2WTOr1qWV83XAB
# dk51JT1z+H08gqtepZsLtZd3Lnj5MMK98Kr86Vn1s0haAaDVhBOJG/c2OCejpFIe
# x6sAxCAo7eGhSTS6jtJOTjZNOdgfojU=
# SIG # End signature block
