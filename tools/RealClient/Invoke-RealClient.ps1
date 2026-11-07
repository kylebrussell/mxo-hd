<#
.SYNOPSIS
Drives the real Matrix Online 7.6005 client against a local mxo-hd server on Windows, so a full
login -> world -> in-game action check takes one command instead of a dozen manual clicks.

.DESCRIPTION
Steps run in this order, each only when its switch or parameter is given:
  -Relaunch     Kill a running client, wait until the server drops its world session, start the launcher.
  -Login        Log in (username + placeholder password; needs MXO_DEV_ANY_PASSWORD=1 on the server),
                then CONTINUE, select the character row, SELECT.
  -WaitWorld    Wait until the server log shows the client answered the self-spawn ("[MPM] Parsing 02 View").
  -Teleport     "?gotopos x y z" (the server multiplies by 100).
  -Chat         Type a chat line and press Enter.
  -DoubleClick  Double-click a point on the 1024x768 game screen.
  -Screenshot   Save a screenshot of the primary screen.
Exit code 0 when every requested step succeeded, 1 otherwise. A one-line JSON summary is printed last.

Prerequisites (see docs/CLIENT-SETUP-PLAN.md): the patched launcher.exe, autoexec.cfg pointing at the
local server, and the server running with MXO_DEV_ANY_PASSWORD=1 and MXO_PACKET_LOG=1.

.EXAMPLE
./Invoke-RealClient.ps1 -Relaunch -Login -WaitWorld -Teleport 1194,8,-150 -DoubleClick 540,660 -Screenshot vendor.png
#>
[CmdletBinding()]
param(
    [string]$ClientDir = (Join-Path $env:USERPROFILE 'Developer\mxo-client\MxO'),
    [string]$ServerLog = (Join-Path $PSScriptRoot '..\..\ServerLog.txt'),
    [string]$Username = 'loluser',
    [ValidateRange(0, 9)][int]$CharacterRow = 2,
    [switch]$Relaunch,
    [switch]$Login,
    [switch]$WaitWorld,
    [int]$WorldTimeoutSeconds = 180,
    [double[]]$Teleport,
    [string[]]$Chat,
    [int[]]$DoubleClick,
    [string]$Screenshot
)

$ErrorActionPreference = 'Stop'

$inputAssemblies = if ($PSVersionTable.PSEdition -eq 'Core') { @('System.Threading.Thread') } else { @() }
Add-Type -ReferencedAssemblies $inputAssemblies -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Threading;

public static class MxoInput
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int x, int y, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint f, UIntPtr e);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll")] static extern short VkKeyScan(char c);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);

    const uint LEFTDOWN = 0x02, LEFTUP = 0x04, KEYUP = 0x02;
    const byte VK_SHIFT = 0x10, VK_MENU = 0x12;

    static MxoInput() { SetProcessDPIAware(); }

    public static bool Focus(IntPtr h)
    {
        ShowWindow(h, 9);
        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYUP, UIntPtr.Zero);
        SetForegroundWindow(h);
        Thread.Sleep(150);
        return GetForegroundWindow() == h;
    }

    public static int[] Rect(IntPtr h)
    {
        RECT r; GetWindowRect(h, out r);
        return new[] { r.L, r.T, r.R, r.B };
    }

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y); Thread.Sleep(80);
        mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(40);
        mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(120);
    }

    public static void DoubleClick(int x, int y)
    {
        SetCursorPos(x, y); Thread.Sleep(80);
        for (int i = 0; i < 2; i++)
        {
            mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(30);
            mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(60);
        }
    }

    static void Key(byte vk, bool down)
    {
        byte scan = (byte)MapVirtualKey(vk, 0);
        keybd_event(vk, scan, down ? 0u : KEYUP, UIntPtr.Zero);
    }

    public static void Press(byte vk) { Key(vk, true); Thread.Sleep(30); Key(vk, false); Thread.Sleep(30); }

    // Key-by-key typing: the game's chat box ignores pasted text.
    public static void Type(string text)
    {
        foreach (char c in text)
        {
            short scan = VkKeyScan(c);
            byte vk = (byte)(scan & 0xff);
            bool shift = (scan & 0x100) != 0;
            if (shift) Key(VK_SHIFT, true);
            Press(vk);
            if (shift) Key(VK_SHIFT, false);
        }
    }

    public static int[] ScreenSize() { return new[] { GetSystemMetrics(0), GetSystemMetrics(1) }; }
}
'@

$summary = [ordered]@{}
$ok = $true

function Get-ClientProcess { Get-Process -Name matrix -ErrorAction SilentlyContinue | Select-Object -First 1 }

function Count-LogMatches([string]$pattern) {
    if (-not (Test-Path $ServerLog)) { return 0 }
    return @(Select-String -Path $ServerLog -Pattern $pattern -SimpleMatch).Count
}

function Focus-Client {
    $p = Get-ClientProcess
    if (-not $p) { throw 'The client (matrix.exe) is not running.' }
    for ($i = 0; $i -lt 5; $i++) {
        if ([MxoInput]::Focus($p.MainWindowHandle)) { return $p }
        Start-Sleep -Milliseconds 300
    }
    throw 'Could not bring the client window to the foreground.'
}

# Launcher layout: the window is 500x420 physical pixels; offsets measured from the 7.6005 launcher.
function Click-Launcher([int]$dx, [int]$dy) {
    $p = Focus-Client
    $r = [MxoInput]::Rect($p.MainWindowHandle)
    [MxoInput]::Click($r[0] + $dx, $r[1] + $dy)
}

if ($Relaunch) {
    $before = Count-LogMatches 'Removed inactive Client'
    $p = Get-ClientProcess
    if ($p) {
        Get-Process -Name matrix -ErrorAction SilentlyContinue | Stop-Process
        $t0 = Get-Date
        while (((Get-Date) - $t0).TotalSeconds -lt 120 -and (Count-LogMatches 'Removed inactive Client') -le $before) { Start-Sleep 2 }
        $summary.sessionDroppedAfterSeconds = [int]((Get-Date) - $t0).TotalSeconds
    }
    Start-Process -FilePath (Join-Path $ClientDir 'launcher.exe') -ArgumentList '-nopatch', '-noeula' -WorkingDirectory $ClientDir | Out-Null
    $t0 = Get-Date
    while (-not (Get-ClientProcess) -and ((Get-Date) - $t0).TotalSeconds -lt 30) { Start-Sleep 1 }
    Start-Sleep 6
    $summary.launcherStarted = [bool](Get-ClientProcess)
    if (-not $summary.launcherStarted) { $ok = $false }
}

if ($Login -and $ok) {
    $spawnsBefore = Count-LogMatches '[MPM] Parsing 02 View'
    $authBefore = Count-LogMatches "accepted $Username without checking"
    $marginBefore = Count-LogMatches 'Margin client Connected'
    Click-Launcher 247 172            # login console
    [MxoInput]::Type($Username); [MxoInput]::Press(0x0D); Start-Sleep 1
    [MxoInput]::Type('x'); [MxoInput]::Press(0x0D)   # placeholder; the dev server ignores passwords
    $t0 = Get-Date
    while (((Get-Date) - $t0).TotalSeconds -lt 30 -and (Count-LogMatches "accepted $Username without checking") -le $authBefore) { Start-Sleep 1 }
    $summary.authAccepted = (Count-LogMatches "accepted $Username without checking") -gt $authBefore
    if (-not $summary.authAccepted) { $ok = $false }
    # The patch-notes page loads over HTTP; CONTINUE does nothing until it has. CONTINUE sits where the
    # next page's SELECT is, so it is clicked once, after a generous wait.
    Start-Sleep 10
    Click-Launcher 411 341            # CONTINUE
    Start-Sleep 4
    # Character row (row 2 = third character). Click twice: the first click after the page
    # changes can be swallowed, and SELECT then logs in the default first row.
    $rowY = [int](82 + 17.5 * $CharacterRow)
    Click-Launcher 229 $rowY
    Start-Sleep 1
    Click-Launcher 229 $rowY
    Start-Sleep 1
    Click-Launcher 413 341            # SELECT
    $summary.loginSubmitted = $true
    $t0 = Get-Date
    while (((Get-Date) - $t0).TotalSeconds -lt 60 -and (Count-LogMatches 'Margin client Connected') -le $marginBefore) { Start-Sleep 1 }
    $summary.reachedMargin = (Count-LogMatches 'Margin client Connected') -gt $marginBefore
    if (-not $summary.reachedMargin) { $ok = $false }   # still in the launcher: a tool problem, not a stall
}

if ($WaitWorld -and $ok) {
    if (-not $spawnsBefore) { $spawnsBefore = Count-LogMatches '[MPM] Parsing 02 View' }
    $t0 = Get-Date
    while (((Get-Date) - $t0).TotalSeconds -lt $WorldTimeoutSeconds -and (Count-LogMatches '[MPM] Parsing 02 View') -le $spawnsBefore) { Start-Sleep 2 }
    $summary.inWorld = (Count-LogMatches '[MPM] Parsing 02 View') -gt $spawnsBefore
    $summary.worldWaitSeconds = [int]((Get-Date) - $t0).TotalSeconds
    $spawnLine = Select-String -Path $ServerLog -Pattern '\[SPAWN\] Queued self-spawn for charId (\d+)' | Select-Object -Last 1
    if ($spawnLine) { $summary.charId = [int]$spawnLine.Matches[0].Groups[1].Value }
    if (-not $summary.inWorld) { $ok = $false }
    Start-Sleep 8   # let the world finish streaming in before any input
}

function Send-Chat([string]$text) {
    Focus-Client | Out-Null
    [MxoInput]::Click(130, 567)       # chat input on the 1024x768 game screen
    [MxoInput]::Type($text)
    [MxoInput]::Press(0x0D)
}

if ($Teleport -and $ok) {
    if ($Teleport.Count -ne 3) { throw '-Teleport takes x,y,z' }
    Send-Chat ('?gotopos {0} {1} {2}' -f $Teleport[0], $Teleport[1], $Teleport[2])
    $summary.teleported = $true
    Start-Sleep 15
}

foreach ($line in @($Chat)) {
    if ($ok -and $line) { Send-Chat $line; Start-Sleep 2 }
}

if ($DoubleClick -and $ok) {
    if ($DoubleClick.Count -ne 2) { throw '-DoubleClick takes x,y' }
    Focus-Client | Out-Null
    [MxoInput]::DoubleClick($DoubleClick[0], $DoubleClick[1])
    $summary.doubleClicked = $true
    Start-Sleep 4
}

if ($Screenshot) {
    Add-Type -AssemblyName System.Drawing
    $path = [IO.Path]::GetFullPath($Screenshot)
    $size = [MxoInput]::ScreenSize()
    $bmp = New-Object System.Drawing.Bitmap $size[0], $size[1]
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    $summary.screenshot = $path
}

$summary.ok = $ok
$summary | ConvertTo-Json -Compress
if (-not $ok) { exit 1 }
