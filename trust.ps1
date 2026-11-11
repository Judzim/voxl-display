# Trusts the self-signed code signing certificate on this machine, so signed files show
# "Judzim" as the publisher instead of "Unknown publisher".
#
# Run elevated. This adds the public certificate to LocalMachine\TrustedPublisher and to
# LocalMachine\Root (a self-signed certificate is its own root).
[CmdletBinding()]
param(
    [string]$CerPath = (Join-Path $PSScriptRoot "signing\voxl-display.cer")
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this elevated (it writes to the machine certificate stores)."
}
if (-not (Test-Path $CerPath)) { throw "certificate not found: $CerPath" }

$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $CerPath
foreach ($store in @("TrustedPublisher", "Root")) {
    $path = "Cert:\LocalMachine\$store"
    if (Get-ChildItem $path | Where-Object Thumbprint -eq $cert.Thumbprint) {
        "already in $store"
        continue
    }
    Import-Certificate -FilePath $CerPath -CertStoreLocation $path | Out-Null
    "imported into $store"
}
"certificate: $($cert.Subject) ($($cert.Thumbprint))"

# SIG # Begin signature block
# MIIHYwYJKoZIhvcNAQcCoIIHVDCCB1ACAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCDaLYnVme5+wC5W
# pr+6UHcbsTsiGn4T8nCS/1uDR0irpaCCBD4wggQ6MIICoqADAgECAhAvDKecr0EG
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
# AgEVMC8GCSqGSIb3DQEJBDEiBCBi81xqFoaUHq30nfRnOjX5X0cbkKLneYzcTLFV
# Q2cpRDANBgkqhkiG9w0BAQEFAASCAYB9CdgLCq5Ppult1ProJtMx8Er9pm/oNxdC
# p1ftZzCTg95TANjf6fOIUGaRqwtjbP967jf6iYsPGLRHCcFgquqnNDAXiXx/TSyh
# wBTBXHKZzKpKfJicDiP1C/pLz6Qiq9bSy3ijnsChj/W/1XIEL/v37af4JIOuWqiW
# VcZTultvtGM1g1W7VTxJLpOC/tVjaAC27raTd0zLLI0E+2clGUp0+N6bHB/tmFh/
# FiO5ATBY32QKcL02NF8JXQiKxf0PVW888JE5n08PX0Sm1leiEsL3MyV/u3Hoh/OH
# jB7nbGtOOTOZ3F97Xo0hgxwpzOG4tPBGQXX14ciroGsngew+qjVqndcyjKTEf8iL
# FMVZUnuddOOV8Q2FRH0pjpWQyca6y8dPmyqfD4FVjDhUVvYO4nYkSyITpJ7KLz+3
# 4/I0zXKWsM/7HYfaHh9e5Jg7w+l2i9a2dn+Yx8raL6kAjNNN1E1jgsqyNx7biUC1
# mB/niO6i2GiY2j20KNIiNA00FWfkxpo=
# SIG # End signature block
