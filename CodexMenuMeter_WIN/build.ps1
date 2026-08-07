param(
    [switch]$Test,
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

if ($Test) {
    $testExe = Join-Path $buildDir 'CodexMenuMeterTests.exe'
    & $compiler /nologo /target:exe /out:$testExe /main:CodexMenuMeter.Tests /reference:System.Web.Extensions.dll `
        (Join-Path $projectRoot 'src\Domain.cs') `
        (Join-Path $projectRoot 'src\AppServerClient.cs') `
        (Join-Path $projectRoot 'tests\CodexMenuMeterTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    exit 0
}

throw 'Application build is added after the production entry point exists.'
