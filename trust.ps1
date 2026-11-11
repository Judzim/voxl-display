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
