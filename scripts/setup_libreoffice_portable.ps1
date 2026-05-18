param(
    [switch]$UseInstallerFallback,
    [switch]$DownloadIfMissing
)

$ErrorActionPreference = 'Stop'

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Split-Path -Parent $ScriptRoot
$ToolsRoot = Join-Path $Root 'src\MusicShell.Wpf\Tools'
$Target = Join-Path $ToolsRoot 'LibreOfficePortable'
$ExpectedSoffice = Join-Path $Target 'App\libreoffice\program\soffice.exe'
$ExpectedSofficeAlt = Join-Path $Target 'App\LibreOffice\program\soffice.exe'
$LibreOfficePortableVersion = '26.2.1'
$PackageBaseName = "LibreOfficePortable_${LibreOfficePortableVersion}_MultilingualStandard.paf"
$PackageNames = @("$PackageBaseName.zip", "$PackageBaseName.exe")

Write-Host 'LibreOffice Portable hazirlaniyor...' -ForegroundColor Cyan

function Get-LibreOfficeSofficePath {
    $known = @(
        $ExpectedSoffice,
        $ExpectedSofficeAlt,
        (Join-Path $Target 'LibreOfficePortable\App\libreoffice\program\soffice.exe'),
        (Join-Path $Target 'LibreOfficePortable\App\LibreOffice\program\soffice.exe')
    ) | Select-Object -Unique

    foreach ($path in $known) {
        if (Test-Path $path) { return $path }
    }

    if (Test-Path $Target) {
        $found = Get-ChildItem -Path $Target -Filter 'soffice.exe' -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '(?i)\\App\\(libreoffice|LibreOffice)\\program\\soffice\.exe$' } |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    return $null
}

function Normalize-LibreOfficeFolder {
    $soffice = Get-LibreOfficeSofficePath
    if (-not $soffice) { return $null }

    $nestedRoot = Join-Path $Target 'LibreOfficePortable'
    $nestedSoffice = Join-Path $nestedRoot 'App\libreoffice\program\soffice.exe'
    $nestedSofficeAlt = Join-Path $nestedRoot 'App\LibreOffice\program\soffice.exe'

    if ((Test-Path $nestedSoffice) -or (Test-Path $nestedSofficeAlt)) {
        Write-Host 'Ic ice LibreOfficePortable klasoru algilandi, duzeltiliyor...' -ForegroundColor Yellow
        $tempMove = Join-Path ([System.IO.Path]::GetTempPath()) ('ToolBridgeLibreOfficeMove_' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $tempMove -Force | Out-Null
        Get-ChildItem -Path $nestedRoot -Force | ForEach-Object { Move-Item -Path $_.FullName -Destination $tempMove -Force }
        Remove-Item $Target -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path $Target -Force | Out-Null
        Get-ChildItem -Path $tempMove -Force | ForEach-Object { Move-Item -Path $_.FullName -Destination $Target -Force }
        Remove-Item $tempMove -Recurse -Force -ErrorAction SilentlyContinue
    }

    return Get-LibreOfficeSofficePath
}

function Get-ToolCommand {
    param([string[]]$Candidates)
    foreach ($candidate in $Candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        if (Test-Path $candidate) { return (Get-Item $candidate).FullName }
        $cmd = Get-Command $candidate -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }
    return $null
}

function Get-SevenZipTool {
    $programFiles = [Environment]::GetEnvironmentVariable('ProgramFiles')
    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    $localAppData = [Environment]::GetEnvironmentVariable('LOCALAPPDATA')

    return Get-ToolCommand -Candidates @(
        (Join-Path $Root 'build_tools\7zip\7z.exe'),
        $(if ($programFiles) { Join-Path $programFiles '7-Zip\7z.exe' }),
        $(if ($programFilesX86) { Join-Path $programFilesX86 '7-Zip\7z.exe' }),
        $(if ($localAppData) { Join-Path $localAppData 'Microsoft\WindowsApps\7z.exe' }),
        $(if ($localAppData) { Join-Path $localAppData 'Microsoft\WindowsApps\NanaZipC.exe' }),
        $(if ($programFiles) { Join-Path $programFiles 'NanaZip\NanaZipC.exe' }),
        '7z.exe', '7zz.exe', '7za.exe', 'NanaZipC.exe'
    )
}

function Test-IsPortableAppsInstallerBinary {
    param([Parameter(Mandatory=$true)][string]$Path)
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            if ($stream.Length -lt 2) { return $false }
            return (($stream.ReadByte() -eq 0x4D) -and ($stream.ReadByte() -eq 0x5A))
        }
        finally { $stream.Dispose() }
    }
    catch { return $false }
}

function Get-LibreOfficePackageCandidates {
    $candidateFolders = @(
        (Join-Path $Root 'src\MusicShell.Wpf\Tools\Packages'),
        $Root,
        (Join-Path $Root 'Packages'),
        (Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads')
    ) | Select-Object -Unique

    $candidates = New-Object System.Collections.Generic.List[string]
    foreach ($folder in $candidateFolders) {
        foreach ($name in $PackageNames) { $candidates.Add((Join-Path $folder $name)) }
        if (Test-Path $folder) {
            Get-ChildItem -Path $folder -File -Filter 'LibreOfficePortable_*_MultilingualStandard.paf.*' -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending |
                ForEach-Object { $candidates.Add($_.FullName) }
        }
    }
    return $candidates | Select-Object -Unique
}

function Invoke-LibreOfficePackageDownload {
    param([Parameter(Mandatory=$true)][string]$DestinationFolder)

    $downloadName = "$PackageBaseName.exe"
    $destination = Join-Path $DestinationFolder $downloadName
    if (Test-Path $destination) { return $destination }

    $urls = @(
        "https://download.nust.na/pub2/DocumentFoundation/libreoffice/portable/26.2.1/$downloadName",
        "https://ftp.osuosl.org/pub/tdf/libreoffice/portable/26.2.1/$downloadName",
        "https://ftp.jaist.ac.jp/pub/tdf/libreoffice/portable/26.2.1/$downloadName",
        "https://softlibre.unizar.es/tdf/libreoffice/portable/26.2.1/$downloadName"
    )

    New-Item -ItemType Directory -Path $DestinationFolder -Force | Out-Null
    foreach ($url in $urls) {
        try {
            Write-Host "LibreOffice Portable indiriliyor: $url" -ForegroundColor Cyan
            Invoke-WebRequest -Uri $url -OutFile $destination -UseBasicParsing
            if ((Test-Path $destination) -and ((Get-Item $destination).Length -gt 100MB)) { return $destination }
            Remove-Item $destination -Force -ErrorAction SilentlyContinue
        }
        catch {
            Write-Host "Indirme basarisiz: $($_.Exception.Message)" -ForegroundColor Yellow
            Remove-Item $destination -Force -ErrorAction SilentlyContinue
        }
    }
    return $null
}

function Resolve-PortableAppsInstaller {
    param([Parameter(Mandatory=$true)][string]$PackagePath, [Parameter(Mandatory=$true)][string]$TempRoot)

    if (Test-IsPortableAppsInstallerBinary -Path $PackagePath) {
        $installerCopy = Join-Path $TempRoot ('LibreOfficePortable_' + [Guid]::NewGuid().ToString('N') + '.paf.exe')
        Copy-Item -Path $PackagePath -Destination $installerCopy -Force
        return Get-Item $installerCopy
    }

    $sevenZip = Get-SevenZipTool
    if ($sevenZip) {
        & $sevenZip x $PackagePath "-o$TempRoot" -y | Out-Host
    }
    else {
        Expand-Archive -Path $PackagePath -DestinationPath $TempRoot -Force
    }

    return Get-ChildItem -Path $TempRoot -Filter '*.paf.exe' -File -Recurse | Select-Object -First 1
}

function Copy-ExtractedLibreOffice {
    param([Parameter(Mandatory=$true)][string]$ExtractRoot)

    $extractedSoffice = Get-ChildItem -Path $ExtractRoot -Filter 'soffice.exe' -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '(?i)\\App\\(libreoffice|LibreOffice)\\program\\soffice\.exe$' } |
        Select-Object -First 1

    if (-not $extractedSoffice) { return $false }

    $portableRoot = $extractedSoffice.Directory.Parent.Parent.Parent.FullName
    if (Test-Path $Target) { Remove-Item $Target -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Path $Target -Force | Out-Null
    Get-ChildItem -Path $portableRoot -Force | ForEach-Object { Move-Item -Path $_.FullName -Destination $Target -Force }
    return [bool](Normalize-LibreOfficeFolder)
}

function Invoke-InstallerWithTimeout {
    param([Parameter(Mandatory=$true)][string]$InstallerPath, [int]$TimeoutSeconds = 420)

    Write-Host 'LibreOffice Portable sessiz kuruluyor...' -ForegroundColor Cyan
    $process = Start-Process -FilePath $InstallerPath -ArgumentList @('/SILENT', "/DESTINATION=$ToolsRoot") -PassThru
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while (-not $process.HasExited) {
        if (Normalize-LibreOfficeFolder) {
            if ($process.WaitForExit(30000)) { break }
        }
        if ((Get-Date) -gt $deadline) {
            try { & taskkill.exe /PID $process.Id /T /F | Out-Null } catch { try { $process.Kill() } catch { } }
            return $false
        }
        Start-Sleep -Seconds 2
    }
    return [bool](Normalize-LibreOfficeFolder)
}

$readyPath = Normalize-LibreOfficeFolder
if ($readyPath) {
    Write-Host "LibreOffice Portable zaten hazir: $readyPath" -ForegroundColor Green
    exit 0
}

$Package = Get-LibreOfficePackageCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $Package -and $DownloadIfMissing) {
    $Package = Invoke-LibreOfficePackageDownload -DestinationFolder (Join-Path $Root 'src\MusicShell.Wpf\Tools\Packages')
}

if (-not $Package) {
    Write-Host 'LibreOffice Portable paketi bulunamadi.' -ForegroundColor Yellow
    Write-Host 'Internet varsa: .\scripts\setup_libreoffice_portable.ps1 -DownloadIfMissing -UseInstallerFallback' -ForegroundColor DarkGray
    exit 0
}

Write-Host "Paket bulundu: $Package" -ForegroundColor Green
New-Item -ItemType Directory -Path $ToolsRoot -Force | Out-Null
$Temp = Join-Path ([System.IO.Path]::GetTempPath()) ('ToolBridgeLibreOffice_' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Temp -Force | Out-Null

try {
    $Installer = Resolve-PortableAppsInstaller -PackagePath $Package -TempRoot $Temp
    if (-not $Installer) { throw 'Paket icinde .paf.exe bulunamadi.' }

    $sevenZip = Get-SevenZipTool
    if ($sevenZip) {
        Write-Host "7-Zip/NanaZip ile PAF icerigi cikariliyor: $sevenZip" -ForegroundColor Cyan
        $extractTemp = Join-Path $Temp 'paf_extract'
        New-Item -ItemType Directory -Path $extractTemp -Force | Out-Null
        & $sevenZip x $Installer.FullName "-o$extractTemp" -y | Out-Host
        [void](Copy-ExtractedLibreOffice -ExtractRoot $extractTemp)
    }

    if (-not (Normalize-LibreOfficeFolder) -and $UseInstallerFallback) {
        [void](Invoke-InstallerWithTimeout -InstallerPath $Installer.FullName)
    }

    $readyPath = Normalize-LibreOfficeFolder
    if (-not $readyPath) {
        Write-Host 'LibreOffice Portable otomatik hazirlanamadi.' -ForegroundColor Yellow
        Write-Host "Beklenen dosya: $ExpectedSoffice" -ForegroundColor Yellow
        exit 0
    }

    Write-Host "LibreOffice Portable hazir: $readyPath" -ForegroundColor Green
}
finally {
    try { Remove-Item $Temp -Recurse -Force -ErrorAction SilentlyContinue } catch { }
}
