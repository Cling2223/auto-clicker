$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$testOutput = Join-Path $projectRoot 'tests\bin'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}

New-Item -ItemType Directory -Force -Path $testOutput | Out-Null

& $compiler /nologo /target:exe /optimize+ /platform:anycpu `
    /main:LightweightAutoClicker.BackgroundClickIntegration `
    /out:"$testOutput\BackgroundClickIntegration.exe" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    "$projectRoot\Models.cs" `
    "$projectRoot\NativeMethods.cs" `
    "$projectRoot\tests\BackgroundClickIntegration.cs"

if ($LASTEXITCODE -ne 0) {
    throw "Test build failed with exit code $LASTEXITCODE."
}

& "$testOutput\BackgroundClickIntegration.exe"
if ($LASTEXITCODE -ne 0) {
    throw "Integration test failed with exit code $LASTEXITCODE."
}
