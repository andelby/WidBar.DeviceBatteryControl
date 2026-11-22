param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [ValidateSet('x64','ARM64')][string]$Architecture = 'x64',
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$visualStudio = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $visualStudio) { throw 'Install the Visual Studio C++ desktop tools.' }
$cmake = Join-Path $visualStudio 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
$generator = if ((Split-Path $visualStudio -Parent) -match '\\18$') { 'Visual Studio 18 2026' } else { 'Visual Studio 17 2022' }
$build = Join-Path $repo $(if ($Architecture -eq 'ARM64') { 'artifacts/native-arm64' } else { 'artifacts/native' })
& $cmake -S (Join-Path $repo 'native') -B $build -G $generator -A $Architecture
if ($LASTEXITCODE) { throw 'Native configuration failed.' }
& $cmake --build $build --config $Configuration --target BatteryProtocols --parallel 2
if ($LASTEXITCODE) { throw 'Native build failed.' }

$dependencies = Join-Path $repo 'artifacts/dependencies'
New-Item -ItemType Directory -Path $dependencies -Force | Out-Null
function Fetch([string]$Url, [string]$Name, [string]$Hash) {
    $path = Join-Path $dependencies $Name
    if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -ne $Hash) {
        Invoke-WebRequest -Uri $Url -OutFile $path
    }
    if ((Get-FileHash -LiteralPath $path).Hash -ne $Hash) { throw "Checksum mismatch: $Name" }
}
$release = 'https://github.com/Sapd/HeadsetControl/releases/download/4.3.0'
Fetch "$release/headsetcontrol-windows-x86_64.exe" 'headsetcontrol.exe' 'e371da043710ddb8fc70af77f2ccad1cd03f8c6f49ba423e08764ad7a94a3c40'
Fetch "$release/headsetcontrol-4.3.0.tar.gz" 'HeadsetControlSource.tgz' '918dcacf8aef347744b36b2d259d76880fa64b3869f0f8ef26b6576b873d28a7'
Copy-Item -LiteralPath (Join-Path $dependencies 'headsetcontrol.exe') -Destination (Join-Path $build "$Configuration/headsetcontrol.exe") -Force
Compress-Archive -LiteralPath (Join-Path $repo 'native'),(Join-Path $repo 'LICENSE') -DestinationPath (Join-Path $dependencies 'ProtocolSources.zip') -Force
if ($Test) {
    & $cmake --build $build --config $Configuration --parallel 2
    if ($LASTEXITCODE) { throw 'Native test build failed.' }
    & (Join-Path (Split-Path $cmake) 'ctest.exe') --test-dir $build -C $Configuration --output-on-failure
    if ($LASTEXITCODE) { throw 'Native tests failed.' }
}
