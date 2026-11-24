param([Parameter(Mandatory)][string] $Executable)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class TestInput {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
}
'@
$ui = [System.Windows.Automation.AutomationElement]
$scope = [System.Windows.Automation.TreeScope]
$invoke = [System.Windows.Automation.InvokePattern]::Pattern
$valuePattern = [System.Windows.Automation.ValuePattern]::Pattern
$exe = (Resolve-Path $Executable).Path
$folder = Join-Path $env:RUNNER_TEMP 'blessed-live-policy-test'
$profile = Join-Path $env:LOCALAPPDATA 'BlessedLine'
$statePath = Join-Path $profile 'state.json'
$fixture = Join-Path $folder 'BlessedFixture.exe'
$uri = 'https://raw.githubusercontent.com/microsoft/win32metadata/main/generation/WinSDK/RecompiledIdlHeaders/um/fwpmu.h'
$group = 'BlessedLine.AppControls'
$script:checks = 0

function Check([bool] $Condition, [string] $Name) {
    if (!$Condition) { throw "Failed scenario: $Name" }
    $script:checks++
    Write-Host "PASS $script:checks . $Name"
}

function ById($container, [string] $id) {
    return $container.FindFirst($scope::Descendants, [System.Windows.Automation.PropertyCondition]::new($ui::AutomationIdProperty, $id))
}

function ByName($container, [string] $name) {
    return $container.FindFirst($scope::Descendants, [System.Windows.Automation.PropertyCondition]::new($ui::NameProperty, $name))
}

function State {
    return Get-Content $statePath -Raw | ConvertFrom-Json -AsHashtable
}

function FixtureRule {
    $rules = (State).AppRules
    if ($rules) { return $rules[$fixture] }
}

function Wait-Check([scriptblock] $Predicate, [string] $Name, [int] $Count = 40) {
    for ($i = 0; $i -lt $Count; $i++) {
        if (& $Predicate) {
            Check $true $Name
            return
        }
        Start-Sleep -Milliseconds 400
    }
    $feedback = if ($script:window) { (ById $script:window 'Feedback').Current.Name }
    $measurement = if ($script:window) { (ById $script:window 'DetailStatus').Current.Name }
    $detailStatus = (ById $ui::RootElement 'ActionStatus').Current.Name
    throw "Timed out: $Name. Feedback: $feedback. Action: $detailStatus. Measurement: $measurement"
}

function Fixture-Request {
    $probe = Start-Process -FilePath $fixture -ArgumentList @('--silent','--fail','--max-time','6','--output','NUL',$uri) -PassThru
    if (!$probe.WaitForExit(10000)) { $probe.Kill(); throw 'Fixture did not exit.' }
    return $probe.ExitCode
}

function Start-FixtureTransfer {
    return Start-Process -FilePath $fixture -ArgumentList @('--silent','--show-error','--fail','--limit-rate','12k','--max-time','32',
        '--output','NUL',$uri) -PassThru
}

function Stop-Transfer($process) {
    if ($process -and !$process.HasExited) { $process.Kill(); $process.WaitForExit() }
}

function FixtureRow {
    $name = ByName $script:window 'BlessedFixture'
    $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
    while ($name -and $name.Current.ControlType -ne [System.Windows.Automation.ControlType]::DataItem) {
        $name = $walker.GetParent($name)
    }
    return $name
}

function MouseAt($element, [uint32] $down, [uint32] $up, [int] $clicks) {
    $bounds = $element.Current.BoundingRectangle
    $null = [TestInput]::SetForegroundWindow([IntPtr]$script:app.MainWindowHandle)
    $null = [TestInput]::SetCursorPos([int]($bounds.Left + 45), [int]($bounds.Top + $bounds.Height / 2))
    for ($i = 0; $i -lt $clicks; $i++) {
        [TestInput]::mouse_event($down, 0, 0, 0, [UIntPtr]::Zero)
        [TestInput]::mouse_event($up, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 70
    }
}

function HelperRam {
    $id = [Guid]::NewGuid().ToString('N')
    $pipe = [IO.Pipes.NamedPipeServerStream]::new("BlessedLine-$id",[IO.Pipes.PipeDirection]::InOut,1,[IO.Pipes.PipeTransmissionMode]::Byte,[IO.Pipes.PipeOptions]::Asynchronous)
    try {
        $worker = Start-Process $exe -ArgumentList @('--policy-only',$id,$PID) -PassThru
        if (!$pipe.WaitForConnectionAsync().Wait(16000)) { throw 'Policy helper failed to connect.' }
        $reader = [IO.StreamReader]::new($pipe, [Text.Encoding]::UTF8, $false, 1024, $true)
        $writer = [IO.StreamWriter]::new($pipe, [Text.UTF8Encoding]::new($false), 1024, $true)
        try {
            $writer.AutoFlush = $true
            $writer.WriteLine('{"Id":17,"Request":{"Operation":0,"Path":"C:\\Windows\\System32\\svchost.exe","Value":1}}')
            $valid = $false
            for ($i = 0; $i -lt 4; $i++) {
                $read = $reader.ReadLineAsync()
                if (!$read.Wait(4000) -or !$read.Result) { break }
                $reply = $read.Result | ConvertFrom-Json
                if ($reply.Policy.Id -eq 17) {
                    $valid = !$reply.Policy.Result.Success -and $reply.Policy.Result.Error -eq 'InvalidAppPolicy'
                    break
                }
            }
            Check $valid 'Authenticated policy helper rejects a shared Windows process'
            $max = 0
            for ($i = 0; $i -lt 25; $i++) {
                $script:app.Refresh()
                $worker.Refresh()
                $max = [Math]::Max($max, $script:app.WorkingSet64 + $worker.WorkingSet64)
                Start-Sleep -Milliseconds 100
            }
            Write-Host ("Peak sampled working set for UI + helper: {0:N2} MiB" -f ($max / 1MB))
            Check ($max -le 100MB) 'App and helper combined working set at or below 100 MiB'
        } finally { $writer.Dispose(); $reader.Dispose() }
    } finally {
        $pipe.Dispose()
        if ($worker -and !$worker.WaitForExit(5000)) { $worker.Kill(); throw 'Policy helper did not exit after disconnection.' }
    }
}

New-Item $folder -ItemType Directory -Force | Out-Null
New-Item $profile -ItemType Directory -Force | Out-Null
Copy-Item (Get-Command curl.exe).Source $fixture -Force
@{ Language = 'tr'; ColorizeUsage = $true; AutoMeasureApplications = $true } |
    ConvertTo-Json | Set-Content $statePath
$script:app = Start-Process -FilePath $exe -PassThru
$sentinel = 'BlessedLine-Test-Unrelated-Disabled'
try {
    Wait-Check { $script:app.Refresh(); $script:app.MainWindowHandle -ne 0 } 'Desktop app opens'
    $script:window = $ui::FromHandle($script:app.MainWindowHandle)
    Check ($null -ne (ById $window 'PingValue')) 'Internet ping is shown next to transfer speeds'
    Wait-Check { (ById $window 'DetailStatus').Current.Name -like 'Uygulama ölçümü açık*' } 'Automatic ETW measurement starts'
    Check ((Fixture-Request) -eq 0) 'Dedicated test executable reaches an Internet endpoint'
    $live = Start-FixtureTransfer
    Wait-Check { $null -ne (FixtureRow) } 'Live traffic adds the dedicated executable to the app list'

    MouseAt (FixtureRow) 2 4 2
    Wait-Check { $null -ne (ById $ui::RootElement 'AppQuotaInput') } 'Double-click opens application details'
    Check ($null -ne (ById $ui::RootElement 'RateSummary')) 'Live graph panel has download/upload readings'
    Stop-Transfer $live

    $upload = ById $ui::RootElement 'UploadCapInput'
    $upload.GetCurrentPattern($valuePattern).SetValue('1500')
    (ById $ui::RootElement 'ApplyUploadCap').GetCurrentPattern($invoke).Invoke()
    Wait-Check { (FixtureRule).UploadKbps -eq 1500 } 'Per-app upload cap is saved'
    $qos = @(Get-NetQosPolicy | Where-Object Name -Like "$group.*")
    Write-Host ('NetQoS: ' + ($qos | Select-Object Name, AppPathName, ThrottleRate | ConvertTo-Json -Compress))
    Check ($qos.Count -eq 1 -and $qos[0].AppPathName -ieq $fixture) 'Windows NetQoS installed the rule for the exact executable'

    $quota = ById $ui::RootElement 'AppQuotaInput'
    $quota.GetCurrentPattern($valuePattern).SetValue('0,03')
    (ById $ui::RootElement 'ApplyAppQuota').GetCurrentPattern($invoke).Invoke()
    Wait-Check { (FixtureRule).QuotaBytes -eq 30000 } 'Individual data quota saves decimal MB'
    $budgetTransfer = Start-FixtureTransfer
    Wait-Check { (FixtureRule).BudgetBlocked -eq $true } 'Attributed traffic triggers automatic per-app cutoff'
    Stop-Transfer $budgetTransfer
    $blocks = @(Get-NetFirewallRule -Group $group)
    Check ($blocks.Count -eq 2 -and @($blocks | Get-NetFirewallApplicationFilter | Where-Object Program -EQ $fixture).Count -eq 2) 'Windows Firewall records both directions for the chosen executable'
    Check ((Fixture-Request) -ne 0) 'Internet connection is actually blocked'
    Check ((Invoke-WebRequest $uri -TimeoutSec 12).StatusCode -eq 200) 'An unrelated process still reaches the Internet'

    $detail = (ById $ui::RootElement 'AppQuotaInput')
    $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
    while ($detail -and $detail.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window) {
        $detail = $walker.GetParent($detail)
    }
    $detail.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    $quick = ByName (FixtureRow) 'İnterneti geri aç'
    $quick.GetCurrentPattern($invoke).Invoke()
    Wait-Check { (FixtureRule).BudgetBlocked -ne $true -and (FixtureRule).QuotaBytes -eq 0 } 'Quick icon restores connectivity and clears the reached quota'
    Check ((Fixture-Request) -eq 0) 'Restored application can connect again'

    MouseAt (FixtureRow) 8 16 1
    Wait-Check { $null -ne (ByName $ui::RootElement 'Favoriye ekle') } 'Right click exposes the favorite action'
    (ByName $ui::RootElement 'Favoriye ekle').GetCurrentPattern($invoke).Invoke()
    Wait-Check { (State).FavoriteApps -contains $fixture } 'Favorite path is persisted'
    $firstCell = FixtureRow
    $gridItem = $firstCell.FindFirst($scope::Descendants, [System.Windows.Automation.PropertyCondition]::new(
        $ui::IsGridItemPatternAvailableProperty, $true))
    Check ($gridItem.GetCurrentPattern([System.Windows.Automation.GridItemPattern]::Pattern).Current.Row -eq 0) 'Favorite is at the top of the grid'

    $quick = ByName (FixtureRow) 'İnterneti kes'
    $quick.GetCurrentPattern($invoke).Invoke()
    Wait-Check { (FixtureRule).Blocked -eq $true } 'Quick icon blocks manually without opening details'
    New-NetFirewallRule -Name $sentinel -DisplayName $sentinel -Enabled False -Direction Outbound -Action Block | Out-Null
    (ById $window 'ClearPoliciesButton').GetCurrentPattern($invoke).Invoke()
    Wait-Check { (State).AppRules.Count -eq 0 } 'Clear all removes the app state'
    Check (!(Get-NetFirewallRule -Group $group -ErrorAction SilentlyContinue) -and
           !(Get-NetQosPolicy | Where-Object Name -Like "$group.*")) 'Clear all removes firewall and QoS rules'
    Check ($null -ne (Get-NetFirewallRule -Name $sentinel)) 'Unrelated firewall rule is untouched'

    (ById $window 'SettingsButton').GetCurrentPattern($invoke).Invoke()
    Start-Sleep -Milliseconds 350
    (ById $ui::RootElement 'UsageColors').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Wait-Check { (State).ColorizeUsage -eq $false } 'Traffic colors can be disabled'
    (ById $window 'SettingsButton').GetCurrentPattern($invoke).Invoke()

    (ById $window 'WifiInfoButton').GetCurrentPattern($invoke).Invoke()
    Wait-Check { $null -ne (ById $ui::RootElement 'WifiStatus') } 'Wi-Fi diagnostics window opens'
    Check ($null -ne (ById $ui::RootElement 'WifiScan')) 'Nearby scan control is present'
    HelperRam
    Check ($script:checks -ge 10) 'At least ten Windows scenarios were exercised'
    Write-Host ("Authenticode status: " + (Get-AuthenticodeSignature $exe).Status)
} finally {
    Stop-Transfer $live
    Stop-Transfer $budgetTransfer
    if ($script:app -and !$script:app.HasExited) { $script:app.Kill(); $script:app.WaitForExit() }
    Get-NetFirewallRule -Group $group -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
    Remove-NetFirewallRule -Name $sentinel -ErrorAction SilentlyContinue
    Get-NetQosPolicy -ErrorAction SilentlyContinue | Where-Object Name -Like "$group.*" |
        ForEach-Object { Remove-NetQosPolicy -Name $_.Name -Confirm:$false -ErrorAction SilentlyContinue }
}
