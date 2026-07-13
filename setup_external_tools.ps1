param(
    [switch]$IncludeLibreOffice,

    [switch]$UseLibreOfficeInstallerFallback,

    [switch]$DownloadLibreOfficeIfMissing
)

$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Invoke-OptionalScript {
    param(
        [Parameter(Mandatory=$true)][string]$ScriptName,
        [string[]]$Arguments = @()
    )

    $path = Join-Path $Root $ScriptName
    if (-not (Test-Path $path)) { return }

    Write-Host "Calistiriliyor: $ScriptName" -ForegroundColor Cyan
    try {
        & $path @Arguments
    }
    catch {
        Write-Host "$ScriptName tamamlanamadi: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "Uygulama calisir; ilgili motor Sistem Durumu ekraninda Eksik gorunur." -ForegroundColor Yellow
    }
}

Invoke-OptionalScript -ScriptName "setup_7zip_build_tool.ps1"
Invoke-OptionalScript -ScriptName "setup_imagemagick_portable.ps1"

if ($IncludeLibreOffice) {
    $libreOfficeArgs = @()
    if ($UseLibreOfficeInstallerFallback) {
        $libreOfficeArgs += '-UseInstallerFallback'
    }
    if ($DownloadLibreOfficeIfMissing) {
        $libreOfficeArgs += '-DownloadIfMissing'
    }
    Invoke-OptionalScript -ScriptName "setup_libreoffice_portable.ps1" -Arguments $libreOfficeArgs
}
else {
    Write-Host "LibreOffice Portable otomatik hazirlama atlandi." -ForegroundColor DarkYellow
    Write-Host "Gerekirse ayrica calistir: .\setup_libreoffice_portable.ps1" -ForegroundColor DarkGray
}
