param(
    [Parameter(Mandatory)][string] $Executable,
    [string] $ReferenceArchive
)

$ErrorActionPreference = 'Stop'
$executablePath = (Resolve-Path $Executable).Path
$profileDirectory = Join-Path $env:LOCALAPPDATA 'BlessedLine'
$statePath = Join-Path $profileDirectory 'state.json'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$expected = '"' + $executablePath + '" --tray'

function Wait-For([scriptblock] $condition, [string] $message) {
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if (& $condition) { return }
        Start-Sleep -Milliseconds 500
    }
    throw $message
}

function Memory-Snapshot([System.Diagnostics.Process] $process) {
    $working = @()
    $commit = @()
    for ($i = 0; $i -lt 3; $i++) {
        Start-Sleep -Seconds 1
        $process.Refresh()
        $working += $process.WorkingSet64
        $commit += $process.PrivateMemorySize64
    }
    @{ Working = ($working | Sort-Object)[1]; Commit = ($commit | Sort-Object)[1] }
}

function Print-Memory([string] $label, $measurement) {
    Write-Host ("{0}: {1:N1} MiB working set, {2:N1} MiB private bytes" -f
        $label, ($measurement.Working / 1MB), ($measurement.Commit / 1MB))
}

function Stop-Application($process) {
    if ($process -and !$process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }
}

New-Item $profileDirectory -ItemType Directory -Force | Out-Null
@{ Counter = @{ Name = 'Toplam sayaç'; UsedBytes = 0; LimitBytes = 1000000000 } } |
    ConvertTo-Json | Set-Content $statePath

if ($ReferenceArchive) {
    $referenceFolder = Join-Path $env:RUNNER_TEMP 'blessed-reference'
    & 7z x $ReferenceArchive "-o$referenceFolder" -y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not extract the 0.5.0 reference package.' }
    $reference = Start-Process -FilePath (Join-Path $referenceFolder 'BlessedLine/BlessedLine.exe') -ArgumentList '--tray' -PassThru
    try {
        Start-Sleep -Seconds 6
        $reference.Refresh()
        if ($reference.HasExited -or $reference.MainWindowHandle -ne 0) { throw 'Reference tray start failed.' }
        $oldMemory = Memory-Snapshot $reference
        Print-Memory '0.5.0 started in the tray with default measurement' $oldMemory
    } finally {
        Stop-Application $reference
    }
}

$resident = Start-Process -FilePath $executablePath -ArgumentList '--tray' -PassThru
try {
    Wait-For { (Get-ItemProperty -LiteralPath $runKey -ErrorAction SilentlyContinue).BlessedLine -eq $expected } 'Windows startup was not registered for the current user.'
    Start-Sleep -Seconds 5
    $resident.Refresh()
    if ($resident.HasExited -or $resident.MainWindowHandle -ne 0) { throw 'Tray startup did not remain in the background.' }

    $newMemory = Memory-Snapshot $resident
    Print-Memory '0.6.0 tray with automatic application measurement' $newMemory

    $duplicate = Start-Process -FilePath $executablePath -ArgumentList '--tray' -PassThru
    if (!$duplicate.WaitForExit(10000)) { Stop-Application $duplicate; throw 'A duplicate logon run did not exit.' }
    $resident.Refresh()
    if ($resident.MainWindowHandle -ne 0) { throw 'A duplicate logon run opened the window.' }

    $before = (Get-Content $statePath -Raw | ConvertFrom-Json).Counter.UsedBytes
    $null = Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/microsoft/win32metadata/main/generation/WinSDK/RecompiledIdlHeaders/um/fwpmu.h'
    Wait-For { (Get-Content $statePath -Raw | ConvertFrom-Json).Counter.UsedBytes -gt $before } 'The tray process did not save aggregate network traffic.'

    $manual = Start-Process -FilePath $executablePath -PassThru
    if (!$manual.WaitForExit(10000)) { Stop-Application $manual; throw 'Manual run did not hand off.' }
    Wait-For { $resident.Refresh(); $resident.MainWindowHandle -ne 0 } 'Manual run did not open the existing app.'
    Write-Host 'Startup registration, background counting, duplicate startup, and reopening the existing process passed.'
} finally {
    Stop-Application $resident
    Remove-ItemProperty -LiteralPath $runKey -Name BlessedLine -ErrorAction SilentlyContinue
}

@{ AutoMeasureApplications = $false } | ConvertTo-Json | Set-Content $statePath
$lightweight = Start-Process -FilePath $executablePath -ArgumentList '--tray' -PassThru
try {
    Start-Sleep -Seconds 6
    $lightweight.Refresh()
    if ($lightweight.HasExited -or $lightweight.MainWindowHandle -ne 0) { throw 'Lightweight tray start failed.' }
    Print-Memory '0.6.0 tray with application measurement disabled' (Memory-Snapshot $lightweight)
} finally {
    Stop-Application $lightweight
    Remove-ItemProperty -LiteralPath $runKey -Name BlessedLine -ErrorAction SilentlyContinue
}
