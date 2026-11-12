param([switch]$Test)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$common = @('/nologo', '/codepage:65001', '/optimize+', '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll')
& $compiler @common '/target:winexe' ('/out:' + (Join-Path $output 'Hitote.exe')) (Join-Path $PSScriptRoot 'InputEngine.cs') (Join-Path $PSScriptRoot 'KeyboardService.cs') (Join-Path $PSScriptRoot 'Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Write-Host ('Built: ' + (Join-Path $output 'Hitote.exe'))
if ($Test) {
    & $compiler @common '/target:exe' ('/out:' + (Join-Path $output 'Hitote.Tests.exe')) (Join-Path $PSScriptRoot 'InputEngine.cs') (Join-Path $PSScriptRoot 'SelfTest.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & (Join-Path $output 'Hitote.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
