param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Split-Path -Parent $ScriptRoot
$BuildToolsRoot = Join-Path $Root "build_tools"
$SevenZipTarget = Join-Path $BuildToolsRoot "7zip"
$LocalSevenZip = Join-Path $SevenZipTarget "7z.exe"

function Get-EnvPath {
    param([Parameter(Mandatory=$true)][string]$Name)
    return [Environment]::GetEnvironmentVariable($Name)
}

function Get-SevenZipCommand {
    $programFiles = Get-EnvPath -Name 'ProgramFiles'
    $programFilesX86 = Get-EnvPath -Name 'ProgramFiles(x86)'
    $localAppData = Get-EnvPath -Name 'LOCALAPPDATA'

    $candidates = @(
        $LocalSevenZip,
        $(if ($programFiles) { Join-Path $programFiles '7-Zip\7z.exe' }),
        $(if ($programFilesX86) { Join-Path $programFilesX86 '7-Zip\7z.exe' }),
        $(if ($localAppData) { Join-Path $localAppData 'Microsoft\WindowsApps\7z.exe' }),
        $(if ($localAppData) { Join-Path $localAppData 'Microsoft\WindowsApps\NanaZipC.exe' }),
        $(if ($programFiles) { Join-Path $programFiles 'NanaZip\NanaZipC.exe' }),
        '7z.exe',
        '7zz.exe',
        '7za.exe',
        'NanaZipC.exe'
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { return (Get-Item $candidate).FullName }
        $cmd = Get-Command $candidate -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }

    return $null
}

function Get-SevenZipInstallerCandidates {
    $downloads = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads'
    $candidateFolders = @(
        (Join-Path $Root 'src\MusicShell.Wpf\Tools\Packages'),
        (Join-Path $Root 'Packages'),
        $Root,
        $downloads
    ) | Select-Object -Unique

    $candidates = New-Object System.Collections.Generic.List[string]
    foreach ($folder in $candidateFolders) {
        if (-not (Test-Path $folder)) { continue }
        Get-ChildItem -Path $folder -File -Filter '7z*-x64.exe' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            ForEach-Object { $candidates.Add($_.FullName) }
        Get-ChildItem -Path $folder -File -Filter '7z*-x86.exe' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            ForEach-Object { $candidates.Add($_.FullName) }
    }

    return $candidates | Select-Object -Unique
}

Write-Host "7-Zip publish hazirlik araci kontrol ediliyor..." -ForegroundColor Cyan

$existing = Get-SevenZipCommand
if ($existing) {
    Write-Host "7-Zip/NanaZip hazir: $existing" -ForegroundColor Green
    exit 0
}

if ($SkipInstaller) {
    Write-Host "7-Zip/NanaZip bulunamadi; installer kurulumu atlandi." -ForegroundColor Yellow
    exit 0
}

$installer = Get-SevenZipInstallerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $installer) {
    Write-Host "7-Zip installer bulunamadi. Opsiyonel hazirlik araci atlandi." -ForegroundColor Yellow
    Write-Host "Istersen 7z*-x64.exe dosyasini src\MusicShell.Wpf\Tools\Packages klasorune koyabilirsin." -ForegroundColor DarkGray
    exit 0
}

Write-Host "7-Zip installer bulundu: $installer" -ForegroundColor Green
Write-Host "Sadece publish hazirligi icin yerel klasore kuruluyor: $SevenZipTarget" -ForegroundColor Cyan

New-Item -ItemType Directory -Path $SevenZipTarget -Force | Out-Null

try {
    $argumentLine = "/S /D=$SevenZipTarget"
    $process = Start-Process -FilePath $installer -ArgumentList $argumentLine -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Write-Host "7-Zip installer cikis kodu: $($process.ExitCode)" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "7-Zip yerel kurulum denenemedi: $($_.Exception.Message)" -ForegroundColor Yellow
}

$sevenZip = Get-SevenZipCommand
if ($sevenZip) {
    Write-Host "7-Zip publish hazirlik araci hazir: $sevenZip" -ForegroundColor Green
    exit 0
}

$foundUnderTarget = Get-ChildItem -Path $SevenZipTarget -Filter '7z.exe' -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if ($foundUnderTarget) {
    Write-Host "7-Zip publish hazirlik araci hazir: $($foundUnderTarget.FullName)" -ForegroundColor Green
    exit 0
}

Write-Host "7-Zip otomatik hazirlanamadi. Uygulama yine publish olabilir; sadece arsiv acma hazirligi kisitli kalabilir." -ForegroundColor Yellow
exit 0