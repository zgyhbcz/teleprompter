<#
.SYNOPSIS
    编译 TiCi（提词器）。

.DESCRIPTION
    只依赖 Windows 自带的 .NET Framework 4.x C# 编译器（csc.exe），
    不需要安装 Visual Studio，也不需要 .NET SDK。

.EXAMPLE
    pwsh -File src/build.ps1
    pwsh -File src/build.ps1 -OutputPath D:\out\TiCi.exe
#>
[CmdletBinding()]
param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$src  = $PSScriptRoot
$root = Split-Path $src -Parent
if (-not $OutputPath) { $OutputPath = Join-Path $root 'dist\TiCi.exe' }

$outDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
if (Test-Path $OutputPath) { Remove-Item $OutputPath -Force }

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) {
    throw '找不到 .NET Framework 4.x 的 csc.exe，请先安装 .NET Framework 4.x（Windows 10/11 已内置）。'
}

Write-Host "编译器: $csc"

$sources = Get-ChildItem -Path $src -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) { throw "在 $src 下没有找到 .cs 源文件。" }

$cscArgs = @(
    '/nologo'
    '/target:winexe'
    "/out:$OutputPath"
    "/win32manifest:$(Join-Path $src 'app.manifest')"
    "/win32icon:$(Join-Path $src 'app.ico')"
    '/codepage:65001'
    '/optimize+'
    '/r:System.dll'
    '/r:System.Drawing.dll'
    '/r:System.Windows.Forms.dll'
) + $sources

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "编译失败，csc 退出码 $LASTEXITCODE" }

$fi = Get-Item $OutputPath
Write-Host ("已生成: {0}  ({1:N1} KB)" -f $OutputPath, ($fi.Length / 1KB))
