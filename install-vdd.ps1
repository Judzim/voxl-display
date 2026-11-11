# Installs the signed MTT virtual display driver (25.7.23) as a virtual monitor.
# Run elevated. Nothing here needs a reboot.
#
# Works offline: the two packages are expected in .\vendor next to this script.
#   vendor\nefcon_v1.21.1.zip                  (creates the device node)
#   vendor\VirtualDisplayDriver-x64.zip        (the driver package itself)
# If a package is missing it is downloaded instead, so a developer run still works.
#
# Rollback: .\uninstall-vdd.ps1
[CmdletBinding()]
param(
    [string]$DriverDir = "C:\VirtualDisplayDriver",
    [string]$VendorDir = "",
    [string]$WorkDir = (Join-Path $env:TEMP "voxl-display"),
    [string]$Settings = ""
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script elevated."
}

if (-not $VendorDir) {
    foreach ($candidate in @((Join-Path $PSScriptRoot "vendor"), (Join-Path (Split-Path $PSScriptRoot -Parent) "vendor"))) {
        if (Test-Path $candidate) { $VendorDir = $candidate; break }
    }
}
if (-not $VendorDir) { $VendorDir = "" }

New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null

function Get-Package([string]$name, [string]$url) {
    $local = if ($VendorDir) { Join-Path $VendorDir $name } else { "" }
    if ($local -and (Test-Path $local)) {
        Write-Host "  using bundled $name"
        return $local
    }
    $cached = Join-Path $WorkDir $name
    if (Test-Path $cached) { Write-Host "  using cached $name"; return $cached }
    Write-Host "  downloading $name"
    Invoke-WebRequest -Uri $url -OutFile $cached -UseBasicParsing
    return $cached
}

# 1. nefcon: the supported way to create a Root\MttVDD device node.
$nefconZip = Get-Package "nefcon_v1.21.1.zip" "https://github.com/nefarius/nefcon/releases/download/v1.21.1/nefcon_v1.21.1.zip"
$nefconDir = Join-Path $WorkDir "nefcon"
Expand-Archive -Path $nefconZip -DestinationPath $nefconDir -Force

$arch = if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "ARM64" } else { "x64" }
$nefcon = @("$arch\nefconw.exe", "$arch\nefcon.exe", "x64\nefconw.exe", "x64\nefcon.exe") |
    ForEach-Object { Join-Path $nefconDir $_ } |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1
if (-not $nefcon) { throw "nefcon executable not found in $nefconZip" }
"nefcon: $nefcon ($arch)"
$nefSigner = (Get-AuthenticodeSignature $nefcon).SignerCertificate.Subject
"nefcon signer: $nefSigner"

# 2. The driver package.
$driverZip = Get-Package "VirtualDisplayDriver-x64.zip" "https://github.com/VirtualDrivers/Virtual-Display-Driver/releases/download/25.7.23/VirtualDisplayDriver-x86.Driver.Only.zip"
$driverSrc = Join-Path $WorkDir "driver"
Expand-Archive -Path $driverZip -DestinationPath $driverSrc -Force
$pkg = Get-ChildItem -Path $driverSrc -Recurse -Filter "MttVDD.inf" | Select-Object -First 1
if (-not $pkg) { throw "MttVDD.inf not found in $driverZip" }
$pkgDir = $pkg.DirectoryName

$sig = Get-AuthenticodeSignature (Join-Path $pkgDir "mttvdd.cat")
"driver catalog: $($sig.Status) by $($sig.SignerCertificate.Subject)"
if ($sig.Status -ne "Valid") { throw "driver catalog signature is not valid: $($sig.Status)" }

# 3. Trust the publisher for this package only (it is not a WHQL package).
$certs = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2Collection
$certs.Import([System.IO.File]::ReadAllBytes((Join-Path $pkgDir "mttvdd.cat")))
foreach ($c in $certs) {
    if ($c.Subject -notlike "*SignPath*") { continue }
    $existing = Get-ChildItem Cert:\LocalMachine\TrustedPublisher | Where-Object Thumbprint -eq $c.Thumbprint
    if ($existing) { "certificate already trusted: $($c.Thumbprint)"; continue }
    $cer = Join-Path $WorkDir "$($c.Thumbprint).cer"
    [System.IO.File]::WriteAllBytes($cer, $c.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))
    Import-Certificate -FilePath $cer -CertStoreLocation "Cert:\LocalMachine\TrustedPublisher" | Out-Null
    "certificate trusted: $($c.Thumbprint)"
}

# 4. Driver files and the mode list the driver reads at start.
New-Item -ItemType Directory -Path $DriverDir -Force | Out-Null
foreach ($f in @("MttVDD.inf", "MttVDD.dll", "mttvdd.cat")) {
    Copy-Item (Join-Path $pkgDir $f) $DriverDir -Force
}
if ($Settings -and (Test-Path $Settings)) {
    Copy-Item $Settings (Join-Path $DriverDir "vdd_settings.xml") -Force
} elseif (-not (Test-Path (Join-Path $DriverDir "vdd_settings.xml"))) {
    Copy-Item (Join-Path $pkgDir "vdd_settings.xml") (Join-Path $DriverDir "vdd_settings.xml") -Force
}
"driver files in $DriverDir"

# 5. The device node.
& $nefcon install (Join-Path $DriverDir "MttVDD.inf") "Root\MttVDD"
Start-Sleep -Seconds 8

# 6. Restart an existing device node so the driver re-reads the mode list.
$node = Get-PnpDevice -Class Display -ErrorAction SilentlyContinue | Where-Object { $_.FriendlyName -eq "Virtual Display Driver" }
foreach ($n in $node) {
    pnputil /restart-device "$($n.InstanceId)" | Out-Null
    "restarted $($n.InstanceId) to pick up the mode list"
}
Start-Sleep -Seconds 3

# 7. Report.
Get-PnpDevice -Class Monitor -PresentOnly | Format-Table FriendlyName, Status, InstanceId -AutoSize

# SIG # Begin signature block
# MIIHYwYJKoZIhvcNAQcCoIIHVDCCB1ACAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCDJsWMhWiSYt3Ln
# 1TGjNXmZNtiBXB0q1ft7Fp23OoI+aKCCBD4wggQ6MIICoqADAgECAhAvDKecr0EG
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
# AgEVMC8GCSqGSIb3DQEJBDEiBCAVoJlhBzz6ELETixydwInmasGG18kGsmT6kH8O
# D6sPJTANBgkqhkiG9w0BAQEFAASCAYA0YbhCfJuBZtRyXeR+LitjKaWS5lTLNagW
# uZwZ7MUN8CFSDPvqy2Lg0QU9CyUqqZGJXaUUNb8uBK9Rt5koTEE6W6huii64+YbD
# yrzYAqNYcaC+5iEoDW4bo6B9EkjKk/K5PtbxRSTUiW8lBMBdWNtEBVmZOfhvkq9h
# Xe68qKkY1D40UKunnWeirZ1QeZSFL1eUXN+mbxP2C0zzx3lusG3v7g0iRJpFLhXM
# HpEEQ2M5qgYhrQEkhC9GFj42Nn1b1hXj9pDyJ8GvH2OzvBkqYojXung4d9G/y35d
# p/GIOEEJETAhIWjFMVPjKOcbhFAjtsY2LaBdip73s4nW2YNiJeN/E1Wa1ts0RDTt
# xz2G8Wu2y+GuQMYMbZKO0c9K7yZfqtXBjoCIz2IE8a7COpkfhd/kiu0vCOfba7AW
# yHMDX6mJDy7UJU6O3If8WBBOIWNrmNv+cOxAeb10Fsi2eKimLrHzqwm+fyTmLtF7
# jZFYDqm6Vnkkh/vrQ2kMisH0O8b/ZdI=
# SIG # End signature block
