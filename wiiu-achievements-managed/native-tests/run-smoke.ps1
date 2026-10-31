param([string]$SourceDirectory = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Questa prova CLR completa usa progressi sintetici e richiede un account CI pulito. Non eseguirla contro i dati personali del PC.' }
$source = [IO.Path]::GetFullPath($SourceDirectory)
$managed = Join-Path $source 'wiiu-achievements-managed'
$archive = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WiiUAchievements'
if (Test-Path -LiteralPath $archive) { throw 'Archivio personale gia presente: smoke test interrotto prima di avviare il modulo.' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Visual Studio Installer / vswhere assente.' }
$vsDirectory = (& $vswhere -latest -version '[17.0,18.0)' -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Out-String).Trim()
if (-not $vsDirectory) { throw 'Compilatore MSVC x64 non trovato.' }
Import-Module (Join-Path $vsDirectory 'Common7\Tools\Microsoft.VisualStudio.DevShell.dll')
Enter-VsDevShell -VsInstallPath $vsDirectory -SkipAutomaticLocation -DevCmdArguments '-arch=x64 -host_arch=x64'
$sdk = @('4.8.1', '4.8') | ForEach-Object { Join-Path ${env:ProgramFiles(x86)} ('Windows Kits\NETFXSDK\' + $_) } | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'Include\um\metahost.h') } | Select-Object -First 1
if (-not $sdk) { throw '.NET Framework SDK metahost.h assente.' }
$output = Join-Path $source 'native-smoke'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& (Join-Path $managed 'build.ps1') -OutputDirectory (Join-Path $output 'WiiUAchievements')
$hostSource = Join-Path $source 'src\WiiUAchievements\WiiUAchievementsHost.cpp'
$hostIncludes = Join-Path $source 'src\WiiUAchievements'
$fixture = Join-Path $managed 'native-tests'
$exe = Join-Path $output 'NativeHostSmoke.exe'
& cl /nologo /std:c++20 /EHsc /W4 /DUNICODE /D_UNICODE /DNOMINMAX "/I$hostIncludes" "/I$fixture\stubs" "/I$sdk\Include\um" $hostSource (Join-Path $fixture 'NativeHostSmoke.cpp') "/Fo$output\\" "/Fe$exe" /link "/LIBPATH:$sdk\Lib\um\x64" mscoree.lib user32.lib
if ($LASTEXITCODE -ne 0) { throw 'Compilazione harness CLR nativo fallita.' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Smoke test CLR nativo fallito.' }
