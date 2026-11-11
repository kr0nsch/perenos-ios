[CmdletBinding()]
param(
    [ValidateSet('Start', 'Selection', 'Settings', 'Full')]
    [string] $Mode = 'Start',
    [ValidateSet('en-US', 'ru-RU')]
    [string] $Language = 'en-US',
    [ValidateSet('Light', 'Dark')]
    [string] $Theme = 'Light',
    [string] $OutDir = 'artifacts/avalonia-review',
    [string] $ExePath = ''
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$exe = if ($ExePath) { [IO.Path]::GetFullPath($ExePath) } else { Join-Path $repo 'src/Perenos/bin/Release/net10.0/win-x64/Perenos.exe' }
if (-not (Test-Path $exe) -or [IO.Path]::GetFileName($exe) -ne 'Perenos.exe') { throw 'Perenos.exe was not found at the expected path.' }
$strings = @{}
[xml]$resources = Get-Content (Join-Path $repo "src/Perenos.Presentation/Strings/Resources$(if ($Language -eq 'ru-RU') { '.ru' }).resx") -Encoding UTF8
foreach ($item in $resources.root.data) { $strings[$item.name] = $item.value }
function T([string]$key) { return $strings[$key] }
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class PerenosDemoCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
}
'@
$script:demo = $null
$rootDir = [IO.Path]::GetFullPath((Join-Path $repo $OutDir))
if (-not $rootDir.StartsWith([IO.Path]::GetFullPath((Join-Path $repo 'artifacts')), [StringComparison]::OrdinalIgnoreCase)) { throw 'OutDir must be inside artifacts.' }
[IO.Directory]::CreateDirectory($rootDir) | Out-Null
function Root {
    $script:demo.Refresh()
    if ($script:demo.MainWindowHandle -eq 0) { throw 'The demo main window is unavailable.' }
    $element = [System.Windows.Automation.AutomationElement]::FromHandle($script:demo.MainWindowHandle)
    $window = $element.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    if ($window.Current.WindowVisualState -eq [System.Windows.Automation.WindowVisualState]::Minimized) {
        $window.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
        Start-Sleep -Milliseconds 300
    }
    return $element
}
function Elements($root, $type) {
    for ($attempt = 0; $attempt -lt 5; $attempt++) {
        try {
            return $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)))
        }
        catch [System.Runtime.InteropServices.COMException] {
            if ($attempt -eq 4) { throw }
            Start-Sleep -Milliseconds 250
        }
    }
}
function FailureState([string]$Reason) {
    Capture "failure-$Reason"
    $text = @((Elements (Root) ([System.Windows.Automation.ControlType]::Text)) | ForEach-Object { "TEXT: $($_.Current.Name)" })
    $buttons = @((Elements (Root) ([System.Windows.Automation.ControlType]::Button)) | ForEach-Object { "BUTTON: $($_.Current.Name) / enabled=$($_.Current.IsEnabled)" })
    [IO.File]::WriteAllLines((Join-Path $rootDir "failure-$Reason.txt"), [string[]]($text + $buttons), (New-Object Text.UTF8Encoding($false)))
}
function ClickButton([string]$Text, [switch]$Last, [int]$TimeoutSeconds = 20) {
    $end = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $buttons = @((Elements (Root) ([System.Windows.Automation.ControlType]::Button)) | Where-Object { $_.Current.Name -eq $Text -and $_.Current.IsEnabled })
        if ($buttons.Count) {
            $button = if ($Last) { $buttons[-1] } else { $buttons[0] }
            $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
            Write-Output "Clicked requested: $Text"
            return
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $end)
    FailureState 'click'
    throw "No enabled button matching: $Text"
}
function SetField([int]$Index, [string]$Text) {
    $end = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $fields = Elements (Root) ([System.Windows.Automation.ControlType]::Edit)
        if ($fields.Count -gt $Index) {
            $fields[$Index].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Text)
            return
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $end)
    throw "Missing edit field $Index"
}
function SelectApp([string]$Name) {
    $end = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $checks = @((Elements (Root) ([System.Windows.Automation.ControlType]::Button)) | Where-Object { $_.Current.Name -eq $Name -and $_.Current.IsEnabled })
        if ($checks.Count) {
            $check = $checks[0]
            $pattern = $check.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
            if ($pattern.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) { $pattern.Toggle() }
            Start-Sleep -Milliseconds 250
            if ($pattern.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) {
                FailureState 'selection'
                throw "Selection did not stick for $Name"
            }
            return
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $end)
    FailureState 'select'
    throw "App checkbox not found: $Name"
}
function WaitText([string]$Text, [int]$Seconds = 25) {
    $end = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        $items = @((Elements (Root) ([System.Windows.Automation.ControlType]::Text)) | Where-Object { $_.Current.Name -eq $Text })
        if ($items.Count) { return }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $end)
    FailureState 'wait'
    throw "Timed out waiting for text: $Text"
}
function Capture([string]$Name) {
    Start-Sleep -Milliseconds 400
    $null = Root
    $handle = $script:demo.MainWindowHandle
    if ($Name.StartsWith('settings-')) {
        $dialogHandle = (SettingsRoot).Current.NativeWindowHandle
        if ($dialogHandle -ne 0) { $handle = [IntPtr]$dialogHandle }
    }
    $rect = New-Object PerenosDemoCapture+RECT
    if (-not [PerenosDemoCapture]::GetWindowRect($handle, [ref]$rect)) { throw 'Cannot read the demo window bounds.' }
    if ($rect.Right - $rect.Left -lt 500 -or $rect.Bottom - $rect.Top -lt 400) { throw 'Unexpected demo window size.' }
    $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    if ($Name.StartsWith('settings-')) {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
    } else {
        $hdc = $graphics.GetHdc()
        try { if (-not [PerenosDemoCapture]::PrintWindow($handle, $hdc, 2)) { throw 'PrintWindow failed.' } }
        finally { $graphics.ReleaseHdc($hdc) }
    }
    $path = Join-Path $rootDir "$Name.png"
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Output "Captured: $path"
}
function FinishInstall {
    WaitText (T 'Footer_Done') 40
    ClickButton (T 'Footer_Done')
}
function SettingsRoot {
    for ($attempt = 0; $attempt -lt 25; $attempt++) {
        $root = Root
        $boxes = Elements $root ([System.Windows.Automation.ControlType]::ComboBox)
        if ($boxes.Count -ge 2) {
            $handle = $boxes[0].Current.NativeWindowHandle
            if ($handle -ne 0) { return [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$handle) }
            return $root
        }
        Start-Sleep -Milliseconds 200
    }
    FailureState 'settings'
    throw 'Settings controls not found.'
}
function SetCombo([int]$Index, [string]$Text) {
    $root = SettingsRoot
    $boxes = Elements $root ([System.Windows.Automation.ControlType]::ComboBox)
    $boxes[$Index].GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $items = @((Elements $root ([System.Windows.Automation.ControlType]::ListItem)) | Where-Object { $_.Current.Name -eq $Text })
    if (-not $items.Count) { throw "No settings item: $Text" }
    $items[0].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 400
}
try {
    $env:PERENOS_LANGUAGE = $Language
    $env:PERENOS_THEME = $Theme
    $script:demo = Start-Process -FilePath $exe -ArgumentList '--demo' -PassThru
    $end = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 200
        $script:demo.Refresh()
    } while ($script:demo.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $end)
    $null = Root
    Start-Sleep -Seconds 6
    Capture "start-$Language-$Theme"
    if ($Mode -eq 'Full') {
        ClickButton (T 'Start_Now.Title')
        ClickButton (T 'Footer_Continue')
        WaitText (T 'Apps_Title')
        Start-Sleep -Seconds 2
        SetField 0 'Cloudy'
        SelectApp 'Cloudy'
        Capture "apps-$Language-$Theme"
        ClickButton (T 'Footer_Continue')
        ClickButton (T 'Footer_Continue')
        SetField 0 'demo@example.com'
        SetField 1 'not-a-real-password'
        ClickButton (T 'Account_SignIn') -Last
        WaitText (T 'Account_Verify')
        SetField 2 '123456'
        ClickButton (T 'Account_Verify') -Last
        WaitText 'demo@example.com'
        ClickButton (T 'Footer_Install')
        WaitText (T 'Install_Title_Done') 40
        Capture "install-$Language-$Theme"
        FinishInstall

        ClickButton (T 'Nav_Restore')
        ClickButton (T 'Footer_Continue')
        WaitText (T 'Restore_AppsTitle')
        Start-Sleep -Seconds 2
        SetField 0 'Cloudy'
        SelectApp 'Cloudy'
        Capture "restore-$Language-$Theme"
        ClickButton (T 'Footer_Continue')
        ClickButton (T 'Footer_Install')
        FinishInstall

        ClickButton (T 'Nav_Library')
        ClickButton (T 'Later_Save.Title')
        ClickButton (T 'Footer_Continue')
        WaitText (T 'Apps_Title')
        SetField 0 'Bloom'
        SelectApp 'Bloom Journal'
        ClickButton (T 'Footer_Continue')
        ClickButton (T 'Footer_Save')
        WaitText (T 'Save_Title_Done') 40
        Capture "saved-$Language-$Theme"
        FinishInstall
        ClickButton (T 'Later_Install.Title')
        ClickButton (T 'Footer_Continue')
        WaitText 'Bloom Journal'
        ClickButton (T 'Footer_Continue')
        ClickButton (T 'Footer_Install')
        FinishInstall
    }
    if ($Mode -eq 'Selection') {
        ClickButton (T 'Start_Now.Title')
        ClickButton (T 'Footer_Continue')
        WaitText (T 'Apps_Title')
        SetField 0 'Cloudy'
        SelectApp 'Cloudy'
        Capture "apps-$Language-$Theme"
    }
    if ($Mode -eq 'Settings') {
        if ($Language -ne 'ru-RU') { throw 'Settings mode checks a live Russian to English switch; use -Language ru-RU.' }
        ClickButton (T 'Settings_Button')
        SetCombo 1 (T 'Settings_ThemeDark')
        Capture "settings-$Language-Dark"
        SetCombo 0 'English'
        WaitText 'What would you like to do?'
        Capture "library-en-US-Dark"
        ClickButton 'Settings'
        SetCombo 1 'Light'
        $settings = SettingsRoot
        $done = @((Elements $settings ([System.Windows.Automation.ControlType]::Button)) | Where-Object { $_.Current.Name -eq 'Done' })
        $done[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        WaitText 'What would you like to do?'
        Capture "library-en-US-Light"
    }
    Write-Output "PASS: $Mode demo, $Language, $Theme"
    $script:completed = $true
}
finally {
    if ($script:demo -and -not $script:demo.HasExited) {
        $script:demo.CloseMainWindow() | Out-Null
        if (-not $script:demo.WaitForExit(3000)) { Stop-Process -Id $script:demo.Id -Force }
    }
    if ($script:completed) {
        $demoData = Join-Path $env:TEMP "PerenosDemo-$($script:demo.Id)"
        if (Test-Path -LiteralPath $demoData) { Remove-Item -LiteralPath $demoData -Recurse -Force }
    }
}
