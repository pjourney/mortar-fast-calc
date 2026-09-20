$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
# Only reviewed paths are packaged. Never recurse through local test/build folders.
$files = @(
 'README.md','RESEARCH.md','SECURITY_AUDIT.md','THIRD-PARTY-NOTICES.txt',
 'build.ps1','package.ps1','.gitignore','.gitattributes',
 'scripts/audit_repo.py',
 'src/App.cs','src/Core.cs','src/MortarScene.cs','src/MainWindow.xaml',
 'src/app.manifest','src/l81.csv','src/AppIcon.ico','src/AppIcon.png','src/AppIcon.svg','src/build-icon.ps1',
 'research/UPSTREAM-LICENSE.txt','research/upstream-commit.txt','research/features.md',
 'research/results.js','research/coordinates.js','research/weapons.json',
 'research/bakurani.json','research/ozeti.json','research/zestafona.json',
 'tests/Tests.cs','tests/VALIDATION.md','tests/test-results.txt',
 'tests/app-example.png','tests/app-minimum-size.png','tests/app-out-of-range.png','tests/app-top-view.png'
)
$destination = Join-Path $PSScriptRoot 'WardogsFastCalc-Windows.zip'
$stream = [IO.File]::Open($destination,[IO.FileMode]::Create)
$archive = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
try {
 foreach ($relative in $files) {
  $source = Join-Path $PSScriptRoot $relative
  if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw "Required package file missing: $relative" }
  [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$source,$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
 }
 [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $PSScriptRoot 'dist/WardogsFastCalc.exe'),'WardogsFastCalc.exe',[IO.Compression.CompressionLevel]::Optimal) | Out-Null
} finally { $archive.Dispose(); $stream.Dispose() }
$binaryHash = (Get-FileHash (Join-Path $PSScriptRoot 'dist/WardogsFastCalc.exe') -Algorithm SHA256).Hash
"$binaryHash  WardogsFastCalc.exe" | Set-Content (Join-Path $PSScriptRoot 'dist/SHA256.txt')
Write-Output "Packaged $($files.Count + 1) explicitly listed files."
