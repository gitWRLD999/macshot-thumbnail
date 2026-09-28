$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'MacShotThumbnail.exe'
if (!(Test-Path -LiteralPath $source -PathType Leaf)) {
    throw 'Extract the release ZIP first. MacShotThumbnail.exe must be beside Install.ps1.'
}
$destination = Join-Path $env:LOCALAPPDATA 'Programs\MacShotThumbnail'
$executable = Join-Path $destination 'MacShotThumbnail.exe'
Get-Process MacShotThumbnail -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $executable } | Stop-Process
New-Item -ItemType Directory -Force -Path $destination | Out-Null
if ([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($executable)) {
    Copy-Item -LiteralPath $source -Destination $executable -Force
}
foreach ($name in @('Uninstall.ps1', 'LICENSE', 'README.md')) {
    $file = Join-Path $PSScriptRoot $name
    if ((Test-Path -LiteralPath $file) -and ([IO.Path]::GetFullPath($file) -ne (Join-Path $destination $name))) {
        Copy-Item -LiteralPath $file -Destination $destination -Force
    }
}
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-Item -Path $runKey -Force | Out-Null
New-ItemProperty -Path $runKey -Name MacShotThumbnail -PropertyType String -Value ('"' + $executable + '"') -Force | Out-Null
Start-Process -FilePath $executable -WindowStyle Hidden
Write-Host 'Installed. Press Print Screen to capture an area.'
