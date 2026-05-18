param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $root "MusicShell.sln"
$projectPath = Join-Path $root "src\MusicShell.Wpf\MusicShell.Wpf.csproj"

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Title,

        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    Write-Host "[build] $Title" -ForegroundColor Cyan
    & $FilePath @ArgumentList

    if ($LASTEXITCODE -ne 0) {
        throw "$Title failed. Exit code: $LASTEXITCODE"
    }
}

if (Test-Path $solutionPath) {
    $targetPath = $solutionPath
} elseif (Test-Path $projectPath) {
    $targetPath = $projectPath
} else {
    throw "Build target not found. Expected MusicShell.sln or src\MusicShell.Wpf\MusicShell.Wpf.csproj under: $root"
}

Invoke-CheckedCommand -Title "dotnet restore" -FilePath "dotnet" -ArgumentList @("restore", $targetPath)
Invoke-CheckedCommand -Title "dotnet build" -FilePath "dotnet" -ArgumentList @("build", $targetPath, "-c", $Configuration, "--no-restore")

Write-Host "Build completed: $Configuration" -ForegroundColor Green