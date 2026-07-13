param(
    [switch]$UseInstallerFallback,

    [switch]$DownloadIfMissing
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$ToolsRoot = Join-Path $Root "src\MusicShell.Wpf\Tools"
$Target = Join-Path $ToolsRoot "LibreOfficePortable"
$ExpectedSoffice = Join-Path $Target "App\libreoffice\program\soffice.exe"
$ExpectedSofficeAlt = Join-Path $Target "App\LibreOffice\program\soffice.exe"
$LibreOfficePortableVersion = "26.2.1"
$PackageBaseName = "LibreOfficePortable_${LibreOfficePortableVersion}_MultilingualStandard.paf"
$PackageNames = @("$PackageBaseName.zip", "$PackageBaseName.exe")

Write-Host "LibreOffice Portable hazirlaniyor..." -ForegroundColor Cyan

function Get-LibreOfficeSofficePath {
    $known = @(
        $ExpectedSoffice,
        $ExpectedSofficeAlt,
        (Join-Path $Target "LibreOfficePortable\App\libreoffice\program\soffice.exe"),
        (Join-Path $Target "LibreOfficePortable\App\LibreOffice\program\soffice.exe")
    ) | Select-Object -Unique

    foreach ($path in $known) {
        if (Test-Path $path) { return $path }
    }

    if (Test-Path $Target) {
        $found = Get-ChildItem -Path $Target -Filter "soffice.exe" -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "(?i)\\App\\(libreoffice|LibreOffice)\\program\\soffice\.exe$" } |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    return $null
}

function Normalize-LibreOfficeFolder {
    $soffice = Get-LibreOfficeSofficePath
    if (-not $soffice) { return $null }

    # PortableApps kurucusu bazi sistemlerde LibreOfficePortable\LibreOfficePortable seklinde ic ice klasor olusturabiliyor.
    $nestedRoot = Join-Path $Target "LibreOfficePortable"
    $nestedSoffice = Join-Path $nestedRoot "App\libreoffice\program\soffice.exe"
    $nestedSofficeAlt = Join-Path $nestedRoot "App\LibreOffice\program\soffice.exe"

    if ((Test-Path $nestedSoffice) -or (Test-Path $nestedSofficeAlt)) {
        Write-Host "Ic ice LibreOfficePortable klasoru algilandi, duzeltiliyor..." -ForegroundColor Yellow
        $tempMove = Join-Path ([System.IO.Path]::GetTempPath()) ("ToolBridgeLibreOfficeMove_" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $tempMove -Force | Out-Null
        Get-ChildItem -Path $nestedRoot -Force | ForEach-Object {
            Move-Item -Path $_.FullName -Destination $tempMove -Force
        }
        Remove-Item $Target -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path $Target -Force | Out-Null
        Get-ChildItem -Path $tempMove -Force | ForEach-Object {
            Move-Item -Path $_.FullName -Destination $Target -Force
        }
        Remove-Item $tempMove -Recurse -Force -ErrorAction SilentlyContinue
    }

    return Get-LibreOfficeSofficePath
}

function Wait-LibreOfficeReady {
    param([int]$Seconds = 90)

    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        $ready = Normalize-LibreOfficeFolder
        if ($ready) { return $ready }
        Start-Sleep -Milliseconds 750
    } while ((Get-Date) -lt $deadline)

    return $null
}

function Get-ToolCommand {
    param([string[]]$Candidates)
    foreach ($candidate in $Candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        if (Test-Path $candidate) { return $candidate }
        $cmd = Get-Command $candidate -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }
    return $null
}

function Test-IsPortableAppsInstallerBinary {
    param([Parameter(Mandatory=$true)][string]$Path)

    try {
        if (-not (Test-Path $Path)) { return $false }
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            if ($stream.Length -lt 2) { return $false }
            $first = $stream.ReadByte()
            $second = $stream.ReadByte()
            return ($first -eq 0x4D -and $second -eq 0x5A) # MZ executable header
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return $false
    }
}

function Get-LibreOfficePackageCandidates {
    $candidateFolders = @(
        (Join-Path $Root "src\MusicShell.Wpf\Tools\Packages"),
        $Root,
        (Join-Path $Root "Packages"),
        (Join-Path ([Environment]::GetFolderPath('UserProfile')) "Downloads")
    ) | Select-Object -Unique

    $candidates = New-Object System.Collections.Generic.List[string]
    foreach ($folder in $candidateFolders) {
        foreach ($name in $PackageNames) {
            $candidates.Add((Join-Path $folder $name))
        }

        if (Test-Path $folder) {
            Get-ChildItem -Path $folder -File -Filter "LibreOfficePortable_*_MultilingualStandard.paf.*" -ErrorAction SilentlyContinue |
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

    if (Test-Path $destination) {
        return $destination
    }

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
            Write-Host "Bu dosya yaklasik 214 MB olabilir; internet hizina gore surebilir." -ForegroundColor DarkGray
            Invoke-WebRequest -Uri $url -OutFile $destination -UseBasicParsing
            if ((Test-Path $destination) -and ((Get-Item $destination).Length -gt 100MB)) {
                Write-Host "LibreOffice paketi indirildi: $destination" -ForegroundColor Green
                return $destination
            }

            Remove-Item $destination -Force -ErrorAction SilentlyContinue
        }
        catch {
            Write-Host "Indirme basarisiz: $($_.Exception.Message)" -ForegroundColor Yellow
            Remove-Item $destination -Force -ErrorAction SilentlyContinue
        }
    }

    return $null
}

function Get-SevenZipToolForArchive {
    $programFiles = [Environment]::GetEnvironmentVariable('ProgramFiles')
    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    $localAppData = [Environment]::GetEnvironmentVariable('LOCALAPPDATA')

    return Get-ToolCommand -Candidates @(
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
    )
}

function Resolve-PortableAppsInstaller {
    param(
        [Parameter(Mandatory=$true)][string]$PackagePath,
        [Parameter(Mandatory=$true)][string]$TempRoot
    )

    if (Test-IsPortableAppsInstallerBinary -Path $PackagePath) {
        $installerCopy = Join-Path $TempRoot ("LibreOfficePortable_" + [Guid]::NewGuid().ToString('N') + ".paf.exe")
        Copy-Item -Path $PackagePath -Destination $installerCopy -Force
        return Get-Item $installerCopy
    }

    Write-Host "PAF zip paketi aciliyor..." -ForegroundColor Cyan
    $sevenZip = Get-SevenZipToolForArchive
    if ($sevenZip) {
        Write-Host "7-Zip/NanaZip ile PAF zip paketi aciliyor: $sevenZip" -ForegroundColor Cyan
        & $sevenZip x $PackagePath "-o$TempRoot" -y | Out-Host
    }
    else {
        Expand-Archive -Path $PackagePath -DestinationPath $TempRoot -Force
    }

    return Get-ChildItem -Path $TempRoot -Filter "*.paf.exe" -File -Recurse | Select-Object -First 1
}

function Copy-ExtractedLibreOffice {
    param([Parameter(Mandatory=$true)][string]$ExtractRoot)

    $extractedSoffice = Get-ChildItem -Path $ExtractRoot -Filter "soffice.exe" -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "(?i)\\App\\(libreoffice|LibreOffice)\\program\\soffice\.exe$" } |
        Select-Object -First 1

    if (-not $extractedSoffice) { return $false }

    # soffice.exe yolu: <Root>\App\libreoffice\program\soffice.exe
    $portableRoot = $extractedSoffice.Directory.Parent.Parent.Parent.FullName
    Write-Host "Cikarilan LibreOffice klasoru tasiniyor: $portableRoot" -ForegroundColor Cyan

    if (Test-Path $Target) { Remove-Item $Target -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Path $Target -Force | Out-Null
    Get-ChildItem -Path $portableRoot -Force | ForEach-Object {
        Move-Item -Path $_.FullName -Destination $Target -Force
    }

    return [bool](Normalize-LibreOfficeFolder)
}

function Invoke-InstallerWithTimeout {
    param(
        [Parameter(Mandatory=$true)][string]$InstallerPath,
        [int]$TimeoutSeconds = 420
    )

    Write-Host "LibreOffice Portable sessiz kuruluyor... Bu adim bilgisayar hizina gore birkac dakika surebilir." -ForegroundColor Cyan

    # PortableApps Installer icin en uyumlu argumanlar.
    $installerArgs = @('/SILENT', "/DESTINATION=$ToolsRoot")
    $process = Start-Process -FilePath $InstallerPath -ArgumentList $installerArgs -PassThru

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while (-not $process.HasExited) {
        $ready = Normalize-LibreOfficeFolder
        if ($ready) {
            Write-Host "LibreOffice dosyalari olustu, kurucunun kapanmasi bekleniyor..." -ForegroundColor DarkGray
            if ($process.WaitForExit(30000)) { break }
        }

        if ((Get-Date) -gt $deadline) {
            Write-Host "LibreOffice kurucusu zaman asimina girdi, islem sonlandiriliyor." -ForegroundColor Yellow
            try { & taskkill.exe /PID $process.Id /T /F | Out-Null } catch { try { $process.Kill() } catch { } }
            return $false
        }

        Start-Sleep -Seconds 2
    }

    $readyPath = Wait-LibreOfficeReady -Seconds 45
    if ($readyPath) { return $true }

    if ($process.ExitCode -ne 0) {
        Write-Host "PAF kurulum cikis kodu: $($process.ExitCode)." -ForegroundColor Yellow
    }
    return $false
}

$readyPath = Normalize-LibreOfficeFolder
if ($readyPath) {
    Write-Host "LibreOffice Portable zaten hazir: $readyPath" -ForegroundColor Green
    exit 0
}

$PackageCandidates = Get-LibreOfficePackageCandidates
$Package = $PackageCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $Package) {
    if ($DownloadIfMissing) {
        $packageFolder = Join-Path $Root "src\MusicShell.Wpf\Tools\Packages"
        $downloadedPackage = Invoke-LibreOfficePackageDownload -DestinationFolder $packageFolder
        if ($downloadedPackage -and (Test-Path $downloadedPackage)) {
            $Package = $downloadedPackage
        }
    }
}

if (-not $Package) {
    Write-Host "LibreOffice Portable paketi bulunamadi." -ForegroundColor Yellow
    Write-Host "Paketi asagidaki yollardan birine koyup scripti tekrar calistir:" -ForegroundColor Yellow
    foreach ($candidate in $PackageCandidates) { Write-Host "  $candidate" -ForegroundColor DarkGray }
    Write-Host "Internet varsa otomatik indirme icin:" -ForegroundColor Yellow
    Write-Host "  .\setup_libreoffice_portable.ps1 -DownloadIfMissing -UseInstallerFallback" -ForegroundColor DarkGray
    Write-Host "LibreOffice opsiyoneldir; uygulama yine calisir ancak DOCX/XLSX/PPTX donusumleri sinirli kalabilir." -ForegroundColor Yellow
    exit 0
}

Write-Host "Paket bulundu: $Package" -ForegroundColor Green
New-Item -ItemType Directory -Path $ToolsRoot -Force | Out-Null

$Temp = Join-Path ([System.IO.Path]::GetTempPath()) ("ToolBridgeLibreOffice_" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Temp -Force | Out-Null

try {
    $Installer = Resolve-PortableAppsInstaller -PackagePath $Package -TempRoot $Temp
    if (-not $Installer) {
        throw "Paket icinde veya belirtilen pakette .paf.exe bulunamadi."
    }
    Write-Host "PAF kurucu hazir: $($Installer.FullName)" -ForegroundColor Green

    # Oncelik: 7-Zip/NanaZip ile dogrudan acmak. Bu yontem GUI kurucu beklemesi/hang riskini ortadan kaldirir.
    $programFiles = [Environment]::GetEnvironmentVariable('ProgramFiles')
    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    $localAppData = [Environment]::GetEnvironmentVariable('LOCALAPPDATA')

    $sevenZip = Get-ToolCommand -Candidates @(
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
    )

    if ($sevenZip) {
        Write-Host "7-Zip/NanaZip ile PAF icerigi cikariliyor: $sevenZip" -ForegroundColor Cyan
        $extractTemp = Join-Path $Temp "paf_extract"
        New-Item -ItemType Directory -Path $extractTemp -Force | Out-Null
        & $sevenZip x $Installer.FullName "-o$extractTemp" -y | Out-Host
        if (-not (Copy-ExtractedLibreOffice -ExtractRoot $extractTemp)) {
            Write-Host "7-Zip/NanaZip cikarimi sonucu soffice.exe bulunamadi. Kurucu deneniyor..." -ForegroundColor Yellow
        }
    }

    $readyPath = Normalize-LibreOfficeFolder
    if (-not $readyPath) {
        if ($UseInstallerFallback) {
            $ok = Invoke-InstallerWithTimeout -InstallerPath $Installer.FullName -TimeoutSeconds 420
            if (-not $ok) {
                Write-Host "LibreOffice Portable otomatik hazirlanamadi; uygulama yine calisir, LibreOffice motoru Eksik gorunur." -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "7-Zip/NanaZip bulunamadi veya PAF icerigi dogrudan cikarilamadi." -ForegroundColor Yellow
            Write-Host "GUI/sessiz kurucu bazi sistemlerde takildigi icin varsayilan olarak calistirilmadi." -ForegroundColor Yellow
            Write-Host "Cozum: 7-Zip veya NanaZip kurup bu scripti tekrar calistir." -ForegroundColor Yellow
            Write-Host "Kurucuyu yine de denemek istersen: .\setup_libreoffice_portable.ps1 -UseInstallerFallback" -ForegroundColor DarkGray
        }
    }

    $readyPath = Normalize-LibreOfficeFolder
    if (-not $readyPath) {
        Write-Host "Manuel cozum:" -ForegroundColor Yellow
        Write-Host "1) 7-Zip veya NanaZip kur." -ForegroundColor Yellow
        Write-Host "2) LibreOfficePortable .paf.exe dosyasini veya .paf.exe iceren zip paketini 7-Zip/NanaZip ile ac." -ForegroundColor Yellow
        Write-Host "3) LibreOfficePortable klasorunu su yola yerlestir:" -ForegroundColor Yellow
        Write-Host "   $Target" -ForegroundColor Yellow
        Write-Host "Beklenen dosya:" -ForegroundColor Yellow
        Write-Host "   $ExpectedSoffice" -ForegroundColor Yellow
        exit 0
    }

    Write-Host "LibreOffice Portable hazir: $readyPath" -ForegroundColor Green
}
finally {
    try { Remove-Item $Temp -Recurse -Force -ErrorAction SilentlyContinue } catch { }
}
