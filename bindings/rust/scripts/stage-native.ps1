<#
.SYNOPSIS
Publishes deskband11lib.dll with NativeAOT and stages it in the deskband11lib crate.

.PARAMETER RuntimeIdentifiers
win-x64, win-arm64, or both. Defaults to the architecture of this machine.
#>
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string[]] $RuntimeIdentifiers
)

$ErrorActionPreference = 'Stop'
if (-not $RuntimeIdentifiers) {
    $RuntimeIdentifiers = @(if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'win-arm64' } else { 'win-x64' })
}

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$projectPath = Join-Path $repositoryRoot 'Deskband11Lib.Native\Deskband11Lib.Native.csproj'
$crateDirectory = Join-Path $PSScriptRoot '..\deskband11lib'

# The NativeAOT linker lookup runs vswhere.exe, which is only on PATH in Visual Studio developer shells.
$installerDirectory = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (Test-Path $installerDirectory) {
    $env:Path = "$env:Path;$installerDirectory"
}

foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    $publishDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "deskband11lib-$([System.Guid]::NewGuid())"
    try {
        dotnet publish $projectPath -c Release -r $runtimeIdentifier -o $publishDirectory -nologo
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed for $runtimeIdentifier."
        }

        $targetDirectory = Join-Path $crateDirectory "bin\$runtimeIdentifier"
        New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
        Copy-Item (Join-Path $publishDirectory 'deskband11lib.dll') $targetDirectory -Force
        Write-Host "Staged ${runtimeIdentifier}: $(Join-Path $targetDirectory 'deskband11lib.dll')"
    }
    finally {
        Remove-Item $publishDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
