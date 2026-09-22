Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinFinder {
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
"@

$targetPid = 76264
[WinFinder]::EnumWindows({
    param($hWnd, $lParam)
    $pid = 0
    [WinFinder]::GetWindowThreadProcessId($hWnd, [ref]$pid) | Out-Null
    if ($pid -eq $targetPid) {
        $sb = New-Object System.Text.StringBuilder 512
        [WinFinder]::GetWindowText($hWnd, $sb, 512) | Out-Null
        $title = $sb.ToString()
        $vis = [WinFinder]::IsWindowVisible($hWnd)
        Write-Output "HWND: $hWnd | Vis: $vis | Title: $title"
        if ($title -like "*Unity*") {
            Write-Output "Activating Unity window $hWnd"
            [WinFinder]::ShowWindow($hWnd, 9)
            [WinFinder]::SetForegroundWindow($hWnd)
            # Send WM_ACTIVATE (0x0006), WA_ACTIVE (1)
            [WinFinder]::SendMessage($hWnd, 0x0006, [IntPtr]1, [IntPtr]::Zero)
            # Send WM_SETFOCUS (0x0007)
            [WinFinder]::SendMessage($hWnd, 0x0007, [IntPtr]::Zero, [IntPtr]::Zero)
        }
    }
    return $true
}, [IntPtr]::Zero)
