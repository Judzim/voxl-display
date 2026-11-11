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
