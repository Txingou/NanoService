param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe',
    [string]$UnityPackage = 'D:\Project_Net\Codex\NanoService\Data\NuGetForUnity.4.5.0.unitypackage',
    [string]$ProjectPath = 'D:\Project_Net\Codex\NanoService\experiments\unity-verify\Unity'
)

$ErrorActionPreference = 'Stop'

dotnet tool install --global NuGetForUnity.Cli --version 4.5.0
nugetforunity restore $ProjectPath

# 批处理模式的 -importPackage 在部分 Unity 版本上不会落地文件，因此这里直接展开 unitypackage。
$temp = Join-Path $env:TEMP 'nufu-unitypackage'
if (Test-Path $temp) {
    Remove-Item -Recurse -Force $temp
}

New-Item -ItemType Directory -Path $temp | Out-Null
tar -xzf $UnityPackage -C $temp

foreach ($dir in Get-ChildItem -Directory $temp) {
    $pathFile = Join-Path $dir.FullName 'pathname'
    if (-not (Test-Path $pathFile)) {
        continue
    }

    $relative = (Get-Content -Raw -Encoding UTF8 $pathFile).Trim()
    $asset = Join-Path $dir.FullName 'asset'
    $meta = Join-Path $dir.FullName 'asset.meta'
    if (-not (Test-Path $asset)) {
        continue
    }

    $destination = Join-Path $ProjectPath $relative
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $asset -Destination $destination -Force
    if (Test-Path $meta) {
        Copy-Item -LiteralPath $meta -Destination "$destination.meta" -Force
    }
}

& $UnityEditor -batchmode -quit -projectPath $ProjectPath -executeMethod UnityVerify.VerifyBuild.BuildWindowsServer -logFile (Join-Path $ProjectPath '..\build-server.log')
& $UnityEditor -batchmode -quit -projectPath $ProjectPath -executeMethod UnityVerify.VerifyBuild.BuildWindowsClient -logFile (Join-Path $ProjectPath '..\build-client.log')
