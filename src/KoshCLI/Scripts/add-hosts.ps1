# add-hosts.ps1
param(
    [string[]]$Domains
)

# -------------------------------
# 1) Check for admin rights
# -------------------------------
$IsAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent() `
).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $IsAdmin) {
    Write-Host "Elevating to Administrator..."

    # Re-run this script as admin
    $argList = @(
        "-NoProfile",
        "-ExecutionPolicy Bypass",
        "-File `"$PSCommandPath`""
    ) + $Domains

    Start-Process powershell.exe -Verb RunAs -ArgumentList $argList
    exit
}

# -------------------------------
# 2) Actual logic (runs as admin)
# -------------------------------
$HostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
$IP = "127.0.0.1"

Write-Host "Editing hosts file: $HostsPath"

foreach ($domain in $Domains) {

    # Escape dots for regex
    $escaped = $domain.Replace(".", "\.")

    # Check if entry already exists
    if (Select-String -Path $HostsPath -Pattern "^\s*$IP\s+$escaped\s*$" -Quiet) {
        Write-Host "Exists: $domain"
        continue
    }

    # Append new entry
    "$IP $domain" | Out-File -FilePath $HostsPath -Encoding utf8 -Append
    Write-Host "Added: $domain"
}

Write-Host "Done!"