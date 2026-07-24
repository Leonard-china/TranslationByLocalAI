$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$framework64 = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$framework32 = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
$compiler = if (Test-Path -LiteralPath $framework64) { $framework64 } else { $framework32 }

if (-not (Test-Path -LiteralPath $compiler)) {
    throw ".NET Framework C# compiler was not found. Enable .NET Framework 4.8."
}

$outputDirectory = Join-Path $projectRoot "dist"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$outputFile = Join-Path $outputDirectory "TranslationByLocalAI.exe"
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot "src") -Filter "*.cs" |
    Sort-Object Name |
    ForEach-Object { $_.FullName }

$compilerArguments = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/codepage:65001",
    "/out:$outputFile",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Net.Http.dll",
    "/reference:System.Web.Extensions.dll",
    "/reference:System.Windows.Forms.dll"
) + $sourceFiles

& $compiler $compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Write-Host "Build complete: $outputFile"
