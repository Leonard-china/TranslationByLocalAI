[CmdletBinding()]
param(
    [string]$Version = "1.0.1",
    [string]$LlamaDirectory = "D:\Project\Github\LocalAI\llama\llama-b10092-bin-win-cuda-12.4-x64",
    [string]$ModelsDirectory = "",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($ModelsDirectory)) {
    $ModelsDirectory = Join-Path $LlamaDirectory "Models"
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot "release"
}

$releaseDirectory = Join-Path $OutputRoot ("v" + $Version)
$portableName = "TranslationByLocalAI-v$Version-win-x64-cuda"
$portableDirectory = Join-Path $releaseDirectory $portableName
$portableModelsDirectory = Join-Path $portableDirectory "Models"
$assetDirectory = Join-Path $releaseDirectory "assets"
$portableArchive = Join-Path $releaseDirectory ($portableName + ".zip")

if (Test-Path -LiteralPath $releaseDirectory) {
    throw "Release directory already exists: $releaseDirectory"
}

$requiredFiles = @(
    (Join-Path $LlamaDirectory "llama-server.exe"),
    (Join-Path $LlamaDirectory "llama-gguf-split.exe"),
    (Join-Path $ModelsDirectory "MiniCPM5-1B-F16.gguf"),
    (Join-Path $ModelsDirectory "Qwen3-1.7B-Q8_0.gguf"),
    (Join-Path $ModelsDirectory "Qwen3-4B-Q4_K_M.gguf")
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile)) {
        throw "Required release input is missing: $requiredFile"
    }
}

powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot "build.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "Application build failed with exit code $LASTEXITCODE"
}

New-Item -ItemType Directory -Path $portableModelsDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $portableDirectory "licenses") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $portableDirectory "Dictionaries") -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $projectRoot "dist\TranslationByLocalAI.exe") -Destination $portableDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot "dist\Dictionaries\ecdict-learning.tsv.gz") `
    -Destination (Join-Path $portableDirectory "Dictionaries")
Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination $portableDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot "MODEL-LICENSES.md") -Destination $portableDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot "licenses\Apache-2.0.txt") -Destination (Join-Path $portableDirectory "licenses")
Copy-Item -LiteralPath (Join-Path $projectRoot "licenses\ECDICT-MIT.txt") -Destination (Join-Path $portableDirectory "licenses")
Copy-Item -LiteralPath (Join-Path $projectRoot "licenses\llama.cpp-MIT.txt") -Destination (Join-Path $portableDirectory "licenses")

$runtimeFiles = @(
    "cublas64_12.dll",
    "cublasLt64_12.dll",
    "cudart64_12.dll",
    "ggml.dll",
    "ggml-base.dll",
    "ggml-cuda.dll",
    "libomp140.x86_64.dll",
    "llama.dll",
    "llama-common.dll",
    "llama-server.exe",
    "llama-server-impl.dll"
)
foreach ($runtimeFile in $runtimeFiles) {
    $runtimePath = Join-Path $LlamaDirectory $runtimeFile
    if (-not (Test-Path -LiteralPath $runtimePath)) {
        throw "Required llama.cpp runtime file is missing: $runtimePath"
    }
    Copy-Item -LiteralPath $runtimePath -Destination $portableDirectory
}
Get-ChildItem -LiteralPath $LlamaDirectory -Filter "ggml-cpu-*.dll" -File |
    Copy-Item -Destination $portableDirectory

@(
    "Place the GGUF model files downloaded from the GitHub Release in this folder.",
    "",
    "MiniCPM5-1B and Qwen3-4B each have two shards; download both shards.",
    "Select the first shard whose filename contains 00001 in the application settings."
) | Set-Content -LiteralPath (Join-Path $portableModelsDirectory "README.txt") -Encoding UTF8

$splitTool = Join-Path $LlamaDirectory "llama-gguf-split.exe"
& $splitTool --split-max-size 1900M `
    (Join-Path $ModelsDirectory "MiniCPM5-1B-F16.gguf") `
    (Join-Path $assetDirectory "MiniCPM5-1B-F16")
if ($LASTEXITCODE -ne 0) {
    throw "Failed to split MiniCPM5-1B."
}

Copy-Item -LiteralPath (Join-Path $ModelsDirectory "Qwen3-1.7B-Q8_0.gguf") -Destination $assetDirectory

& $splitTool --split-max-size 1900M `
    (Join-Path $ModelsDirectory "Qwen3-4B-Q4_K_M.gguf") `
    (Join-Path $assetDirectory "Qwen3-4B-Q4_K_M")
if ($LASTEXITCODE -ne 0) {
    throw "Failed to split Qwen3-4B."
}

Compress-Archive -LiteralPath $portableDirectory -DestinationPath $portableArchive -CompressionLevel Optimal
Copy-Item -LiteralPath (Join-Path $projectRoot "RELEASE_NOTES.md") `
    -Destination (Join-Path $releaseDirectory "README-Release.md")
Copy-Item -LiteralPath (Join-Path $projectRoot "MODEL-LICENSES.md") `
    -Destination (Join-Path $releaseDirectory "MODEL-LICENSES.md")
Copy-Item -LiteralPath (Join-Path $projectRoot "licenses\Apache-2.0.txt") `
    -Destination (Join-Path $releaseDirectory "Apache-2.0.txt")

$hashTargets = @(
    Get-Item -LiteralPath $portableArchive
    Get-Item -LiteralPath (Join-Path $releaseDirectory "README-Release.md")
    Get-Item -LiteralPath (Join-Path $releaseDirectory "MODEL-LICENSES.md")
    Get-Item -LiteralPath (Join-Path $releaseDirectory "Apache-2.0.txt")
    Get-ChildItem -LiteralPath $assetDirectory -File
)
$hashLines = $hashTargets |
    Sort-Object Name |
    ForEach-Object {
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
        "$hash  $($_.Name)"
    }
$hashLines | Set-Content -LiteralPath (Join-Path $releaseDirectory "SHA256SUMS.txt") -Encoding ASCII

Write-Host "Release prepared: $releaseDirectory"
Get-ChildItem -LiteralPath $releaseDirectory -Recurse -File |
    Select-Object FullName, Length
