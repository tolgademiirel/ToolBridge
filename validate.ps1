param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [switch]$PrepareLibreOffice,

    [switch]$UseLibreOfficeInstallerFallback,

    [switch]$SkipExternalTools
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

Write-Host 'ToolBridge dogrulama baslatildi...' -ForegroundColor Cyan

$externalToolsSetup = Join-Path $root 'setup_external_tools.ps1'
if ((-not $SkipExternalTools) -and (Test-Path $externalToolsSetup)) {
    Write-Host 'Opsiyonel portable araclar kontrol ediliyor...' -ForegroundColor Cyan
    if ($PrepareLibreOffice) {
        if ($UseLibreOfficeInstallerFallback) {
            & $externalToolsSetup -IncludeLibreOffice -UseLibreOfficeInstallerFallback
        }
        else {
            & $externalToolsSetup -IncludeLibreOffice
        }
    }
    else {
        & $externalToolsSetup
    }
}
elseif ($SkipExternalTools) {
    Write-Host 'Opsiyonel portable arac kurulumu atlandi.' -ForegroundColor DarkYellow
}

$project = Join-Path $root 'src\MusicShell.Wpf\MusicShell.Wpf.csproj'
if (-not (Test-Path $project)) {
    $projectFile = Get-ChildItem -Path $root -Recurse -Filter 'MusicShell.Wpf.csproj' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $projectFile) { throw 'MusicShell.Wpf.csproj bulunamadi. ZIP dosyasini kaynak kokune cikarin.' }
    $project = $projectFile.FullName
}

$projectDir = Split-Path -Parent $project
$requiredFiles = @(
    (Join-Path $projectDir 'MainWindow.xaml'),
    (Join-Path $projectDir 'App.xaml'),
    (Join-Path $projectDir 'Styles\ToolBridge.UI.xaml'),
    (Join-Path $projectDir 'Tools\SumatraPDF.exe'),
    (Join-Path $projectDir 'Tools\Docnet.Core.dll'),
    (Join-Path $projectDir 'Tools\pdfium.dll')
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        throw "Eksik zorunlu dosya: $file"
    }
}

$optionalTools = @(
    @{ Name = 'ImageMagick'; Path = (Join-Path $projectDir 'Tools\ImageMagick\magick.exe') },
    @{ Name = 'LibreOffice Portable'; Path = (Join-Path $projectDir 'Tools\LibreOfficePortable\App\libreoffice\program\soffice.exe') }
)

foreach ($tool in $optionalTools) {
    if (Test-Path $tool.Path) {
        Write-Host "$($tool.Name) hazir: $($tool.Path)" -ForegroundColor Green
    }
    else {
        Write-Host "$($tool.Name) opsiyonel motoru hazir degil: $($tool.Path)" -ForegroundColor Yellow
    }
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory=$true)][string]$Title,
        [Parameter(Mandatory=$true)][scriptblock]$Command
    )

    Write-Host $Title -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Title basarisiz oldu. Cikis kodu: $LASTEXITCODE"
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK bulunamadi. .NET 8 SDK kurulduktan sonra validate.ps1 yeniden calistirilmali.'
}

Invoke-CheckedCommand 'dotnet restore calisiyor...' { dotnet restore $project }
Invoke-CheckedCommand 'dotnet build calisiyor...' { dotnet build $project -c $Configuration --no-restore }

Write-Host 'Dogrulama basarili.' -ForegroundColor Green

# Optional external tools are intentionally not mandatory for source validation.
# They are installed or packaged by setup_external_tools.ps1 / publish.ps1 when needed.
$optionalExternalTools = @(
    (Join-Path $projectDir 'Tools\SumatraPDF.exe'),
    (Join-Path $projectDir 'Tools\Docnet.Core.dll'),
    (Join-Path $projectDir 'Tools\pdfium.dll')
)
foreach ($optionalTool in $optionalExternalTools) {
    if (-not (Test-Path $optionalTool)) {
        Write-Host "[optional] Missing external tool: $optionalTool"
    }
}