$ErrorActionPreference = 'Stop'
$directory = Join-Path $env:LOCALAPPDATA 'Programs\MacShotThumbnail'
$executable = Join-Path $directory 'MacShotThumbnail.exe'
$taskName = 'MacShotThumbnail-' + [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$scheduler = New-Object -ComObject Schedule.Service
$scheduler.Connect()
try { $scheduler.GetFolder('\').GetTask($taskName).Stop(0); $scheduler.GetFolder('\').DeleteTask($taskName, 0) }
catch { if ($_.Exception.HResult -ne -2147024894) { throw } }
Get-Process MacShotThumbnail -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $executable } | Stop-Process
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name MacShotThumbnail -ErrorAction SilentlyContinue
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'MacShot Settings.lnk'
if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath }
foreach ($name in @('MacShotThumbnail.exe', 'LICENSE', 'README.md', 'Uninstall.ps1', 'DOTNET-LICENSE.txt', 'WINDOWSDESKTOP-LICENSE.txt')) {
    $file = Join-Path $directory $name
    if (Test-Path -LiteralPath $file -PathType Leaf) { Remove-Item -LiteralPath $file }
}
Write-Host 'Uninstalled. Your screenshots were kept.'
