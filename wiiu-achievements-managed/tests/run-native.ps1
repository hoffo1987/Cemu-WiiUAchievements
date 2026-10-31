param([string]$OutputDirectory = (Join-Path ([IO.Path]::GetTempPath()) ('WiiUAchievements-native-' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
& (Join-Path $project 'build.ps1') -OutputDirectory $output
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $output 'NativeCemuTests.exe'
& $compiler /nologo /langversion:5 /target:exe /platform:x64 /utf8output /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:Accessibility.dll "/r:$output\WiiUAchievements.exe" "/out:$exe" (Join-Path $PSScriptRoot 'NativeCemuTests.cs') (Join-Path $PSScriptRoot 'NativeCemuUiTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compilazione test del collegamento Cemu fallita.' }
& $exe (Join-Path $output 'synthetic-state')
if ($LASTEXITCODE -ne 0) { throw 'Test del collegamento Cemu falliti.' }
