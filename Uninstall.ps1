$ErrorActionPreference = 'Stop'
$directory = Join-Path $env:LOCALAPPDATA 'Programs\MacShotThumbnail'
$executable = Join-Path $directory 'MacShotThumbnail.exe'
Get-Process MacShotThumbnail -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $executable } | Stop-Process
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name MacShotThumbnail -ErrorAction SilentlyContinue
foreach ($name in @('MacShotThumbnail.exe', 'LICENSE', 'README.md', 'Uninstall.ps1')) {
    $file = Join-Path $directory $name
    if (Test-Path -LiteralPath $file -PathType Leaf) { Remove-Item -LiteralPath $file }
}
Write-Host 'Uninstalled. Your screenshots were kept.'
