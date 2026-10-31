$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('WiiUAchievements-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$exe = Join-Path $testRoot 'CoreTests.exe'
& $compiler /nologo /langversion:5 /target:exe /platform:x64 /utf8output /r:System.Xml.Linq.dll /r:System.Web.Extensions.dll /r:System.Security.dll "/out:$exe" (Join-Path $project 'src\CloudSync.cs') (Join-Path $project 'src\UserData.cs') (Join-Path $project 'src\Gameplay.cs') (Join-Path $project 'src\Core.cs') (Join-Path $project 'src\WindWakerProgression.cs') (Join-Path $project 'src\MemoryReader.cs') (Join-Path $project 'src\GameSession.cs') (Join-Path $project 'src\GameCatalog.cs') (Join-Path $PSScriptRoot 'GameplayTests.cs') (Join-Path $PSScriptRoot 'RecoveryTests.cs') (Join-Path $PSScriptRoot 'CloudTests.cs') (Join-Path $PSScriptRoot 'CoreTests.cs') (Join-Path $PSScriptRoot 'SystemTests.cs') (Join-Path $PSScriptRoot 'ProgressionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compilazione test fallita.' }
Copy-Item -LiteralPath (Join-Path $project 'achievements.xml') -Destination $testRoot
Copy-Item -LiteralPath (Join-Path $project 'achievements-planned.xml') -Destination $testRoot
Copy-Item -LiteralPath (Join-Path $project 'achievements-story.xml') -Destination $testRoot
Copy-Item -LiteralPath (Join-Path $project 'achievement-presentation.xml') -Destination $testRoot
& $exe (Join-Path $testRoot 'results')
if ($LASTEXITCODE -ne 0) { throw 'Test falliti.' }
