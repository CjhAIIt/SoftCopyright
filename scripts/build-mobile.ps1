<#
.SYNOPSIS
  构建移动端（Android）安装包。

.DESCRIPTION
  移动端与桌面端共用同一套界面与引擎源码（src/SoftCopyright.Client）。
  本脚本负责构建并打包 APK。

  前置条件：安装 .NET Android 工作负载
      dotnet workload install android
  若机器上装有 Android SDK（ANDROID_HOME / ANDROID_SDK_ROOT），
  会使用它；否则使用 .NET 自带的 Android SDK 打包工具。

.EXAMPLE
  ./scripts/build-mobile.ps1
  ./scripts/build-mobile.ps1 -Configuration Release
#>
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/SoftCopyright.Mobile/SoftCopyright.Mobile.csproj'
$outRoot = Join-Path $repoRoot 'artifacts/mobile/android'

Write-Host "==> 检查 Android 工作负载"
$workloads = dotnet workload list
if ($workloads -notmatch '^\s*android\s' -and $workloads -notmatch '\bandroid\b') {
    throw "未安装 Android 工作负载。请先执行：dotnet workload install android"
}

Write-Host "==> 发布 APK（$Configuration）"
dotnet publish $project -c $Configuration -v minimal --nologo

$apk = Get-ChildItem (Join-Path $repoRoot 'src/SoftCopyright.Mobile/bin') -Filter '*.apk' -Recurse -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $apk) {
    throw "未找到生成的 APK。"
}

New-Item -ItemType Directory -Force -Path $outRoot | Out-Null
$target = Join-Path $outRoot $apk.Name
Copy-Item $apk.FullName $target -Force

Write-Host ''
Write-Host "APK：$target（$([math]::Round($apk.Length / 1mb, 2)) MB）"
Write-Host '安装到设备：adb install -r <apk 路径>'
