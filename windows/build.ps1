$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$output=Join-Path $root 'dist\windows'
New-Item -ItemType Directory -Force $output | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe ("/out:"+(Join-Path $output 'MidiToVocal.exe')) ("/win32icon:"+(Join-Path $root 'assets\VocalGrid.ico')) ("/win32manifest:"+(Join-Path $root 'assets\VocalGrid.manifest')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'MidiToVocal.cs') (Join-Path $PSScriptRoot 'ModernWindow.cs') (Join-Path $PSScriptRoot 'Preferences.cs')
if($LASTEXITCODE -ne 0){throw 'Build failed. Close any running copy and retry.'}
