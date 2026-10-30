param(
    [string]$Serial,
    [string]$Package = 'com.lostrealms.openworld.dev',
    [string]$AdbPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
)

$ErrorActionPreference = 'Stop'
if ($Package -notmatch '^[A-Za-z0-9_]+(\.[A-Za-z0-9_]+)+$') { throw 'Invalid Android package name.' }
if (-not (Test-Path -LiteralPath $AdbPath -PathType Leaf)) { throw 'Android platform tools were not found; supply AdbPath.' }

$deviceLines = @(& $AdbPath devices)
if ($LASTEXITCODE -ne 0) { throw 'Could not query Android devices.' }
$readyDevices = @($deviceLines | ForEach-Object {
    if ($_ -match '^(\S+)\s+device$') { $Matches[1] }
})
if ($Serial) {
    if ($readyDevices -notcontains $Serial) { throw 'The selected device is not connected and authorized.' }
} elseif ($readyDevices.Count -eq 1) {
    $Serial = $readyDevices[0]
} else {
    throw 'Connect and authorize one Android phone, or select an authorized phone with Serial.'
}

$appProcess = ((& $AdbPath -s $Serial shell pidof $Package) -join ' ').Trim()
if ($LASTEXITCODE -ne 0 -or $appProcess -notmatch '^\d+( \d+)*$') {
    throw 'Start the development game on the phone before capturing evidence.'
}
# Unity runs in the main application process; this limits log collection to this game.
$appProcessId = ($appProcess -split ' ')[0]
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$captureRoot = Join-Path $projectRoot ('Docs\DeviceTests\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $captureRoot | Out-Null

function Save-AdbReport([string]$Name, [string[]]$Arguments) {
    # Preserve permission-denied output on devices that restrict diagnostics.
    $ErrorActionPreference = 'Continue'
    $reportLines = @(& $AdbPath -s $Serial @Arguments 2>&1 | ForEach-Object { "$_" })
    $reportExit = $LASTEXITCODE
    $reportLines | Out-File -LiteralPath (Join-Path $captureRoot $Name) -Encoding utf8
    if ($reportExit -ne 0) { Write-Warning "$Name is incomplete (device command exit $reportExit)." }
}

Save-AdbReport 'model.txt' @('shell', 'getprop', 'ro.product.model')
Save-AdbReport 'android-version.txt' @('shell', 'getprop', 'ro.build.version.release')
Save-AdbReport 'memory.txt' @('shell', 'dumpsys', 'meminfo', $Package)
Save-AdbReport 'battery.txt' @('shell', 'dumpsys', 'battery')
Save-AdbReport 'thermal.txt' @('shell', 'dumpsys', 'thermalservice')
Save-AdbReport 'game-log.txt' @('logcat', '-d', '--pid', $appProcessId, '-v', 'threadtime')

$apkPath = Join-Path $projectRoot 'Builds\Android\LostRealmsOpenWorld-Development.apk'
$apkInfo = 'Local APK unavailable. Record the installed build manually.'
if (Test-Path -LiteralPath $apkPath -PathType Leaf) {
    $apk = Get-Item -LiteralPath $apkPath
    $apkInfo = "Local APK: $($apk.LastWriteTime.ToString('o')); $($apk.Length) bytes. Verify this is the installed build."
}
@(
    '# Android device evidence',
    '',
    "Captured: $((Get-Date).ToString('o'))",
    "Package: $Package; application process: $appProcessId",
    $apkInfo,
    '',
    'Graphics profile, route stage and elapsed play time: fill in.',
    'Observed animation, touch, audio or rendering problems: fill in.',
    '',
    'These are point-in-time device diagnostics, not a frame-rate benchmark.',
    'Use the Unity Profiler with the development player for CPU/GPU frame times.',
    'Some phones restrict thermal details. Logcat only contains the retained log buffer.',
    'Repeat at cold launch and after the 20-minute route. Compare Balanced and High separately.'
) | Out-File -LiteralPath (Join-Path $captureRoot 'README.md') -Encoding utf8
Write-Output "Device evidence saved to $captureRoot"
