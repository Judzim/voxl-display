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
Write-Host $out.Trim()

if ($LASTEXITCODE -ne 0) { exit 1 }
Start-Sleep -Seconds 2
exit 0

# SIG # Begin signature block
# MIIHYwYJKoZIhvcNAQcCoIIHVDCCB1ACAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBiSRhL0zCnCpmW
# 3g5gJ0KirA/DF44Dc5tmSS0xBo7JraCCBD4wggQ6MIICoqADAgECAhAvDKecr0EG
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
# AgEVMC8GCSqGSIb3DQEJBDEiBCClNgQ4patWbLt4h1U9Yw1pINJAVzvD97/E0NIQ
# Gyz+rzANBgkqhkiG9w0BAQEFAASCAYByIrfFS7Oc1qZFRDTaWA/Kw/b0w+D/myyx
# 7tWOu1WkGLOWuJuhAuGgQH+OyUFjCXcUrb2INGmvTDYmC3IneKnHEUsE4l7SkOQx
# aWU+pYIYTvBeTCVjBfFdDtFBTkpnA57f7wxR+mKP8q8P014iUqdcezdpjOZtIDp5
# yTHbs4fX4L8apvP5iKb/sFdkfWaiAvztPBKZOnMi7ijVDaZwlMETcGpcMgUMj5Mj
# enoJ7a0bGI3r0fj2jAZp7yRABG9ar4l+eBIL4UJnfQlJ2jHrAvMMxw0DlxqT1xzJ
# wlj6Rns2fp4T3OQeLhCsPjRBlA/C5DUFqr79qo0GzcmUmlgJ5Uu8l68cUieSE25X
# Mi2SFSMq7RffQpSKCYbzt6xfxnf8kD5uKw5vNBEIB7u14fwZLa1PWh8xuLML9754
# gz9ocp1QEufyCZSZlT6e8GoAhxUISIyBTV+2SdGye5V7uAVfmQTGE6/1gYr1pz+S
# fR7nfB1mOahH1fMpF8RW+IF0uXnnwtw=
# SIG # End signature block
