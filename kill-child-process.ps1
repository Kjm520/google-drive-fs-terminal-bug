# If Drive is not currently running, start it like normal from the desktop icon, start menu, or exe directly. If drive IS currently running and the command fails to reproduce the bug, close Drive entirely manually, from either it's tray/status icon or any other method, then relaunch it normally. 

$dq = "Name = 'GoogleDriveFS.exe'";
$child = Get-CimInstance Win32_Process -Property CommandLine -Filter $dq | Select-Object Handle, CommandLine | Where-Object -Property CommandLine -match "--crash_handler";
Stop-Process $child.Handle