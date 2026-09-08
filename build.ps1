$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path $projectRoot 'bin\Release'
$appName = 'AUTO CLICKER by ' + (-join ([char]0x4E00, [char]0x53F6, [char]0x4E36, [char]0x77E5, [char]0x79CB))
$outputExe = Join-Path $outputDir ($appName + '.exe')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}

if (-not (Test-Path $compiler)) {
    throw '.NET Framework C# compiler was not found.'
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /win32icon:"$projectRoot\app.ico" `
    /out:"$outputExe" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Xml.dll `
    "$projectRoot\Program.cs" `
    "$projectRoot\Models.cs" `
    "$projectRoot\NativeMethods.cs" `
    "$projectRoot\ClickDispatcher.cs" `
    "$projectRoot\ClickEngine.cs" `
    "$projectRoot\ClickVisualizer.cs" `
    "$projectRoot\MainForm.cs"

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Copy-Item -Force "$projectRoot\App.config" ($outputExe + '.config')
Write-Host "Built: $outputExe"
