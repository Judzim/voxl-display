# Signs the app with a self-signed code signing certificate.
#
#   .\sign.ps1            create the certificate if needed, then sign
#   .\sign.ps1 -NewCert   replace the certificate
#
# The certificate lives in Cert:\CurrentUser\My and its public part is exported to
# .\signing\voxl-display.cer so people can trust it:
#   Import-Certificate -FilePath .\signing\voxl-display.cer -CertStoreLocation Cert:\LocalMachine\TrustedPublisher
#
# A self-signed signature removes "Unknown publisher" on machines that trust the
# certificate. Windows SmartScreen still warns until the file earns reputation; that
# part needs a commercial certificate and is not something this script can do.
[CmdletBinding()]
param(
    [switch]$NewCert,
    [string]$Subject = "CN=Judzim, O=Voxl Display, C=SK"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$signing = Join-Path $root "signing"
New-Item -ItemType Directory -Path $signing -Force | Out-Null

$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $Subject -and $_.HasPrivateKey } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($NewCert -or -not $cert) {
    if ($cert) { Remove-Item ("Cert:\CurrentUser\My\" + $cert.Thumbprint) -Force }
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -KeyUsage DigitalSignature `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(10)
    "created certificate $($cert.Thumbprint)"
}

$cerPath = Join-Path $signing "voxl-display.cer"
Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
"public certificate: $cerPath"

# Authenticode covers PE files and PowerShell scripts; a .cmd is plain text and cannot
# carry a signature, so it is not in this list.
$targets = Get-ChildItem -Path $root -File |
    Where-Object { $_.Extension -in ".exe", ".ps1" } |
    Where-Object { $_.Name -ne "sign.ps1" -and $_.Name -ne "voxl.exe" }

foreach ($file in $targets) {
    try {
        # A timestamp keeps the signature valid after the certificate expires.
        $sig = Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $cert `
            -TimestampServer "http://timestamp.digicert.com" -HashAlgorithm SHA256 -ErrorAction Stop
        if ($sig.Status -ne "Valid") {
            $sig = Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $cert -HashAlgorithm SHA256 -ErrorAction Stop
        }
        "{0,-22} {1} {2}" -f $file.Name, $sig.Status, $sig.StatusMessage
    } catch {
        "{0,-22} skipped ({1})" -f $file.Name, $_.Exception.Message.Split([char]10)[0]
    }
}
