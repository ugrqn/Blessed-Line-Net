param([Parameter(Mandatory)][string] $Executable)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class QuotaWindowStyles
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    public static extern int GetWindowLong(IntPtr window, int index);
}
'@
$ui = [System.Windows.Automation.AutomationElement]
$scope = [System.Windows.Automation.TreeScope]
$invoke = [System.Windows.Automation.InvokePattern]::Pattern
$state = Join-Path $env:LOCALAPPDATA 'BlessedLine/state.json'
$path = (Resolve-Path $Executable).Path

function Control($container, [string] $id) {
    return $container.FindFirst($scope::Descendants, [System.Windows.Automation.PropertyCondition]::new($ui::AutomationIdProperty, $id))
}

function Wait-ForQuotaWindow($process, [string] $expected) {
    $owner = [System.Windows.Automation.PropertyCondition]::new($ui::ProcessIdProperty, $process.Id)
    for ($attempt = 0; $attempt -lt 32; $attempt++) {
        Start-Sleep -Milliseconds 500
        if ($process.HasExited) { throw 'BlessedLine exited before showing the quota window.' }
        foreach ($window in $ui::RootElement.FindAll($scope::Children, $owner)) {
            $heading = Control $window 'Heading'
            if ($heading -and $heading.Current.Name -eq $expected) {
                $handle = [IntPtr]$window.Current.NativeWindowHandle
                if (!([QuotaWindowStyles]::GetWindowLong($handle, -20) -band 8)) {
                    throw 'The quota window is missing the topmost flag.'
                }
                return $window
            }
        }
    }
    throw "No quota alert with heading '$expected' appeared."
}

foreach ($exhausted in @($false, $true)) {
    $used = if ($exhausted) { 500000000 } else { 360000000 }
    @{ Language = 'tr'; AutoMeasureApplications = $false
       Counter = @{ Name = 'Kota testi'; UsedBytes = $used; LimitBytes = 500000000; WarningBeforeBytes = 150000000 } } |
        ConvertTo-Json | Set-Content $state
    $process = Start-Process -FilePath $path -ArgumentList '--tray' -PassThru
    try {
        if ($exhausted) {
            $alert = Wait-ForQuotaWindow $process 'Limit doldu'
            (Control $alert 'ContinueButton').GetCurrentPattern($invoke).Invoke()
        } else {
            $alert = Wait-ForQuotaWindow $process 'Kotanız bitmek üzere'
            $remaining = (Control $alert 'Remaining').Current.Name
            if ($remaining -notmatch 'MB.*kaldı') { throw "Remaining quota is missing: $remaining" }
            $menu = Control $alert 'ExtensionMenu'
            $menu.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
            Start-Sleep -Milliseconds 300
            (Control $alert 'Add200Button').GetCurrentPattern($invoke).Invoke()
        }
        Start-Sleep -Seconds 1
        $saved = Get-Content $state -Raw | ConvertFrom-Json
        if ($exhausted -and !$saved.Counter.LimitDelivered) { throw 'Limit reached was not saved.' }
        if (!$exhausted -and $saved.Counter.LimitBytes -ne 700000000) { throw '+200 MB did not extend the same quota.' }
        $owner = [System.Windows.Automation.PropertyCondition]::new($ui::ProcessIdProperty, $process.Id)
        foreach ($window in $ui::RootElement.FindAll($scope::Children, $owner)) {
            if (Control $window 'Heading') { throw 'The quota window stayed open after the action.' }
        }
        Write-Host "Topmost quota window and action passed, exhausted=$exhausted."
    } finally {
        if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }
    }
}
