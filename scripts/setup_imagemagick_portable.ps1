$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Split-Path -Parent $ScriptRoot
$Package = Join-Path $Root "src\MusicShell.Wpf\Tools\Packages\ImageMagick-7.1.2-21-portable-Q16-x64.7z"
$Target = Join-Path $Root "src\MusicShell.Wpf\Tools\ImageMagick"
$Magick = Join-Path $Target "magick.exe"

Write-Host "ImageMagick portable hazirlaniyor..." -ForegroundColor Cyan

if (Test-Path $Magick) {
    Write-Host "ImageMagick zaten hazir: $Magick" -ForegroundColor Green
    exit 0
}

if (-not (Test-Path $Package)) {
    Write-Host "Portable paket bulunamadi: $Package" -ForegroundColor Yellow
    Write-Host "ImageMagick opsiyoneldir; uygulama yine calisir ancak bazi gorsel donusumleri sinirli kalabilir." -ForegroundColor Yellow
    exit 0
}

New-Item -ItemType Directory -Path $Target -Force | Out-Null

$programFiles = [Environment]::GetEnvironmentVariable('ProgramFiles')
$programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
$localAppData = [Environment]::GetEnvironmentVariable('LOCALAPPDATA')
$SevenZipCandidates = @(
    (Join-Path $Root "build_tools\7zip\7z.exe"),
    $(if ($programFiles) { Join-Path $programFiles "7-Zip\7z.exe" }),
    $(if ($programFilesX86) { Join-Path $programFilesX86 "7-Zip\7z.exe" }),
    $(if ($localAppData) { Join-Path $localAppData "Microsoft\WindowsApps\7z.exe" }),
    $(if ($localAppData) { Join-Path $localAppData "Microsoft\WindowsApps\NanaZipC.exe" }),
    $(if ($programFiles) { Join-Path $programFiles "NanaZip\NanaZipC.exe" }),
    "7z.exe",
    "7zz.exe",
    "7za.exe",
    "NanaZipC.exe"
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

$Extracted = $false
foreach ($candidate in $SevenZipCandidates) {
    try {
        if (Test-Path $candidate) {
            $cmd = [pscustomobject]@{ Source = (Resolve-Path $candidate).Path }
        }
        else {
            $cmd = Get-Command $candidate -ErrorAction SilentlyContinue
        }
        if ($cmd) {
            Write-Host "7-Zip ile cikariliyor: $($cmd.Source)" -ForegroundColor Cyan
            & $cmd.Source x $Package "-o$Target" -y | Out-Host
            $Extracted = $true
            break
        }
    } catch { }
}

if (-not $Extracted) {
    $tar = Get-Command "tar.exe" -ErrorAction SilentlyContinue
    if ($tar) {
        try {
            Write-Host "tar.exe ile cikariliyor..." -ForegroundColor Cyan
            & $tar.Source -xf $Package -C $Target
            $Extracted = $true
        } catch {
            Write-Host "tar.exe bu 7z paketini acamadi." -ForegroundColor Yellow
        }
    }
}

if (-not (Test-Path $Magick)) {
    Write-Host "ImageMagick otomatik cikarilamadi." -ForegroundColor Yellow
    Write-Host "Cozum: 7-Zip/NanaZip ile paketi su klasore manuel cikar:" -ForegroundColor Yellow
    Write-Host $Target -ForegroundColor Cyan
    Write-Host "Cikarim sonrasi su dosya olmalidir:" -ForegroundColor Yellow
    Write-Host $Magick -ForegroundColor Cyan
    exit 0
}

Write-Host "ImageMagick hazir: $Magick" -ForegroundColor Green