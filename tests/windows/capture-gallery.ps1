param(
    [Parameter(Mandatory)][string] $Executable,
    [Parameter(Mandatory)][string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Use a fresh GitHub-hosted Windows runner for gallery capture.'
}

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing.Common, System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class GalleryDesktop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Bounds { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Bounds bounds, int size);
}
'@

$ui = [System.Windows.Automation.AutomationElement]
$scope = [System.Windows.Automation.TreeScope]
$invoke = [System.Windows.Automation.InvokePattern]::Pattern
$walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
$exe = (Resolve-Path $Executable).Path
$stateDirectory = Join-Path $env:LOCALAPPDATA 'BlessedLine'
$statePath = Join-Path $stateDirectory 'state.json'
$null = New-Item $stateDirectory, $OutputDirectory -ItemType Directory -Force

function Control($window, [string] $id) {
    $window.FindFirst($scope::Descendants, [System.Windows.Automation.PropertyCondition]::new($ui::AutomationIdProperty, $id))
}

function Wait-ForWindow($app, [string] $controlId) {
    $owner = [System.Windows.Automation.PropertyCondition]::new($ui::ProcessIdProperty, $app.Id)
    for ($i = 0; $i -lt 40; $i++) {
        if ($app.HasExited) { throw "BlessedLine exited with code $($app.ExitCode)." }
        foreach ($window in $ui::RootElement.FindAll($scope::Children, $owner)) {
            $element = Control $window $controlId
            if (!$element -or $element.Current.IsOffscreen) { continue }
            while ($element -and ($element.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window -or
                                 !$element.Current.NativeWindowHandle)) {
                $element = $walker.GetParent($element)
            }
            if ($element -and $element.Current.ProcessId -eq $app.Id) { return $element }
        }
        Start-Sleep -Milliseconds 350
    }
    throw "BlessedLine window with '$controlId' did not appear."
}

function Show-Window($window) {
    $handle = [IntPtr]$window.Current.NativeWindowHandle
    if (!$handle -or ![GalleryDesktop]::SetWindowPos($handle, [IntPtr](-1), 0, 0, 0, 0, 0x53)) {
        throw 'Could not bring the BlessedLine window to the top of the desktop.'
    }
    $null = [GalleryDesktop]::SetForegroundWindow($handle)
}

function Save-Window($window, [string] $name) {
    Show-Window $window
    Start-Sleep -Milliseconds 350
    $handle = [IntPtr]$window.Current.NativeWindowHandle
    $bounds = [GalleryDesktop+Bounds]::new()
    if ([GalleryDesktop]::DwmGetWindowAttribute($handle, 9, [ref]$bounds, 16) -ne 0) {
        throw "Could not determine the visible bounds for $name."
    }
    $width = $bounds.Right - $bounds.Left
    $height = $bounds.Bottom - $bounds.Top
    $crop = [System.Drawing.Rectangle]::new($bounds.Left, $bounds.Top, $width, $height)
    if ($width -lt 180 -or $height -lt 75 -or
        !([System.Windows.Forms.Screen]::AllScreens | Where-Object { $_.Bounds.Contains($crop) })) {
        throw "Invalid capture bounds for $name."
    }

    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($crop.Location, [System.Drawing.Point]::Empty, $crop.Size)
        $bitmap.Save((Join-Path $OutputDirectory "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "Captured $name at ${width}x${height}."
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Stop-ProcessIfRunning($process) {
    if ($process -and !$process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }
}

function Start-Example([string] $theme, [long] $used, [switch] $tray, [switch] $measure) {
    $favorites = @(
        (Join-Path $env:WINDIR 'System32/curl.exe')
        (Join-Path $env:WINDIR 'System32/notepad.exe')
        (Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe')
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe')
    ) | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { (Resolve-Path -LiteralPath $_).Path }
    @{
        Language = 'tr'
        Theme = $theme
        AutoMeasureApplications = $measure.IsPresent
        StartWithWindows = $tray.IsPresent
        MainDetails = $true
        FavoriteApps = @($favorites)
        Counter = @{ Name = 'İnternet kotası'; UsedBytes = $used; LimitBytes = 1000000000; WarningBeforeBytes = 100000000 }
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statePath -Encoding utf8NoBOM

    if ($tray) { return Start-Process -FilePath $exe -ArgumentList '--tray' -PassThru }
    return Start-Process -FilePath $exe -PassThru
}

function Start-ExampleDownload {
    $curl = Join-Path $env:WINDIR 'System32/curl.exe'
    return Start-Process -FilePath $curl -ArgumentList @(
        '--silent','--fail','--limit-rate','8k','--max-time','35','--output','NUL',
        'https://raw.githubusercontent.com/microsoft/win32metadata/main/generation/WinSDK/RecompiledIdlHeaders/um/fwpmu.h'
    ) -WindowStyle Hidden -PassThru
}

$aero = Start-Example -theme Win7 -used 642000000 -measure
$transfer = $null
try {
    $main = Wait-ForWindow $aero 'MiniButton'
    $transfer = Start-ExampleDownload
    Start-Sleep -Seconds 3
    Save-Window $main 'aero-dashboard'

    (Control $main 'MiniButton').GetCurrentPattern($invoke).Invoke()
    $compact = Wait-ForWindow $aero 'MiniQuotaButton'
    Save-Window $compact 'mini-panel'
    (Control $compact 'MiniButton').GetCurrentPattern($invoke).Invoke()

    $main = Wait-ForWindow $aero 'Applications'
    Show-Window $main
    $curl = $main.FindFirst($scope::Descendants, [System.Windows.Automation.PropertyCondition]::new($ui::NameProperty, 'curl'))
    if (!$curl -or $curl.Current.IsOffscreen) { throw 'The curl example row is missing.' }
    $cell = $curl.Current.BoundingRectangle
    $null = [GalleryDesktop]::SetCursorPos([int]($cell.Left + 8), [int]($cell.Top + $cell.Height / 2))
    for ($click = 0; $click -lt 2; $click++) {
        [GalleryDesktop]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
        [GalleryDesktop]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 70
    }
    $detail = Wait-ForWindow $aero 'AppQuotaInput'
    Stop-ProcessIfRunning $transfer
    $transfer = Start-ExampleDownload
    Start-Sleep -Seconds 3
    Save-Window $detail 'application-details'
} finally {
    Stop-ProcessIfRunning $transfer
    Stop-ProcessIfRunning $aero
}

$classic = Start-Example -theme XP -used 642000000
try {
    Save-Window (Wait-ForWindow $classic 'MiniButton') 'xp-dashboard'
} finally {
    Stop-ProcessIfRunning $classic
}

$warning = Start-Example -theme Win7 -used 938000000 -tray
try {
    Save-Window (Wait-ForWindow $warning 'ContinueButton') 'quota-warning'
} finally {
    Stop-ProcessIfRunning $warning
}
