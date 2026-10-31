param(
    [string]$SourceDirectory,
    [ValidateSet('Release', 'RelWithDebInfo', 'Debug')][string]$Configuration = 'Release',
    [switch]$ManagedOnly,
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
if ([String]::IsNullOrWhiteSpace($SourceDirectory)) {
    # This script is also staged as <fork>/wiiu-achievements-managed/build-cemu.ps1.
    if ((Split-Path -Leaf $PSScriptRoot) -eq 'wiiu-achievements-managed') { $SourceDirectory = Split-Path -Parent $PSScriptRoot }
    else { $SourceDirectory = Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))) 'build-check\cemu-fork' }
}
$source = [IO.Path]::GetFullPath($SourceDirectory)
$managed = Join-Path $source 'wiiu-achievements-managed'
if (-not (Test-Path -LiteralPath (Join-Path $managed 'build.ps1'))) { throw 'Prima eseguire integrations/cemu/prepare.ps1.' }
$bin = Join-Path $source 'bin'
& (Join-Path $managed 'build.ps1') -OutputDirectory (Join-Path $bin 'WiiUAchievements')
if (-not $SkipTests) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $managed 'tests\run.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Regressioni del motore fallite.' }
    $nativeTests = Join-Path $managed 'tests\run-native.ps1'
    if (Test-Path -LiteralPath $nativeTests) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $nativeTests
        if ($LASTEXITCODE -ne 0) { throw 'Regressioni del collegamento nativo fallite.' }
    }
}
if ($ManagedOnly) { Write-Output ('Modulo gestito compilato: ' + (Join-Path $bin 'WiiUAchievements\WiiUAchievements.exe')); return }

$cmake = Get-Command cmake -ErrorAction SilentlyContinue
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsDirectory = $null
if (Test-Path -LiteralPath $vswhere) {
    $vsDirectory = (& $vswhere -latest -version '[17.0,18.0)' -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Out-String).Trim()
    if (-not $cmake -and $vsDirectory) {
        $bundledCmake = Join-Path $vsDirectory 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
        if (Test-Path -LiteralPath $bundledCmake) { $cmake = Get-Item -LiteralPath $bundledCmake }
    }
}
if (-not $cmake -or [String]::IsNullOrWhiteSpace($vsDirectory)) {
    throw 'Modulo C# compilato. Per compilare Cemu servono Visual Studio 2022 / Build Tools, strumenti C++ x64, Windows 10/11 SDK e CMake >= 3.21.1. Nessun programma e stato installato e il Cemu del Desktop e invariato.'
}
$netFxSdk = @('4.8.1', '4.8') | Where-Object {
    Test-Path -LiteralPath (Join-Path ${env:ProgramFiles(x86)} ('Windows Kits\NETFXSDK\' + $_ + '\Include\um\metahost.h'))
}
if ($netFxSdk.Count -eq 0) {
    throw 'Manca .NET Framework 4.8 / 4.8.1 SDK (metahost.h e mscoree.lib): aggiungere il componente SDK di Visual Studio per il collegamento CLR nativo.'
}
foreach ($dependency in @('vcpkg\scripts\buildsystems\vcpkg.cmake', 'imgui\imgui.h', 'Vulkan-Headers\include\vulkan\vulkan.h', 'cubeb\CMakeLists.txt', 'ZArchive\CMakeLists.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source ('dependencies\' + $dependency)))) { throw 'Dipendenze sorgente Cemu assenti: eseguire git submodule update --init --recursive nel fork.' }
}
$cmakePath = if ($cmake -is [IO.FileInfo]) { $cmake.FullName } else { $cmake.Source }
$build = Join-Path $source 'build-wiiu-achievements'
& $cmakePath -S $source -B $build -G 'Visual Studio 17 2022' -A x64 '-DEMULATOR_VERSION_MAJOR=2' '-DEMULATOR_VERSION_MINOR=6' '-DEMULATOR_VERSION_PATCH=0'
if ($LASTEXITCODE -ne 0) { throw 'Configurazione CMake del fork fallita.' }
& $cmakePath --build $build --config $Configuration --parallel
if ($LASTEXITCODE -ne 0) { throw 'Compilazione Cemu fallita.' }
$builtExe = Join-Path $bin ('Cemu_' + $Configuration.ToLowerInvariant() + '.exe')
if (-not (Test-Path -LiteralPath $builtExe)) { throw ('Eseguibile Cemu compilato non trovato: ' + $builtExe) }
Copy-Item -LiteralPath $builtExe -Destination (Join-Path $bin 'Cemu.exe') -Force
Write-Output ('Fork compilato: ' + (Join-Path $bin 'Cemu.exe'))
Write-Output 'Il Cemu del Desktop non e stato sostituito. Non copiare mlc01, chiavi o salvataggi nel sorgente.'
