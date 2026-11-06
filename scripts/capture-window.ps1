param(
  [Parameter(Mandatory = $true)][string]$Out,
  [string]$ProcessName = "SoftCopyright.Desktop",
  [int]$Height = 0,
  [int]$Width = 0
)

# 抓取本机原生窗口内容（PrintWindow），用于桌面端界面自查。
# 传入 -Height/-Width 时先把窗口还原并调整尺寸，方便截取超出屏幕高度的长页面。
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not ("WinCap" -as [type])) {
  Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinCap {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int X, int Y, int cx, int cy, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
}

$p = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { throw "进程未运行：$ProcessName" }

$h = $p.MainWindowHandle

if ($Height -gt 0 -or $Width -gt 0) {
  [void][WinCap]::ShowWindow($h, 9)   # SW_RESTORE
  Start-Sleep -Milliseconds 600
  $r0 = New-Object WinCap+RECT
  [void][WinCap]::GetWindowRect($h, [ref]$r0)
  $w = if ($Width -gt 0) { $Width } else { $r0.Right - $r0.Left }
  $hh = if ($Height -gt 0) { $Height } else { $r0.Bottom - $r0.Top }
  [void][WinCap]::SetWindowPos($h, [IntPtr]::Zero, 0, 0, $w, $hh, 0x0004)
  Start-Sleep -Seconds 2
}
else {
  [void][WinCap]::ShowWindow($h, 3)
}

[void][WinCap]::SetForegroundWindow($h)
Start-Sleep -Seconds 2

$r = New-Object WinCap+RECT
[void][WinCap]::GetWindowRect($h, [ref]$r)
$w = $r.Right - $r.Left
$hh = $r.Bottom - $r.Top
if ($w -le 0 -or $hh -le 0) { throw "窗口尺寸异常" }

$bmp = New-Object System.Drawing.Bitmap $w, $hh
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [WinCap]::PrintWindow($h, $hdc, 2)
$g.ReleaseHdc($hdc)

New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()

Write-Output "PrintWindow=$ok size=${w}x${hh} saved=$Out"
