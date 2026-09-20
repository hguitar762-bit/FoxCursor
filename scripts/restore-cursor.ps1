# Emergency standalone recovery: no FoxCursor or .NET Desktop Runtime is needed.
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class FoxCursorRecovery {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint parameter, IntPtr value, uint flags);
}
'@
if (-not [FoxCursorRecovery]::SystemParametersInfo(0x0057, 0, [IntPtr]::Zero, 0)) {
    throw "Windows did not reload the saved cursor scheme. Error: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
}
Write-Host 'Windows cursor scheme restored.'
