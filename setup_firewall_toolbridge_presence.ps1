param(
    [int]$PresencePort = 47892,
    [int]$TransferPort = 47893,
    [string]$PresenceRuleName = 'ToolBridge LAN Presence UDP',
    [string]$TransferRuleName = 'ToolBridge LAN File Transfer TCP'
)

$ErrorActionPreference = 'Stop'

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Bu script yönetici olarak çalıştırılmalıdır. PowerShell için Run as Administrator kullanın.'
}

function Ensure-FirewallRule {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][ValidateSet('TCP','UDP')][string]$Protocol,
        [Parameter(Mandatory=$true)][int]$Port
    )

    $existingRule = Get-NetFirewallRule -DisplayName $Name -ErrorAction SilentlyContinue
    if ($existingRule) {
        Write-Host "Firewall kuralı zaten var: $Name" -ForegroundColor Green
        return
    }

    New-NetFirewallRule `
        -DisplayName $Name `
        -Direction Inbound `
        -Action Allow `
        -Protocol $Protocol `
        -LocalPort $Port `
        -Profile Domain,Private | Out-Null

    Write-Host "Firewall kuralı eklendi: $Name / $Protocol $Port" -ForegroundColor Green
}

Ensure-FirewallRule -Name $PresenceRuleName -Protocol UDP -Port $PresencePort
Ensure-FirewallRule -Name $TransferRuleName -Protocol TCP -Port $TransferPort

# ToolBridge outbound UDP presence rule for restricted corporate networks.
if (-not (Get-NetFirewallRule -DisplayName 'ToolBridge LAN Presence UDP Outbound' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'ToolBridge LAN Presence UDP Outbound' -Direction Outbound -Action Allow -Protocol UDP -LocalPort Any -RemotePort $PresencePort | Out-Null
    Write-Host 'Firewall rule added: ToolBridge LAN Presence UDP Outbound / UDP 47892'
}
else {
    Write-Host 'Firewall rule already exists: ToolBridge LAN Presence UDP Outbound'
}