<#
.SYNOPSIS
  发布桌面端（Avalonia）。

.DESCRIPTION
  桌面端是端侧离线应用：引擎、数据库与材料生成全部在本机完成，不需要服务器。
  本脚本按平台发布自包含可执行文件，并可选打 Windows 安装包。

.PARAMETER Platform
  windows | linux | macos。默认 windows。

.PARAMETER SelfContained
  是否自包含 .NET 运行时。默认 true（目标机器不需要另装 .NET）。

.PARAMETER Installer
  是否额外生成 Windows 安装包（需要本机装有 Inno Setup）。仅对 windows 有效。

.EXAMPLE
  ./scripts/publish-desktop.ps1 -Platform windows -Installer
  ./scripts/publish-desktop.ps1 -Platform linux
#>
[CmdletBinding()]
param(
  [ValidateSet('windows', 'linux', 'macos')]
  [string]$Platform = 'windows',

  [bool]$SelfContained = $true,

  [switch]$Installer
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/SoftCopyright.Desktop/SoftCopyright.Desktop.csproj'
$outRoot = Join-Path $repoRoot "artifacts/desktop/$Platform"

$rid = switch ($Platform) {
  'windows' { 'win-x64' }
  'linux'   { 'linux-x64' }
  'macos'   { 'osx-arm64' }
}

if ($Platform -eq 'macos' -and -not (Test-Path "$env:HOME/.dotnet/publish")) {
  Write-Host "提示：macOS 产物可在 Windows 上交叉发布，但要在 Mac 上运行还需要对应架构的原生依赖。"
}

Write-Host "==> 发布桌面端：$Platform ($rid)，自包含=$SelfContained"

dotnet publish $project `
  -c Release `
  -r $rid `
  --self-contained $SelfContained `
  -p:PublishTrimmed=false `
  -p:PublishSingleFile=false `
  -o $outRoot

Write-Host "==> 发布完成：$outRoot"

if ($Installer) {
  if ($Platform -ne 'windows') {
    Write-Warning "安装包仅支持 Windows 平台，已跳过。"
  }
  elseif (-not (Get-Command iscc.exe -ErrorAction SilentlyContinue) -and
          -not (Test-Path 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe')) {
    Write-Warning "未检测到 Inno Setup（ISCC.exe），已跳过安装包生成。可安装 Inno Setup 6 后重试。"
  }
  else {
    $iscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
    if (-not (Test-Path $iscc)) { $iscc = (Get-Command iscc.exe).Source }

    $iss = Join-Path $PSScriptRoot 'desktop.iss'
    Write-Host "==> 生成 Windows 安装包：$iss"
    & $iscc $iss
    Write-Host "==> 安装包输出：$outRoot/installer"
  }
}

Write-Host ''
Write-Host '产物清单：'
Get-ChildItem $outRoot -Filter 'SoftCopyright.Desktop*' | Select-Object Name, Length | Format-Table -AutoSize
