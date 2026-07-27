$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$framework64 = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$framework32 = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
$compiler = if (Test-Path -LiteralPath $framework64) { $framework64 } else { $framework32 }

if (-not (Test-Path -LiteralPath $compiler)) {
    throw ".NET Framework C# compiler was not found. Enable .NET Framework 4.8."
}

$outputDirectory = Join-Path $projectRoot "dist"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$outputFile = Join-Path $outputDirectory "TranslationResultTests.exe"
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot "src") -Filter "*.cs" |
    Sort-Object Name |
    ForEach-Object { $_.FullName }
$testFile = Join-Path $projectRoot "tests\TranslationResultTests.cs"

$compilerArguments = @(
    "/nologo",
    "/target:exe",
    "/platform:anycpu",
    "/codepage:65001",
    "/main:TranslationByLocalAITests.TranslationResultTests",
    "/out:$outputFile",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.Net.Http.dll",
    "/reference:System.Security.dll",
    "/reference:System.Web.Extensions.dll",
    "/reference:System.Windows.Forms.dll"
) + $sourceFiles + $testFile

$dictionaryDirectory = Join-Path $outputDirectory "Dictionaries"
New-Item -ItemType Directory -Path $dictionaryDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot "resources\ecdict-learning.tsv.gz") `
    -Destination $dictionaryDirectory -Force

& $compiler $compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "Test build failed with exit code $LASTEXITCODE"
}

& $outputFile
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed with exit code $LASTEXITCODE"
}
