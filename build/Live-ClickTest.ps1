# Live mouse-click regression test for the custom title bar (real SendInput-level clicks,
# not UIA Invoke): buttons must click, empty title bar must drag. Run locally on Windows
# with a desktop session; moves the physical cursor and restores it afterwards.
[CmdletBinding()]
param(
    [string] $ExePath = (Join-Path $PSScriptRoot '..\src\Perenos\bin\Debug\net10.0\win-x64\Perenos.exe')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class LiveClick {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; public int Width { get { return Right - Left; } } public int Height { get { return Bottom - Top; } } }
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out RECT pt);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  public static uint PidOf(IntPtr hWnd) { uint pid; GetWindowThreadProcessId(hWnd, out pid); return pid; }
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
  [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
  public const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004;
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(80);
    mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(60);
    mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(450);
  }
  public static void Drag(int x1, int y1, int x2, int y2) {
    SetCursorPos(x1, y1);
    System.Threading.Thread.Sleep(80);
    mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
    for (int i = 1; i <= 12; i++) {
      SetCursorPos(x1 + (x2 - x1) * i / 12, y1 + (y2 - y1) * i / 12);
      System.Threading.Thread.Sleep(30);
    }
    System.Threading.Thread.Sleep(150);
    mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(300);
  }
}
'@
$failures = 0
function Check([string] $Name, [bool] $Condition) {
    if ($Condition) { Write-Output "PASS $Name" } else { Write-Output "FAIL $Name"; $script:failures++ }
}
function RootOf([IntPtr] $Handle) {
    return [System.Windows.Automation.AutomationElement]::FromHandle($Handle)
}
function Buttons($Root) {
    return $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
}
function ButtonRect($Root, [string] $Name) {
    $end = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $found = @(Buttons $Root | Where-Object { $_.Current.Name -eq $Name })
        if ($found.Count) { return $found[0].Current.BoundingRectangle }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $end)
    throw "Button not found: $Name; available: " + ((@(Buttons $Root | ForEach-Object { $_.Current.Name }) -join ' | '))
}
function ClickButton($Root, [string] $Name) {
    $r = ButtonRect $Root $Name
    [LiveClick]::Click([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
}
function RectOf([IntPtr] $Handle) {
    $rect = New-Object LiveClick+RECT
    [LiveClick]::GetWindowRect($Handle, [ref]$rect) | Out-Null
    return $rect
}

[LiveClick]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
Write-Output ("system dpi: " + [LiveClick]::GetDpiForSystem())
$savedCursor = New-Object LiveClick+RECT
[LiveClick]::GetCursorPos([ref]$savedCursor) | Out-Null
$proc = Start-Process -FilePath $ExePath -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while ($proc.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200; $proc.Refresh() }
    $main = $proc.MainWindowHandle
    if ($main -eq 0) { throw 'Main window never appeared.' }
    Start-Sleep -Seconds 2
    $root = RootOf $main

    # 0. Activate the app window with a real click on the empty title bar area.
    $r0 = RectOf $main
    [LiveClick]::SetForegroundWindow($main) | Out-Null
    [LiveClick]::Click([int]($r0.Left + $r0.Width / 2), [int]($r0.Top + 22))
    Check 'app window takes foreground on click' ([LiveClick]::PidOf([LiveClick]::GetForegroundWindow()) -eq $proc.Id)
    $root = RootOf $main

    # 1. Real mouse click on the settings button must open the settings dialog.
    ClickButton $root 'Настройки'
    $fg = [LiveClick]::GetForegroundWindow()
    $dialogRoot = RootOf $fg
    $combos = $dialogRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)))
    Check 'settings opens on real mouse click' ($fg -ne $main -and [LiveClick]::PidOf($fg) -eq $proc.Id -and $combos.Count -ge 2)

    # 2. Real mouse click on the dialog close button must close it.
    ClickButton $dialogRoot 'Закрыть'
    Start-Sleep -Milliseconds 400
    Check 'dialog closes on real mouse click' ([LiveClick]::PidOf([LiveClick]::GetForegroundWindow()) -eq $proc.Id -and [LiveClick]::GetForegroundWindow() -eq $main)

    # 3. Minimize caption button.
    ClickButton $root 'Свернуть'
    Check 'minimize caption works' ([LiveClick]::IsIconic($main))
    [LiveClick]::ShowWindow($main, 9) | Out-Null
    Start-Sleep -Milliseconds 600

    # 4. Maximize caption button toggles both ways.
    $before = RectOf $main
    ClickButton $root 'Развернуть'
    $max = RectOf $main
    $work = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    Write-Output ("before={0},{1} {2}x{3} max={4},{5} {6}x{7} work={8}x{9}" -f $before.Left, $before.Top, $before.Width, $before.Height, $max.Left, $max.Top, $max.Width, $max.Height, $work.Width, $work.Height)
    Check 'maximize caption works' ($max.Width -ge $work.Width - 8 -and $max.Height -ge $work.Height - 8)
    ClickButton $root 'Развернуть'
    $restored = RectOf $main
    Write-Output ("restored={0},{1} {2}x{3}" -f $restored.Left, $restored.Top, $restored.Width, $restored.Height)
    Check 'restore caption works' ([Math]::Abs($restored.Width - $before.Width) -le 4 -and [Math]::Abs($restored.Height - $before.Height) -le 4)

    # 5. Dragging the empty title bar area still moves the window.
    $start = RectOf $main
    [LiveClick]::Drag([int]($start.Left + $start.Width / 2), [int]($start.Top + 22), [int]($start.Left + $start.Width / 2 + 120), [int]($start.Top + 22 + 60))
    $moved = RectOf $main
    Write-Output ("drag start={0},{1} moved={2},{3}" -f $start.Left, $start.Top, $moved.Left, $moved.Top)
    Check 'empty title bar drag moves window' (($moved.Left - $start.Left) -ge 100 -and ($moved.Left - $start.Left) -le 126 -and ($moved.Top - $start.Top) -ge 50 -and ($moved.Top - $start.Top) -le 66)

    # 6. Real mouse click on the close caption exits the app.
    ClickButton $root 'Закрыть'
    Check 'close caption exits app' ($proc.WaitForExit(6000))
}
finally {
    [LiveClick]::SetCursorPos($savedCursor.Left, $savedCursor.Top) | Out-Null
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
}
if ($failures) { throw "$failures live-click checks failed." }
Write-Output 'ALL LIVE CLICK CHECKS PASSED'
