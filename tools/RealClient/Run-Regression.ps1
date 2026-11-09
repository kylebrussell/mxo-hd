<#
.SYNOPSIS
Plays the gameplay checklist in the real 7.6005 client against this checkout's server and reports pass/fail
from the server's logs, with a screenshot per step, so a PR can be checked in minutes.

.DESCRIPTION
Plays one system per section, checks DebugLog.txt / the database, and undoes its test data. Aggro, Regen, Abilities,
Guns and Death each log in fresh; Social, Items, Trade, Market and Booth share one login at the hardline booth, so
they always run in this order. Sections: Aggro Regen Abilities Guns Social Items Trade Market Booth Death (default: all).

Takes the client lock (C:\Users\russe\Developer\mxo-client.lock), so it waits for another session using the client.
It won't start while a "Windows Security" prompt is open: answering that prompt is the user's call, and with
MXO_BIND_ADDRESS=127.0.0.1 the server doesn't trigger one. Build first (dotnet build "hds/Hardline Dreams MxO server.csproj").

The UI coordinates were measured at 1024x768 from the login spots used here; a section that fails may just have
lost its target, so read its screenshots in -OutDir (a failed check prints the name of the step). The game picks
targets from the real cursor: anything covering the game, such as a Windows notification, makes clicks there fail.

.EXAMPLE
./tools/RealClient/Run-Regression.ps1 -Sections Guns,Market
#>
[CmdletBinding()]
param(
    [string[]]$Sections = @('Aggro', 'Regen', 'Abilities', 'Guns', 'Social', 'Items', 'Trade', 'Market', 'Booth', 'Death'),
    [string]$OutDir = (Join-Path $env:TEMP 'mxo-regression'),
    [int]$WaitLockMinutes = 45,
    [string]$DbContainer = 'mxo-hd-mysql'
)

$ErrorActionPreference = 'Continue'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$tool = Join-Path $PSScriptRoot 'Invoke-RealClient.ps1'
$lock = 'C:\Users\russe\Developer\mxo-client.lock'
$Field = @(24500, 500, -1900)            # TheNeo beside the Blackwoods in the Slums
$Booth = @(67566.8, -105, -10164.6)      # TheNeo beside the Achan hardline booth (rotation 0: booth on the right)
$TheNeo = 2
New-Item -ItemType Directory -Force $OutDir | Out-Null
Set-Location $root

$script:results = New-Object System.Collections.ArrayList
$script:section = ''
$script:server = $null
$script:logStart = 0

function Check([string]$name, [bool]$ok, [string]$detail = '') {
    [void]$script:results.Add([pscustomobject]@{ Section = $script:section; Check = $name; Ok = $ok; Detail = $detail })
    "  {0} {1}{2}" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $name, $(if ($detail) { " ($detail)" } else { '' }) | Write-Host
}

function Sql([string]$sql) {
    docker exec $DbContainer sh -c "mysql -N -u`"`$MYSQL_USER`" -p`"`$MYSQL_PASSWORD`" `"`$MYSQL_DATABASE`" -e `"$sql`"" 2>$null
}

function Log-Lines {
    if (-not (Test-Path DebugLog.txt)) { return @() }
    @(Get-Content DebugLog.txt | Select-Object -Skip $script:logStart)
}

function Mark-Log { $script:logStart = if (Test-Path DebugLog.txt) { @(Get-Content DebugLog.txt).Count } else { 0 } }

function Log-Has([string]$pattern) { [bool](Log-Lines | Select-String -Pattern $pattern) }

function Log-Count([string]$pattern) { @(Log-Lines | Select-String -Pattern $pattern).Count }

function Wait-Log([string]$pattern, [int]$seconds = 30, [int]$count = 1) {
    $t0 = Get-Date
    while (((Get-Date) - $t0).TotalSeconds -lt $seconds) {
        if ((Log-Count $pattern) -ge $count) { return $true }
        Start-Sleep 1
    }
    (Log-Count $pattern) -ge $count
}

function Shot([int]$cp, [string]$name) {
    & $tool -ClientPid $cp -Screenshot (Join-Path $OutDir "$($script:section)-$name.png") 2>&1 | Out-Null
}

# Runs the client tool for one client and ignores its summary line; Check the result from the logs (polled by Wait-Log),
# so the wait after each input can be short.
function Act([int]$cp, [hashtable]$a) { $a.ClientPid = $cp; $a.Settle = 1; & $tool @a 2>&1 | Out-Null }

# Clicks a button and, when a log line proves it worked, retries a dropped click: the game reads the real cursor once
# per frame, so a click posted just after moving it can miss the hover.
function Click-Until([int]$cp, [int[]]$at, [string]$what, [string]$pattern, [int]$tries = 3) {
    if (-not $pattern) { Act $cp @{ Click = $at }; return }
    foreach ($try in 1..$tries) {
        Act $cp @{ Click = $at }
        if (Wait-Log $pattern 3) { return }
        Write-Host "  no answer to $what, clicking again"
    }
}

function Stop-Everything {
    Get-Process matrix -ErrorAction SilentlyContinue | Stop-Process -Force
    if ($script:server) {
        Get-CimInstance Win32_Process -Filter "ParentProcessId=$($script:server.Id)" | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        Stop-Process -Id $script:server.Id -Force -ErrorAction SilentlyContinue
        $script:server = $null
    }
}

function Start-World([switch]$AggroEveryone) {
    Stop-Everything
    Start-Sleep 2
    $env:MXO_PACKET_LOG = '1'; $env:MXO_DEV_ANY_PASSWORD = '1'; $env:MXO_DEBUG_LOG = '1'; $env:MXO_RPC_LOG = '1'
    $env:MXO_CLIENT_VIEW_LOG = '1'; $env:MXO_MOB_WANDER = '0'; $env:MXO_MOBS = 'test'; $env:MXO_BIND_ADDRESS = '127.0.0.1'; $env:MXO_DEV_COMMANDS = '1'
    $env:MXO_AGGRO_LEVEL_GAP = $(if ($AggroEveryone) { '100' } else { $null })
    $out = Join-Path $OutDir 'server.out'
    $script:server = Start-Process (Join-Path $root 'hds\bin\Debug\net8.0\hds.exe') -WorkingDirectory $root -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $out -RedirectStandardError (Join-Path $OutDir 'server.err')
    # Its last startup line, printed once the world data is loaded
    $t0 = Get-Date
    while (((Get-Date) - $t0).TotalSeconds -lt 90 -and -not (Select-String -Path $out -SimpleMatch "Im'running" -Quiet -ErrorAction SilentlyContinue)) { Start-Sleep -Milliseconds 500 }
}

# Logs TheNeo in on a fresh client and returns its process id (0 when it didn't reach the world).
function Login([double[]]$pos = $Field, [int]$rotation = 126, [string]$handle = 'TheNeo', [string]$user = 'loluser', [int]$row = 2, [switch]$Second) {
    $a = @{ Handle = $handle; SetPosition = $pos; SetRotation = $rotation; Login = $true; Username = $user; CharacterRow = $row; WaitWorld = $true; WorldTimeoutSeconds = 60 }
    if ($Second) { $a.NewInstance = $true } else { $a.Relaunch = $true }
    Mark-Log                                 # before the login: an NPC can attack while the tool is still waiting
    $r = & $tool @a 2>&1 | Select-Object -Last 1
    try { $j = $r | ConvertFrom-Json } catch { $j = $null }
    if ($j -and -not $j.inWorld -and -not $Second) {
        # Logins occasionally stall on "Negotiating with Servers..." (cause unknown); a relaunch has cleared it
        Write-Host "  login stalled, retrying once"
        $r = & $tool @a 2>&1 | Select-Object -Last 1
        try { $j = $r | ConvertFrom-Json } catch { $j = $null }
    }
    if (-not $j -or -not $j.inWorld) {
        Check "login $handle" $false "$r"
        if ($j -and $j.clientPid) { Shot ([int]$j.clientPid) 'login-failed' }
        return 0
    }
    [int]$j.clientPid
}

# ---- sections -------------------------------------------------------------------------------------

function Test-Aggro {
    Start-World -AggroEveryone
    $cp = Login; if (-not $cp) { return }
    Act $cp @{ Chat = @('?weaken 60') }      # a real fight, just not a long one
    Check 'an NPC attacks on its own' (Wait-Log '\[AGGRO\] .* attacks' 20)
    Shot $cp 'attacked'
    Act $cp @{ DoubleClick = @(435, 300) }
    Check 'the attacker dies' (Wait-Log 'Sending death for mob' 120)
    Check 'the loot window opens' (Wait-Log 'Loot window sent' 10)
    Start-Sleep 3                            # the corpse window appears a moment after its record
    Shot $cp 'corpse'
    Click-Until $cp @(348, 358) 'Take All' '\[LOOT\] Player received' 6
    Check 'Take All gives the loot' (Log-Has '\[LOOT\] Player received')
    Shot $cp 'looted'
}

function Test-Regen {
    Start-World
    $cp = Login; if (-not $cp) { return }
    Check 'no aggro on a level 50' (-not (Wait-Log '\[AGGRO\]' 8))
    Act $cp @{ Chat = @('?weaken 150') }     # enough rounds for the NPC to land some hits
    $fightStart = @(Get-Content PacketLog.txt).Count
    # One NPC is a small target: double-click again if the client sent no fight request
    foreach ($try in 1..3) {
        Act $cp @{ DoubleClick = @(500, 640) }
        if (Wait-Log 'Close combat requested' 3) { break }
        Write-Host "  no fight started, double-clicking again"
    }
    Check 'player attack kills the NPC' (Wait-Log 'Sending death for mob' 120)
    $packets = @(Get-Content PacketLog.txt).Count
    Act $cp @{ Walk = @('S:1.5') }
    Start-Sleep 12
    $after = @(Get-Content PacketLog.txt | Select-Object -Skip $packets)
    Check 'movement after the fight' ([bool]($after | Select-String '^02 03 02 00 01 '))
    # Own-view health values since the fight began: they must have risen again after the last hit, or be full
    $health = @(Get-Content PacketLog.txt | Select-Object -Skip $fightStart | Select-String '02 00 02 80 80 80 40 ([0-9a-f]{2}) ([0-9a-f]{2})' | ForEach-Object {
        [Convert]::ToInt32($_.Matches[0].Groups[2].Value + $_.Matches[0].Groups[1].Value, 16) })
    $rose = $false
    for ($i = 1; $i -lt $health.Count; $i++) { if ($health[$i] -gt $health[$i - 1]) { $rose = $true } }
    Check 'health regenerates after the fight' ($health.Count -eq 0 -or $rose) ("health updates: " + ($health -join ','))
    Shot $cp 'after'
}

function Test-Abilities {
    Start-World
    $cp = Login; if (-not $cp) { return }
    Act $cp @{ Walk = @('O:0.05') }
    # Drag the Actions window to the top left, clear of any notification: its contents move by -660,-235
    Act $cp @{ Drag = @(860, 245, 200, 10) }
    Act $cp @{ DoubleClick = @(193, 374); Once = $true }
    Check 'Hyper-Speed buff on' (Wait-Log '\[BUFF\] 12 on' 10)
    Act $cp @{ DoubleClick = @(198, 278); Once = $true }
    Check 'Evade Combat on' (Wait-Log '\[BUFF\] 217 on' 10)
    Act $cp @{ DoubleClick = @(198, 278); Once = $true }
    Check 'Evade Combat off' (Wait-Log '\[BUFF\] 217 off' 10)
    1..3 | ForEach-Object { Act $cp @{ Click = @(298, 365) } }
    Shot $cp 'list'
    Act $cp @{ DoubleClick = @(185, 341); Once = $true }
    Check 'Overclock buff on' (Wait-Log '\[BUFF\] 327 on' 10)
    Act $cp @{ DoubleClick = @(480, 560) }
    Check 'fight starts' (Wait-Log 'Close combat requested' 10)
    Act $cp @{ DoubleClick = @(200, 309) }   # two uses 2 s apart: the second is on its 4 s cooldown
    Check 'Logic Blast lands' (Wait-Log '\[ABILITY\] \d+ lands on view' 10)
    Start-Sleep 3
    Check 'a second use during the cast or cooldown does not land' ((Log-Count '\[ABILITY\] \d+ lands on view') -eq 1) ((Log-Lines | Select-String 'denied|lands on view' | ForEach-Object Line) -join '; ')
    Shot $cp 'blast'
}

function Test-Guns {
    Start-World
    $cp = Login; if (-not $cp) { return }
    try {
        Act $cp @{ Chat = @('?weaken 150') }     # survives two shots: long enough to close in
        Act $cp @{ Chat = @('?additem 2565') }   # logs "Free Fire weapon" only when the client has none chosen yet
        # Target the Blackwoods from the login spot, where it is always at the same place on screen: walking
        # distance varies between runs, and it stays targeted while walking.
        Act $cp @{ Click = @(500, 640) }
        Act $cp @{ Walk = @('D:0.8') }
        Act $cp @{ Walk = @('S:4.5') }
        Shot $cp 'far'
        Act $cp @{ Click = @(710, 18) }
        Check 'a target out of range is refused' (Wait-Log 'Range combat refused' 10)
        Shot $cp 'outofrange'
        Act $cp @{ Walk = @('W:3') }
        Act $cp @{ Click = @(710, 18) }
        Check 'Free Fire fires' (Wait-Log 'Shot at .*: (hit|miss)' 10)
        Check 'a melee NPC closes in on the shooter' (Wait-Log 'closes in on the shooter' 15)
        Shot $cp 'firing'
        Check 'the NPC dies to gunfire' (Wait-Log 'Combat ended: DefenderDied' 120)
    }
    finally {
        Stop-Everything
        Sql "delete from inventory where charId=$TheNeo and goid=2565 and slot<>22"
    }
}

# Social, Items, Trade, Market and Booth share this login. Their test items go in before it: the bag loads at login.
$script:boothClient = 0
$script:bagMoved = $false
$script:cash = $null

function Use-BoothSession {
    if ($script:boothClient -and (Get-Process -Id $script:boothClient -ErrorAction SilentlyContinue)) { Mark-Log; return $script:boothClient }
    Start-World
    Clear-BoothData
    $script:cash = Sql "select cash from characters where charId=$TheNeo"
    Sql "insert into inventory (charId,goid,slot,count,purity,created) values ($TheNeo,2399,30,3,0,NOW()),($TheNeo,7336,40,5,0,NOW()),($TheNeo,2565,23,0,0,NOW()),($TheNeo,2565,28,0,0,NOW())"
    $script:bagMoved = $false
    $script:boothClient = Login -pos $Booth -rotation 0
    # Booth mid-screen, clear of any notification in the corner
    if ($script:boothClient) { Act $script:boothClient @{ Walk = @('D:0.3') } }
    Mark-Log
    $script:boothClient
}

function Clear-BoothData {
    Sql "delete from inventory where charId=$TheNeo and (goid in (2399,7332,7336,7458) or (goid=2565 and slot<>22)); delete from char_abilities where char_id=$TheNeo and ability_name like 'Code storage%'; delete from marketplace where charID=$TheNeo"
    if ($script:cash) { Sql "update characters set cash=$($script:cash) where charId=$TheNeo" }
}

# The bag dragged to the top left, clear of any notification, on page 2. The game stops it at the screen's edge, and a
# trade window covers its right column: slot 23 is at 180,307, slot 28 at 71,379 (clear of the trade window), slot 30 at 143,379.
function Show-BagPage2([int]$cp) {
    if (-not $script:bagMoved) {
        Act $cp @{ Drag = @(860, 245, 100, 10) }
        Act $cp @{ Click = @(83, 230) }
        $script:bagMoved = $true
    }
}

function Test-Social {
    $cp = Use-BoothSession; if (-not $cp) { return }
    $packets = @(Get-Content PacketLog.txt).Count
    Act $cp @{ Chat = @('/invite NoSuchHandle') }
    Start-Sleep 1
    Check 'a bad invite gets the error record' ([bool](Get-Content PacketLog.txt | Select-Object -Skip $packets | Select-String '80 8d'))
    Act $cp @{ Chat = @('/friend NoSuchHandle') }
    Check 'a friend add of a missing handle is handled' (-not (Log-Has 'Exception'))
    Shot $cp 'social'
}

function Test-Items {
    $cp = Use-BoothSession; if (-not $cp) { return }
    Act $cp @{ Walk = @('I:0.05') }
    Show-BagPage2 $cp
    Shot $cp 'bag'
    Act $cp @{ DoubleClick = @(143, 379); Once = $true }
    Check 'a pill is used' (Wait-Log 'Used item 2399' 10)
    Start-Sleep 1
    Check 'the pill stack drops to 2' ((Sql "select count from inventory where charId=$TheNeo and goid=2399") -eq '2')
    Shot $cp 'pill'
    Act $cp @{ Chat = @('?compile 7458') }
    Check 'compile makes the item' (Wait-Log 'Compiled 7458' 10)
    Check 'compile uses two Code Bits' ((Sql "select count from inventory where charId=$TheNeo and goid=7336") -eq '3')
    Shot $cp 'compiled'
}

# ?tradebot: a partner played by the server. A real two-player trade needs a second client.
function Test-Trade {
    $cp = Use-BoothSession; if (-not $cp) { return }
    $before = [long](Sql "select cash from characters where charId=$TheNeo")
    Act $cp @{ Chat = @('?tradebot') }
    Check 'the trade opens' (Wait-Log '\[TRADE\] Session open' 10)
    Show-BagPage2 $cp                        # the bag opens with the trade
    Shot $cp 'open'
    Act $cp @{ Drag = @(71, 379, 510, 240) }
    Check 'an item is offered' (Wait-Log '\[TRADE\] Action 0 .*slot 28' 10)
    Shot $cp 'offered'
    Act $cp @{ Click = @(606, 372) }
    Check 'accepting completes the trade' (Wait-Log '\[TRADE\] Complete' 10)
    Start-Sleep 2
    Check 'the item left and the bot''s item arrived' ((Sql "select count(*) from inventory where charId=$TheNeo and ((goid=2565 and slot=28) or goid=7332)") -eq '1')
    Check 'information rose by 100' ([long](Sql "select cash from characters where charId=$TheNeo") -eq ($before + 100))
    Shot $cp 'complete'
    Act $cp @{ Chat = @('?tradebot') }
    Wait-Log '\[TRADE\] Session open' 10 2 | Out-Null
    Act $cp @{ Click = @(451, 401) }
    Check 'Cancel ends the trade' (Wait-Log '\[TRADE\] Cancelled' 10)
}

function Test-Market {
    $cp = Use-BoothSession; if (-not $cp) { return }
    if ($script:bagMoved) { Act $cp @{ Click = @(202, 11) }; $script:bagMoved = $false }   # the bag would cover the window
    Act $cp @{ RightClick = @(590, 430) }
    Act $cp @{ Click = @(588, 380) }
    Start-Sleep 1
    Shot $cp 'open'
    Click-Until $cp @(800, 313) 'Weapons' $null  # Items
    Click-Until $cp @(800, 405) 'Handguns' $null # Weapons
    Click-Until $cp @(800, 405) 'search' '\[MARKET\] Search '   # Handguns
    Check 'search answers' (Log-Has '\[MARKET\] Search ')
    Shot $cp 'search'
    Act $cp @{ Click = @(567, 184) }     # Selling
    Act $cp @{ Click = @(325, 217) }     # Possessions filter
    Act $cp @{ Click = @(193, 303) }     # Weapons
    Act $cp @{ Drag = @(220, 252, 600, 380) }
    Act $cp @{ Click = @(510, 338) }
    Act $cp @{ TypeText = '1000' }
    Shot $cp 'price'
    Act $cp @{ Click = @(453, 419) }
    Check 'listing an item' (Wait-Log '\[MARKET\] \d+ listed goid 2565' 10)
    Shot $cp 'listed'
    Click-Until $cp @(799, 266) 'De-list' '\[MARKET\] \d+ delisted listing'
    Check 'De-list returns it' (Log-Has '\[MARKET\] \d+ delisted listing')
    Shot $cp 'delisted'
    Act $cp @{ Click = @(869, 141) }     # close it for the sections after this one
}

# Last on the shared login: its Exit quits the client.
function Test-Booth {
    $cp = Use-BoothSession; if (-not $cp) { return }
    Shot $cp 'booth'
    Act $cp @{ DoubleClick = @(590, 430); Once = $true }
    Check 'Upload registers the hardline' (Wait-Log 'Booth \d+ action 4' 10)
    Check 'Upload moves Code Bits to code storage' (Wait-Log '\[CODE STORAGE\] Uploaded' 5)
    Act $cp @{ RightClick = @(590, 430) }
    Shot $cp 'menu'
    Act $cp @{ Click = @(498, 412) }
    Check 'Loading Area asks first' (Wait-Log 'Booth \d+ action 5' 10)
    Act $cp @{ Click = @(550, 433) }     # No
    Act $cp @{ RightClick = @(590, 430) }
    Act $cp @{ Click = @(588, 480) }
    Check 'Exit asks first' (Wait-Log 'Booth \d+ action 3' 10)
    # The prompt can show a moment after the server's 80 fb, so a single early click missed it in full runs
    Click-Until $cp @(472, 433) 'the jack-out prompt' 'Jack out at a hardline' 4
    Check 'jack out at the booth' (Wait-Log 'Jack out at a hardline' 10)
    Start-Sleep 5
    Check 'the client leaves' (-not (Get-Process -Id $cp -ErrorAction SilentlyContinue))
    $script:boothClient = 0
}

function Test-Death {
    Start-World
    $cp = Login -pos $Booth -rotation 0; if (-not $cp) { return }
    Act $cp @{ Chat = @('?die') }
    Check 'death waits for the respawn request' (Wait-Log 'waiting for the respawn request' 10)
    Shot $cp 'dead'
    Act $cp @{ Chat = @('/reconstruct') }
    Check 'Reconstruct goes to the Loading Area' (Wait-Log '\[LA\] Spawn ready in the Loading Area' 40)
    Shot $cp 'la'
    Act $cp @{ Click = @(105, 17) }
    Act $cp @{ Click = @(143, 235) }
    Shot $cp 'hardlines'
    Act $cp @{ Click = @(747, 572) }
    Check 'jack in from the Loading Area' (Wait-Log '\[LA\] Leaving the Loading Area' 15)
    Start-Sleep 20
    Shot $cp 'jackedin'
}

# ---- run -------------------------------------------------------------------------------------------

$deadline = (Get-Date).AddMinutes($WaitLockMinutes)
$got = $false
while (-not $got -and (Get-Date) -lt $deadline) {
    if ((Test-Path $lock) -and ((Get-Date) - (Get-Item $lock).LastWriteTime).TotalMinutes -gt 40) { Remove-Item $lock -Force }
    if (Get-Process -Name PickerHost -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -eq 'Windows Security' }) { Start-Sleep 30; continue }
    try { New-Item -ItemType File -Path $lock -Value "regression $(Get-Date -Format o)" -ErrorAction Stop | Out-Null; $got = $true } catch { Start-Sleep 20 }
}
if (-not $got) { Write-Host "Client lock not free after $WaitLockMinutes minutes"; exit 2 }

$runStart = Get-Date
try {
    $order = 'Aggro', 'Regen', 'Abilities', 'Guns', 'Social', 'Items', 'Trade', 'Market', 'Booth', 'Death'
    foreach ($name in ($order | Where-Object { $Sections -contains $_ })) {
        $script:section = $name
        $sectionStart = Get-Date
        Write-Host "== $name"
        (Get-Item $lock).LastWriteTime = Get-Date
        try { & "Test-$name" } catch { Check 'section ran' $false "$_" }
        if (Log-Has 'Exception') { Check 'no server exception' $false ((Log-Lines | Select-String 'Exception' | Select-Object -First 1).Line) }
        Write-Host ("  ({0:N0} s)" -f ((Get-Date) - $sectionStart).TotalSeconds)
    }
}
finally {
    Stop-Everything
    if ($script:cash) { Clear-BoothData }
    Remove-Item $lock -Force -ErrorAction SilentlyContinue
}

$failed = @($script:results | Where-Object { -not $_.Ok })
$elapsed = (Get-Date) - $runStart
$report = Join-Path $OutDir 'report.md'
"# Client regression $(Get-Date -Format s)`n" | Set-Content $report
"Commit: $(git rev-parse --short HEAD)`n" | Add-Content $report
"| Section | Check | Result | Detail |`n|---|---|---|---|" | Add-Content $report
$script:results | ForEach-Object { "| $($_.Section) | $($_.Check) | $(if ($_.Ok) { 'pass' } else { '**FAIL**' }) | $($_.Detail) |" } | Add-Content $report
Write-Host "`n$($script:results.Count - $failed.Count)/$($script:results.Count) passed in $([int][Math]::Floor($elapsed.TotalMinutes)) min $($elapsed.Seconds) s. Report: $report; screenshots in $OutDir"
exit $(if ($failed.Count) { 1 } else { 0 })
