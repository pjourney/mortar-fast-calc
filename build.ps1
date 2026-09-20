param([string]$OutputDirectory = 'dist')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path $compiler)) { throw 'The Windows .NET Framework 4.x compiler is required.' }
$output = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Force $output | Out-Null
$refs = @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$refs += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework "WPF\$_") }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output "/out:$output\WardogsFastCalc.exe" "/win32manifest:$root\src\app.manifest" "/win32icon:$root\src\AppIcon.ico" "/resource:$root\src\AppIcon.ico,AppIcon.ico" "/resource:$root\src\MainWindow.xaml,MainWindow.xaml" "/resource:$root\src\l81.csv,l81.csv" @refs "$root\src\Core.cs" "$root\src\App.cs" "$root\src\MortarScene.cs" "$root\tests\Tests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output "Built $output\WardogsFastCalc.exe"
