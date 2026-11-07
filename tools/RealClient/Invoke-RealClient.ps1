<#
.SYNOPSIS
Drives the real Matrix Online 7.6005 client against a local mxo-hd server on Windows, so a full
login -> world -> in-game action check takes one command instead of a dozen manual clicks.

.DESCRIPTION
Steps run in this order, each only when its switch or parameter is given:
  -Relaunch     Kill a running client, wait until the server drops its world session, start the launcher.
  -NewInstance  Start another launcher next to the running client(s), for a second player.
  -ClientPid    Drive this client process (from a previous run's clientPid) when several are running.
  -SetPosition / -SetRotation  With -Handle: set the character's saved position/facing before logging in.
  -Login        Log in (username + placeholder password; needs MXO_DEV_ANY_PASSWORD=1 on the server),
                then CONTINUE, select the character row, SELECT.
  -WaitWorld    Wait until the server log shows the client answered the self-spawn (RPC 80 be, header 190).
  -Teleport     "?gotopos x y z" (the server multiplies by 100).
  -Chat         Type a chat line and press Enter.
  -Walk         Hold movement keys in turn, KEY:seconds each, e.g. -Walk W:2,A:0.5,Space:0.2.
  -DoubleClick  Double-click a point on the 1024x768 game screen.
  -Click        Click a point on the game screen once (UI buttons), after any double-click.
  -Screenshot   Save a screenshot of the primary screen.
Exit code 0 when every requested step succeeded, 1 otherwise. A one-line JSON summary is printed last.

Prerequisites (see docs/CLIENT-SETUP-PLAN.md): the patched launcher.exe, autoexec.cfg pointing at the
local server, and the server running with MXO_DEV_ANY_PASSWORD=1 and MXO_PACKET_LOG=1.

.EXAMPLE
./Invoke-RealClient.ps1 -Relaunch -Handle TheNeo -SetPosition 111534,695,-7188.31 -SetRotation 31 -Login -WaitWorld -Teleport 1194,8,-150 -DoubleClick 540,660 -CheckVendorWindow
#>
[CmdletBinding()]
param(
    [string]$ClientDir = (Join-Path $env:USERPROFILE 'Developer\mxo-client\MxO'),
    [string]$ServerLog = (Join-Path $PSScriptRoot '..\..\ServerLog.txt'),
    [string]$Username = 'loluser',
    [ValidateRange(0, 9)][int]$CharacterRow = 2,
    # Before logging in, set this character's saved position (x,y,z in database units) and/or facing
    # (0-255). Positions are now saved as the player moves, so a scenario whose screen coordinates were
    # measured from a known start (like the vendor click) needs that start restored. Needs -Handle.
    [double[]]$SetPosition,
    [ValidateRange(0, 255)][int]$SetRotation = -1,
    [ValidatePattern('^[A-Za-z0-9_]+$')][string]$Handle,
    [string]$DbContainer = 'mxo-hd-mysql',
    [switch]$Relaunch,
    # Drive one client of several (a second player). -NewInstance starts another launcher without
    # closing the running client(s); the summary's clientPid is the one to pass on later runs.
    [int]$ClientPid,
    [switch]$NewInstance,
    [switch]$Login,
    [switch]$StopAtSelect,
    [switch]$WaitWorld,
    [int]$WorldTimeoutSeconds = 180,
    [double[]]$Teleport,
    [string[]]$Chat,
    [string[]]$Walk,
    [int[]]$DoubleClick,
    [int[]]$Click,
    [int[]]$LauncherClick,
    [int[]]$RightClick,
    [switch]$CheckVendorWindow,
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
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint f, UIntPtr e);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);

    const uint KEYUP = 0x02;
    const byte VK_MENU = 0x12;   // an Alt tap lets SetForegroundWindow succeed from a background process

    // Work in physical pixels regardless of how the hosting process was started. SetProcessDPIAware
    // alone was not enough: run as a background job, coordinates came back DPI-scaled and every
    // launcher click landed in the wrong place.
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
    static readonly IntPtr PerMonitorAwareV2 = new IntPtr(-4);
    static void Physical() { try { SetThreadDpiAwarenessContext(PerMonitorAwareV2); } catch { } }

    static MxoInput() { SetProcessDPIAware(); Physical(); }

    public static bool Focus(IntPtr h)
    {
        Physical();
        ShowWindow(h, 9);
        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYUP, UIntPtr.Zero);
        SetForegroundWindow(h);
        Thread.Sleep(150);
        return GetForegroundWindow() == h;
    }

    public static int[] Rect(IntPtr h)
    {
        Physical();
        RECT r; GetWindowRect(h, out r);
        return new[] { r.L, r.T, r.R, r.B };
    }

    public static int[] ScreenSize() { Physical(); return new[] { GetSystemMetrics(0), GetSystemMetrics(1) }; }

    // Process.MainWindowHandle is unreliable for the launcher: it can point at a tooltip popup
    // ("This column indicates your character's name..."). Pick the largest visible top-level window
    // of the process titled "The Matrix Online".
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);

    public static IntPtr FindClientWindow(uint pid)
    {
        Physical();
        IntPtr best = IntPtr.Zero; long bestArea = -1;
        EnumWindows((h, p) =>
        {
            uint owner; GetWindowThreadProcessId(h, out owner);
            if (owner != pid || !IsWindowVisible(h)) return true;
            var title = new System.Text.StringBuilder(256); GetWindowText(h, title, 256);
            if (title.ToString() != "The Matrix Online") return true;
            RECT r; GetWindowRect(h, out r);
            long area = (long)(r.R - r.L) * (r.B - r.T);
            if (area > bestArea) { bestArea = area; best = h; }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    // ---- Window-message input for the launcher -------------------------------------------------
    // Posted straight to the launcher's windows, so it works whatever is in front and can never
    // type into another application.
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr ChildWindowFromPointEx(IntPtr parent, POINT pt, uint flags);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref POINT pt);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT pt);
    const uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_CHAR = 0x102;
    const uint WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202;
    const uint CWP_SKIPINVISIBLE = 1;

    static IntPtr MakeLParam(int x, int y) { return (IntPtr)((y << 16) | (x & 0xffff)); }

    // Deepest visible child of top under the given offset from top's top-left corner.
    static IntPtr ChildAt(IntPtr top, int dx, int dy, out POINT client)
    {
        RECT r; GetWindowRect(top, out r);
        POINT screen = new POINT { X = r.L + dx, Y = r.T + dy };
        IntPtr current = top;
        while (true)
        {
            POINT p = screen;
            ScreenToClient(current, ref p);
            IntPtr child = ChildWindowFromPointEx(current, p, CWP_SKIPINVISIBLE);
            if (child == IntPtr.Zero || child == current) { client = p; return current; }
            current = child;
        }
    }

    public static void PostClick(IntPtr top, int dx, int dy)
    {
        Physical();
        POINT p; IntPtr target = ChildAt(top, dx, dy, out p);
        PostMessage(target, WM_LBUTTONDOWN, (IntPtr)1, MakeLParam(p.X, p.Y)); Thread.Sleep(50);
        PostMessage(target, WM_LBUTTONUP, IntPtr.Zero, MakeLParam(p.X, p.Y)); Thread.Sleep(150);
    }

    public static void PostDoubleClick(IntPtr top, int dx, int dy)
    {
        Physical();
        const uint WM_LBUTTONDBLCLK = 0x203;
        POINT p; IntPtr target = ChildAt(top, dx, dy, out p);
        // The game picks the object under the real cursor, not the message coordinates.
        POINT screen = p; ClientToScreen(target, ref screen); SetCursorPos(screen.X, screen.Y); Thread.Sleep(80);
        IntPtr lp = MakeLParam(p.X, p.Y);
        PostMessage(target, WM_LBUTTONDOWN, (IntPtr)1, lp); Thread.Sleep(30);
        PostMessage(target, WM_LBUTTONUP, IntPtr.Zero, lp); Thread.Sleep(30);
        PostMessage(target, WM_LBUTTONDBLCLK, (IntPtr)1, lp); Thread.Sleep(30);
        PostMessage(target, WM_LBUTTONUP, IntPtr.Zero, lp); Thread.Sleep(150);
    }

    // A single in-game click (UI buttons), with the real cursor moved there first like PostDoubleClick.
    public static void PostGameClick(IntPtr top, int dx, int dy)
    {
        Physical();
        POINT p; IntPtr target = ChildAt(top, dx, dy, out p);
        POINT screen = p; ClientToScreen(target, ref screen); SetCursorPos(screen.X, screen.Y); Thread.Sleep(80);
        IntPtr lp = MakeLParam(p.X, p.Y);
        PostMessage(target, WM_LBUTTONDOWN, (IntPtr)1, lp); Thread.Sleep(50);
        PostMessage(target, WM_LBUTTONUP, IntPtr.Zero, lp); Thread.Sleep(150);
    }

    // A single in-game right-click (radial menus), cursor moved there first.
    public static void PostGameRightClick(IntPtr top, int dx, int dy)
    {
        Physical();
        const uint WM_RBUTTONDOWN = 0x204, WM_RBUTTONUP = 0x205;
        POINT p; IntPtr target = ChildAt(top, dx, dy, out p);
        POINT screen = p; ClientToScreen(target, ref screen); SetCursorPos(screen.X, screen.Y); Thread.Sleep(80);
        IntPtr lp = MakeLParam(p.X, p.Y);
        PostMessage(target, WM_RBUTTONDOWN, (IntPtr)2, lp); Thread.Sleep(50);
        PostMessage(target, WM_RBUTTONUP, IntPtr.Zero, lp); Thread.Sleep(150);
    }

    public static void PostKey(IntPtr top, byte vk)
    {
        uint scan = MapVirtualKey(vk, 0);
        PostMessage(top, WM_KEYDOWN, (IntPtr)vk, (IntPtr)(1 | (scan << 16))); Thread.Sleep(30);
        PostMessage(top, WM_KEYUP, (IntPtr)vk, (IntPtr)(1 | (scan << 16) | 0xC0000000)); Thread.Sleep(30);
    }

    // Hold a key: key-down, auto-repeat downs every 50 ms like a real keyboard, then key-up.
    public static void PostKeyHold(IntPtr top, byte vk, int ms)
    {
        uint scan = MapVirtualKey(vk, 0);
        PostMessage(top, WM_KEYDOWN, (IntPtr)vk, (IntPtr)(1 | (scan << 16)));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            Thread.Sleep(50);
            PostMessage(top, WM_KEYDOWN, (IntPtr)vk, (IntPtr)(1 | (scan << 16) | 0x40000000));
        }
        PostMessage(top, WM_KEYUP, (IntPtr)vk, (IntPtr)(1 | (scan << 16) | 0xC0000000));
    }

    public static void PostText(IntPtr top, string text)
    {
        foreach (char c in text) { PostMessage(top, WM_CHAR, (IntPtr)c, (IntPtr)1); Thread.Sleep(30); }
    }
}
'@

$summary = [ordered]@{}
$ok = $true

function Get-ClientProcess {
    if ($ClientPid) { return Get-Process -Id $ClientPid -ErrorAction SilentlyContinue }
    Get-Process -Name matrix -ErrorAction SilentlyContinue | Select-Object -First 1
}

function Count-LogMatches([string]$pattern) {
    if (-not (Test-Path $ServerLog)) { return 0 }
    return @(Select-String -Path $ServerLog -Pattern $pattern -SimpleMatch).Count
}

# The client answers the self-spawn with RPC 80 be (logged as header 190), sometimes together with a
# sub-view 02 update ("[MPM] Parsing 02 View"). Count either; only the increase matters.
function Count-WorldReplies {
    if (-not (Test-Path $ServerLog)) { return 0 }
    return @(Select-String -Path $ServerLog -Pattern '\[MPM\] Parsing 02 View|RPC \(Header 190 ').Count
}

# All input is posted as window messages to the client's own window, so it works whatever is in
# front and keystrokes can never land in another application. The game picks objects under the real
# cursor, so in-game double-clicks also move the cursor (see MxoInput.PostDoubleClick).
function Get-ClientWindow {
    $p = Get-ClientProcess
    if (-not $p) { throw 'The client (matrix.exe) is not running.' }
    # No focus call here: Focus() taps Alt, which can put the launcher into menu mode.
    $h = [MxoInput]::FindClientWindow([uint32]$p.Id)
    if ($h -eq [IntPtr]::Zero) { throw 'No visible "The Matrix Online" window found for the client process.' }
    return $h
}

# Launcher layout: the window is 500x420 physical pixels; offsets measured from the 7.6005 launcher.
# A second launcher started while another client runs can come up DPI-scaled (1000x840); these
# offsets don't fit that one.
function Click-Launcher([int]$dx, [int]$dy) {
    [MxoInput]::PostClick((Get-ClientWindow), $dx, $dy)
}

function Type-Client([string]$text) { [MxoInput]::PostText((Get-ClientWindow), $text) }
function Press-Client([byte]$vk) { [MxoInput]::PostKey((Get-ClientWindow), $vk) }

if ($Relaunch -or $NewInstance) {
    if ($Relaunch) {
        $before = Count-LogMatches 'Removed inactive Client'
        $p = Get-ClientProcess
        if ($p) {
            # Only the targeted client: with -ClientPid another instance (a second player) keeps running.
            if ($ClientPid) { $p | Stop-Process } else { Get-Process -Name matrix -ErrorAction SilentlyContinue | Stop-Process }
            $t0 = Get-Date
            # The server drops a dead world session after ~30 s; there is nothing to wait for if the
            # killed client never got past the launcher, so cap the wait.
            while (((Get-Date) - $t0).TotalSeconds -lt 45 -and (Count-LogMatches 'Removed inactive Client') -le $before) { Start-Sleep 2 }
            $summary.sessionDroppedAfterSeconds = [int]((Get-Date) - $t0).TotalSeconds
        }
    }
    $existing = @(Get-Process -Name matrix -ErrorAction SilentlyContinue | ForEach-Object Id)
    Start-Process -FilePath (Join-Path $ClientDir 'launcher.exe') -ArgumentList '-nopatch', '-noeula' -WorkingDirectory $ClientDir | Out-Null
    $t0 = Get-Date
    $new = $null
    while (-not $new -and ((Get-Date) - $t0).TotalSeconds -lt 30) {
        Start-Sleep 1
        $new = Get-Process -Name matrix -ErrorAction SilentlyContinue | Where-Object { $existing -notcontains $_.Id } | Select-Object -First 1
    }
    Start-Sleep 6
    $summary.launcherStarted = [bool]$new
    if ($new) {
        # Every later step in this run drives the instance just started.
        $ClientPid = $new.Id
        $summary.clientPid = $new.Id
    } else { $ok = $false }
}

if (($SetPosition -or $SetRotation -ge 0) -and $ok) {
    if (-not $Handle) { throw '-SetPosition/-SetRotation need -Handle' }
    $sets = @()
    if ($SetPosition) {
        if ($SetPosition.Count -ne 3) { throw '-SetPosition takes x,y,z' }
        $inv = [Globalization.CultureInfo]::InvariantCulture
        $sets += 'x={0},y={1},z={2}' -f $SetPosition[0].ToString($inv), $SetPosition[1].ToString($inv), $SetPosition[2].ToString($inv)
    }
    if ($SetRotation -ge 0) { $sets += "rotation=$SetRotation" }
    # Runs after -Relaunch has waited for the old session to drop, so its final save can't overwrite
    # this. The container's own environment supplies the credentials.
    $sql = "update characters set $($sets -join ',') where handle='$Handle'"
    docker exec $DbContainer sh -c "mysql -u`"`$MYSQL_USER`" -p`"`$MYSQL_PASSWORD`" `"`$MYSQL_DATABASE`" -e `"$sql`"" 2>$null
    $summary.characterReset = ($LASTEXITCODE -eq 0)
    if (-not $summary.characterReset) { $ok = $false }
}

if ($Login -and $ok) {
    $spawnsBefore = Count-WorldReplies
    $authBefore = Count-LogMatches "accepted $Username without checking"
    $marginBefore = Count-LogMatches 'Margin client Connected'
    Click-Launcher 247 172            # login console
    Type-Client $Username; Press-Client 0x0D; Start-Sleep 1
    Type-Client 'x'; Press-Client 0x0D   # placeholder; the dev server ignores passwords
    $t0 = Get-Date
    while (((Get-Date) - $t0).TotalSeconds -lt 30 -and (Count-LogMatches "accepted $Username without checking") -le $authBefore) { Start-Sleep 1 }
    $summary.authAccepted = (Count-LogMatches "accepted $Username without checking") -gt $authBefore
    # Not a failure yet: the auth line can reach the log late; reaching the margin server settles it.
    # Patch-notes page: Enter acts as CONTINUE. Clicking CONTINUE was unreliable (the embedded web page
    # can swallow the click), and it sits where the next page's SELECT is, so a retry click is unsafe.
    Start-Sleep 8
    Press-Client 0x0D
    Start-Sleep 4
}

if ($Login -and $ok -and $StopAtSelect) {
    $summary.stoppedAtSelect = $true   # leave the character list on screen (with -Screenshot to read it)
    $summary.authAccepted = (Count-LogMatches "accepted $Username without checking") -gt $authBefore
    if (-not $summary.authAccepted) { $ok = $false }
} elseif ($Login -and $ok) {
    $rowY = [int](93 + 14.5 * $CharacterRow)   # row 2 (TheNeo for loluser) measured at y=122
    Click-Launcher 229 $rowY
    Start-Sleep 1
    Click-Launcher 413 341            # SELECT
    $summary.loginSubmitted = $true
    $t0 = Get-Date
    while (((Get-Date) - $t0).TotalSeconds -lt 60 -and (Count-LogMatches 'Margin client Connected') -le $marginBefore) { Start-Sleep 1 }
    $summary.reachedMargin = (Count-LogMatches 'Margin client Connected') -gt $marginBefore
    if ($summary.reachedMargin -and -not $summary.authAccepted) {
        # The auth line can reach the log after the 30 s wait above; reaching the margin server proves it.
        $summary.authAccepted = (Count-LogMatches "accepted $Username without checking") -gt $authBefore
    }
    if (-not $summary.reachedMargin) { $ok = $false }   # still in the launcher: a tool problem, not a stall
}

if ($WaitWorld -and $ok) {
    if (-not $spawnsBefore) { $spawnsBefore = Count-WorldReplies }
    $t0 = Get-Date
    while (((Get-Date) - $t0).TotalSeconds -lt $WorldTimeoutSeconds -and (Count-WorldReplies) -le $spawnsBefore) { Start-Sleep 2 }
    $summary.inWorld = (Count-WorldReplies) -gt $spawnsBefore
    $summary.worldWaitSeconds = [int]((Get-Date) - $t0).TotalSeconds
    $spawnLine = Select-String -Path $ServerLog -Pattern '\[SPAWN\] Queued self-spawn for charId (\d+)' | Select-Object -Last 1
    if ($spawnLine) { $summary.charId = [int]$spawnLine.Matches[0].Groups[1].Value }
    if (-not $summary.inWorld) { $ok = $false }
    Start-Sleep 8   # let the world finish streaming in before any input
}

function Send-Chat([string]$text) {
    Press-Client 0x0D                 # Enter opens the chat input
    Start-Sleep 1
    Type-Client $text
    Press-Client 0x0D
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

if ($Walk -and $ok) {
    # Each entry is KEY:seconds, held in order with a 1 s pause between, e.g. -Walk W:2,A:0.5,Space:0.2.
    # W/S = forward/back, A/D = turn, Q/E = strafe, Space = jump.
    $keys = @{ SPACE = 0x20; LEFT = 0x25; UP = 0x26; RIGHT = 0x27; DOWN = 0x28; SHIFT = 0x10; ESCAPE = 0x1B; ENTER = 0x0D; TAB = 0x09 }
    foreach ($step in $Walk) {
        $name, $seconds = $step -split ':'
        $name = $name.ToUpperInvariant()
        $vk = if ($keys.ContainsKey($name)) { [byte]$keys[$name] } elseif ($name.Length -eq 1) { [byte][char]$name } else { throw "Unknown key '$name' in -Walk" }
        [MxoInput]::PostKeyHold((Get-ClientWindow), $vk, [int]([double]$seconds * 1000))
        Start-Sleep 1
    }
    $summary.walked = $true
    Start-Sleep 2
}

if ($DoubleClick -and $ok) {
    if ($DoubleClick.Count -ne 2) { throw '-DoubleClick takes x,y' }
    # Like a player: rest the cursor on the target, click once to target it, then double-click to use
    # it. A double-click on an untargeted object only targets it.
    $h = Get-ClientWindow
    [MxoInput]::PostDoubleClick($h, $DoubleClick[0], $DoubleClick[1])   # moves the cursor + targets
    Start-Sleep 2
    [MxoInput]::PostDoubleClick($h, $DoubleClick[0], $DoubleClick[1])   # uses the targeted object
    $summary.doubleClicked = $true
    Start-Sleep 4
}

if ($LauncherClick -and $ok) {
    if ($LauncherClick.Count -ne 2) { throw '-LauncherClick takes x,y' }
    Click-Launcher $LauncherClick[0] $LauncherClick[1]
    $summary.launcherClicked = $true
    Start-Sleep 1
}

if ($RightClick -and $ok) {
    if ($RightClick.Count -ne 2) { throw '-RightClick takes x,y' }
    [MxoInput]::PostGameRightClick((Get-ClientWindow), $RightClick[0], $RightClick[1])
    $summary.rightClicked = $true
    Start-Sleep 2
}

if ($Click -and $ok) {
    if ($Click.Count -ne 2) { throw '-Click takes x,y' }
    [MxoInput]::PostGameClick((Get-ClientWindow), $Click[0], $Click[1])
    $summary.clicked = $true
    Start-Sleep 3
}

function Capture-Screen {
    Add-Type -AssemblyName System.Drawing
    $size = [MxoInput]::ScreenSize()
    $bmp = New-Object System.Drawing.Bitmap $size[0], $size[1]
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $g.Dispose()
    return $bmp
}

if ($CheckVendorWindow) {
    # The merchant window's yellow "Ability Codes" category label sits around (20..110, 128..142) on
    # the 1024x768 game screen; nothing that yellow is there when the window is closed.
    $bmp = Capture-Screen
    $yellow = 0
    for ($x = 20; $x -lt 110; $x++) { for ($y = 128; $y -lt 142; $y++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.R -gt 180 -and $c.G -gt 180 -and $c.B -lt 60) { $yellow++ }
    } }
    $bmp.Dispose()
    $summary.vendorWindow = $yellow -gt 20
    if (-not $summary.vendorWindow) { $ok = $false }
}

if ($Screenshot) {
    $path = [IO.Path]::GetFullPath($Screenshot)
    $bmp = Capture-Screen
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $summary.screenshot = $path
}

$summary.ok = $ok
$summary | ConvertTo-Json -Compress
if (-not $ok) { exit 1 }
