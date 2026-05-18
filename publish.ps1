param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64','win-x86','win-arm64')]
    [string]$Runtime = 'win-x64',

    [string]$Output = '.\publish',

    [switch]$FrameworkDependent,

    # Geriye uyumluluk icin duruyor. Final pakette LibreOffice varsayilan olarak hazirlanir.
    [switch]$PrepareLibreOffice,

    [switch]$UseLibreOfficeInstallerFallback,

    # LibreOffice paketi yoksa resmi TDF aynalarindan indirmeyi dener.
    [switch]$DownloadLibreOfficeIfMissing,

    # Kucuk paket isteyenler icin. Bu modda LibreOffice zorunlu tutulmaz.
    [switch]$Lite,

    [switch]$SkipExternalTools,

    # Final paket icin motorlarin publish cikisinda oldugunu dogrular.
    [switch]$NoStrictToolCheck
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory=$true)][string]$Title,
        [Parameter(Mandatory=$true)][scriptblock]$Command
    )

    Write-Host $Title -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Title failed. Exit code: $LASTEXITCODE"
    }
}

function Get-ProjectAssemblyName {
    param([Parameter(Mandatory=$true)][string]$ProjectPath)

    try {
        [xml]$projectXml = Get-Content $ProjectPath -Raw
        $propertyGroups = @($projectXml.Project.PropertyGroup)
        foreach ($group in $propertyGroups) {
            if ($group.AssemblyName -and -not [string]::IsNullOrWhiteSpace($group.AssemblyName)) {
                return [string]$group.AssemblyName
            }
        }
    }
    catch {
        Write-Host "AssemblyName could not be read from csproj. Project file name will be used." -ForegroundColor Yellow
    }

    return [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
}

function Find-7ZipExecutable {
    $programFiles = [Environment]::GetEnvironmentVariable('ProgramFiles')
    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    $localAppData = [Environment]::GetEnvironmentVariable('LOCALAPPDATA')

    $candidates = @(
        (Join-Path $root 'build_tools\7zip\7z.exe'),
        (Join-Path $root 'src\MusicShell.Wpf\Tools\7zip\7z.exe'),
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
        $command = Get-Command $candidate -ErrorAction SilentlyContinue
        if ($command) { return $command.Source }
    }

    return $null
}

function Find-FirstExistingPath {
    param([string[]]$Paths)
    foreach ($path in $Paths) {
        if (-not [string]::IsNullOrWhiteSpace($path) -and (Test-Path $path)) {
            return $path
        }
    }
    return $null
}

function Get-SourceLibreOfficePath {
    $toolsRoot = Join-Path $root 'src\MusicShell.Wpf\Tools'
    $paths = @(
        (Join-Path $toolsRoot 'LibreOfficePortable\App\libreoffice\program\soffice.exe'),
        (Join-Path $toolsRoot 'LibreOfficePortable\App\LibreOffice\program\soffice.exe'),
        (Join-Path $toolsRoot 'LibreOfficePortable\LibreOfficePortable\App\libreoffice\program\soffice.exe'),
        (Join-Path $toolsRoot 'LibreOfficePortable\LibreOfficePortable\App\LibreOffice\program\soffice.exe')
    )
    return Find-FirstExistingPath -Paths $paths
}

function Get-SourceImageMagickPath {
    return Find-FirstExistingPath -Paths @(
        (Join-Path $root 'src\MusicShell.Wpf\Tools\ImageMagick\magick.exe')
    )
}

function Get-PublishLibreOfficePath {
    param([string]$PublishOutput)
    return Find-FirstExistingPath -Paths @(
        (Join-Path $PublishOutput 'Tools\LibreOfficePortable\App\libreoffice\program\soffice.exe'),
        (Join-Path $PublishOutput 'Tools\LibreOfficePortable\App\LibreOffice\program\soffice.exe'),
        (Join-Path $PublishOutput 'Tools\LibreOfficePortable\LibreOfficePortable\App\libreoffice\program\soffice.exe'),
        (Join-Path $PublishOutput 'Tools\LibreOfficePortable\LibreOfficePortable\App\LibreOffice\program\soffice.exe')
    )
}

function Get-PublishImageMagickPath {
    param([string]$PublishOutput)
    return Find-FirstExistingPath -Paths @(
        (Join-Path $PublishOutput 'Tools\ImageMagick\magick.exe')
    )
}

Write-Host "ToolBridge publish started..." -ForegroundColor Cyan
Write-Host "Root: $root" -ForegroundColor DarkGray

$project = Join-Path $root 'src\MusicShell.Wpf\MusicShell.Wpf.csproj'
if (-not (Test-Path $project)) {
    $projectFile = Get-ChildItem -Path $root -Recurse -Filter 'MusicShell.Wpf.csproj' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $projectFile) {
        throw "Project file not found. Extract the source ZIP directly into C:\ToolBridge or run this script from the extracted source root."
    }
    $project = $projectFile.FullName
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK was not found. Install .NET 8 SDK and run publish again."
}

$assemblyName = Get-ProjectAssemblyName -ProjectPath $project
$expectedExeName = "$assemblyName.exe"
$publishOutput = Join-Path $root $Output
$selfContained = -not $FrameworkDependent.IsPresent
$includeLibreOffice = -not $Lite.IsPresent
$strictToolCheck = -not $NoStrictToolCheck.IsPresent

Write-Host "Project: $project" -ForegroundColor Green
Write-Host "Output executable: $expectedExeName" -ForegroundColor Green
Write-Host "Package mode: $(if ($includeLibreOffice) { 'FULL - LibreOffice required' } else { 'LITE - LibreOffice optional' })" -ForegroundColor Green

$externalToolsSetup = Join-Path $root 'scripts\setup_external_tools.ps1'
if ((-not $SkipExternalTools) -and (Test-Path $externalToolsSetup)) {
    Write-Host "Portable tools are being prepared..." -ForegroundColor Cyan
    $externalArgs = @()
    if ($includeLibreOffice -or $PrepareLibreOffice) {
        $externalArgs += '-IncludeLibreOffice'
    }
    if ($UseLibreOfficeInstallerFallback -or $includeLibreOffice) {
        $externalArgs += '-UseLibreOfficeInstallerFallback'
    }
    if ($DownloadLibreOfficeIfMissing) {
        $externalArgs += '-DownloadLibreOfficeIfMissing'
    }
    & $externalToolsSetup @externalArgs
}
elseif ($SkipExternalTools) {
    Write-Host "Portable tools setup skipped." -ForegroundColor DarkYellow
}

$sourceMagick = Get-SourceImageMagickPath
if (-not $sourceMagick) {
    Write-Host "ImageMagick source engine is missing. Image conversions may be limited." -ForegroundColor Yellow
}
else {
    Write-Host "ImageMagick source engine ready: $sourceMagick" -ForegroundColor Green
}

$sourceLibreOffice = Get-SourceLibreOfficePath
if ($includeLibreOffice -and -not $sourceLibreOffice) {
    throw @"
LibreOffice Portable is required for FULL publish but soffice.exe was not found.
Expected source path:
  $root\src\MusicShell.Wpf\Tools\LibreOfficePortable\App\libreoffice\program\soffice.exe

Fix options:
  1) Put LibreOfficePortable_26.2.1_MultilingualStandard.paf.exe into src\MusicShell.Wpf\Tools\Packages and run publish again.
  2) Run publish with -DownloadLibreOfficeIfMissing if internet access is available.
  3) Use -Lite if you intentionally want a package without Office conversions.
"@
}
elseif ($sourceLibreOffice) {
    Write-Host "LibreOffice source engine ready: $sourceLibreOffice" -ForegroundColor Green
}

if (Test-Path $publishOutput) {
    Write-Host "Cleaning old publish folder..." -ForegroundColor Yellow
    try {
        Remove-Item $publishOutput -Recurse -Force
    }
    catch {
        Write-Host "Publish folder could not be cleaned. Close ToolBridge.exe and Explorer windows using the publish folder." -ForegroundColor Yellow
        throw
    }
}

New-Item -ItemType Directory -Path $publishOutput | Out-Null

Invoke-CheckedCommand 'dotnet restore...' { dotnet restore $project }

Invoke-CheckedCommand 'dotnet publish...' {
    dotnet publish $project `
        -c $Configuration `
        -r $Runtime `
        --self-contained $selfContained `
        -p:PublishSingleFile=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $publishOutput
}

$firewallScript = Join-Path $root 'scripts\setup_firewall_toolbridge_presence.ps1'
if (Test-Path $firewallScript) {
    Copy-Item $firewallScript -Destination (Join-Path $publishOutput 'setup_firewall_toolbridge_presence.ps1') -Force
}

$expectedExePath = Join-Path $publishOutput $expectedExeName
if (Test-Path $expectedExePath) {
    $exePath = $expectedExePath
}
else {
    $exe = Get-ChildItem -Path $publishOutput -Filter '*.exe' -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $exe) {
        throw "Publish failed. No EXE file was created in: $publishOutput"
    }
    $exePath = $exe.FullName
}

if ($strictToolCheck) {
    $publishMagick = Get-PublishImageMagickPath -PublishOutput $publishOutput
    if (-not $publishMagick) {
        Write-Host "WARNING: ImageMagick was not found in publish output." -ForegroundColor Yellow
    }
    else {
        Write-Host "ImageMagick publish check OK: $publishMagick" -ForegroundColor Green
    }

    $publishLibreOffice = Get-PublishLibreOfficePath -PublishOutput $publishOutput
    if ($includeLibreOffice -and -not $publishLibreOffice) {
        throw "LibreOffice was prepared in source but was not copied to publish output. Check csproj CopyToPublishDirectory settings."
    }
    elseif ($publishLibreOffice) {
        Write-Host "LibreOffice publish check OK: $publishLibreOffice" -ForegroundColor Green
    }
}

$zipSuffix = if ($Lite) { 'Lite' } else { 'Full' }
$zipPath = Join-Path $root "ToolBridge_${zipSuffix}_publish_$Runtime.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

$sevenZip = Find-7ZipExecutable
if ($sevenZip) {
    Write-Host "Creating ZIP with 7-Zip: $sevenZip" -ForegroundColor Cyan
    & $sevenZip a -tzip $zipPath (Join-Path $publishOutput '*') -mx=5
    if ($LASTEXITCODE -ne 0) {
        throw "7-Zip archive creation failed. Exit code: $LASTEXITCODE"
    }
}
else {
    Write-Host "Creating ZIP with Compress-Archive..." -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $publishOutput '*') -DestinationPath $zipPath -Force
}

Write-Host "" 
Write-Host "Publish completed successfully." -ForegroundColor Green
Write-Host "Publish folder: $publishOutput" -ForegroundColor Green
Write-Host "ZIP file: $zipPath" -ForegroundColor Green
Write-Host "Run this file: $exePath" -ForegroundColor Cyan