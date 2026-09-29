$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'MacShotThumbnail.exe'
if (!(Test-Path -LiteralPath $source -PathType Leaf)) {
    throw 'Extract the release ZIP first. MacShotThumbnail.exe must be beside Install.ps1.'
}
$destination = Join-Path $env:LOCALAPPDATA 'Programs\MacShotThumbnail'
$executable = Join-Path $destination 'MacShotThumbnail.exe'
$userSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$taskName = 'MacShotThumbnail-' + $userSid
$scheduler = New-Object -ComObject Schedule.Service
$scheduler.Connect()
try { $scheduler.GetFolder('\').GetTask($taskName).Stop(0) }
catch { if ($_.Exception.HResult -ne -2147024894) { throw } }
Get-Process MacShotThumbnail -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $executable } | Stop-Process
New-Item -ItemType Directory -Force -Path $destination | Out-Null
if ([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($executable)) {
    Copy-Item -LiteralPath $source -Destination $executable -Force
}
foreach ($name in @('Uninstall.ps1', 'LICENSE', 'README.md', 'DOTNET-LICENSE.txt', 'WINDOWSDESKTOP-LICENSE.txt')) {
    $file = Join-Path $PSScriptRoot $name
    if ((Test-Path -LiteralPath $file) -and ([IO.Path]::GetFullPath($file) -ne (Join-Path $destination $name))) {
        Copy-Item -LiteralPath $file -Destination $destination -Force
    }
}
$documentation = Join-Path $PSScriptRoot 'docs'
if ((Test-Path -LiteralPath $documentation -PathType Container) -and ([IO.Path]::GetFullPath($PSScriptRoot) -ne [IO.Path]::GetFullPath($destination))) {
    Copy-Item -LiteralPath $documentation -Destination $destination -Recurse -Force
}
$task = $scheduler.NewTask(0)
$task.RegistrationInfo.Description = 'Start MacShot after sign-in and restart it after a failure.'
$task.Principal.UserId = $userSid
$task.Principal.LogonType = 3
$task.Principal.RunLevel = 0
$task.Settings.DisallowStartIfOnBatteries = $false
$task.Settings.StopIfGoingOnBatteries = $false
$task.Settings.ExecutionTimeLimit = 'PT0S'
$task.Settings.StartWhenAvailable = $true
$task.Settings.MultipleInstances = 2
$task.Settings.RestartCount = 3
$task.Settings.RestartInterval = 'PT1M'
$trigger = $task.Triggers.Create(9)
$trigger.UserId = $userSid
$trigger.Delay = 'PT10S'
$action = $task.Actions.Create(0)
$action.Path = $executable
$action.Arguments = '--supervise'
$action.WorkingDirectory = $destination
$registered = $scheduler.GetFolder('\').RegisterTaskDefinition($taskName, $task, 6, $userSid, $null, 3)
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name MacShotThumbnail -ErrorAction SilentlyContinue
$registered.Run($null) | Out-Null
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'MacShot Settings.lnk'
$shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcutPath)
$shortcut.TargetPath = $executable
$shortcut.Arguments = '--settings'
$shortcut.WorkingDirectory = $destination
$shortcut.Save()
Write-Host 'Installed. Press Print Screen to capture an area.'
