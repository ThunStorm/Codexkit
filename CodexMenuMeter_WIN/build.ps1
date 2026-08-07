param(
    [switch]$Test,
    [switch]$Live,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$buildDir = Join-Path $projectRoot 'build'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if ($Clean -and (Test-Path -LiteralPath $buildDir)) {
    Remove-Item -Recurse -Force -LiteralPath $buildDir
}

New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

if ($Test -or $Live) {
    $testExe = Join-Path $buildDir 'CodexMenuMeterTests.exe'
    & $compiler /nologo /target:exe /out:$testExe /main:CodexMenuMeter.Tests `
        /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
        (Join-Path $projectRoot 'src\Domain.cs') `
        (Join-Path $projectRoot 'src\AppServerClient.cs') `
        (Join-Path $projectRoot 'src\TrayApplicationContext.cs') `
        (Join-Path $projectRoot 'tests\CodexMenuMeterTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    if ($Live) { & $testExe --live } else { & $testExe }
    if ($LASTEXITCODE -ne 0) { throw $(if ($Live) { 'Live validation failed.' } else { 'Tests failed.' }) }
    exit 0
}

$appExe = Join-Path $buildDir 'CodexMenuMeter.exe'
& $compiler /nologo /target:winexe /out:$appExe /main:CodexMenuMeter.Program `
    /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
    (Join-Path $projectRoot 'src\Domain.cs') `
    (Join-Path $projectRoot 'src\AppServerClient.cs') `
    (Join-Path $projectRoot 'src\TrayApplicationContext.cs') `
    (Join-Path $projectRoot 'src\Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
Write-Output $appExe
