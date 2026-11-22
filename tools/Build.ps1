param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [ValidateSet('x64','ARM64')][string]$Architecture = 'x64',
    [switch]$Test,
    [switch]$Package
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Build-Native.ps1') -Configuration $Configuration -Architecture $Architecture -Test:$Test
dotnet build (Join-Path $repo 'src/Widget/DeviceBatteryControl.Widget.csproj') -c $Configuration -p:Platform=$Architecture -p:RuntimeIdentifier=win-$($Architecture.ToLowerInvariant()) -p:SkipNativeBuild=true --nologo
if ($LASTEXITCODE) { throw 'Widget build failed.' }
if ($Test) {
    dotnet test (Join-Path $repo 'tests/Widget.Tests/Widget.Tests.csproj') -c $Configuration --nologo
    if ($LASTEXITCODE) { throw 'Widget tests failed.' }
}
if ($Package) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $visualStudio = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
    & (Join-Path $visualStudio 'MSBuild/Current/Bin/MSBuild.exe') (Join-Path $repo 'packaging/DeviceBatteryControl.Package.wapproj') /restore /t:Build /p:Configuration=$Configuration /p:Platform=$Architecture /p:SkipNativeBuild=true /v:minimal /nologo
    if ($LASTEXITCODE) { throw 'MSIX build failed.' }
}
