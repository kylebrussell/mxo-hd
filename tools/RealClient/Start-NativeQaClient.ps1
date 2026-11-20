[CmdletBinding(DefaultParameterSetName = 'Start')]
param(
    [Parameter(Mandatory)][string]$FixtureDirectory,
    [Parameter(ParameterSetName = 'Start')]
    [string]$InstalledClientDir = (Join-Path $env:USERPROFILE 'Developer/mxo-client/MxO'),
    [Parameter(ParameterSetName = 'Start')]
    [string]$SecondaryClientDir = (Join-Path $env:USERPROFILE 'Developer/mxo-client2/MxO'),
    [Parameter(ParameterSetName = 'Start')][switch]$PrepareOnly,
    [Parameter(ParameterSetName = 'Stop', Mandatory)][switch]$Stop
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $FixtureDirectory).Path
$manifestPath = Join-Path $root 'native-client.json'
$handoff = Join-Path $env:TEMP 'mxo-claude-codex-handoff.txt'
$lease = [IO.FileStream]::new((Join-Path $root '.native-client-launch.lock'), [IO.FileMode]::CreateNew,
    [IO.FileAccess]::ReadWrite, [IO.FileShare]::None, 4096, [IO.FileOptions]::DeleteOnClose)
$clientDirLease = $null

try {
    function Get-DefenderSnapshot {
        @(Get-MpThreatDetection -ErrorAction Stop | ForEach-Object {
            "$($_.DetectionID)|$($_.InitialDetectionTime.Ticks)|$($_.LastThreatStatusChangeTime.Ticks)"
        })
    }

    function Write-Lifecycle([string]$State, [int]$ClientId) {
        Add-Content -LiteralPath $handoff -Encoding utf8 -Value (
            "Update {0:HH:mm} ET (Codex): second client {1}, pid {2}" -f (Get-Date), $State, $ClientId)
    }

    if ($Stop) {
        $stoppedPath = Join-Path $root 'native-client-stopped.json'
        if (Test-Path -LiteralPath $stoppedPath) {
            return Get-Content -LiteralPath $stoppedPath -Raw | ConvertFrom-Json
        }
        $launch = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $client = Get-Process -Id $launch.Pid -ErrorAction SilentlyContinue
        if ($client) {
            if ($client.ProcessName -ne 'matrix' -or $client.StartTime.ToUniversalTime().Ticks -ne [long]$launch.StartTicks) {
                throw 'The client PID has changed identity.'
            }
            Stop-Process -InputObject $client
            if (!$client.WaitForExit(5000)) { throw 'The owned client has not stopped.' }
        }
        Write-Lifecycle down $launch.Pid
        $new = @(Get-DefenderSnapshot | Where-Object { $_ -notin $launch.DefenderBaseline })
        $result = [pscustomobject]@{ Pid = $launch.Pid; DefenderChanges = $new.Count }
        $result | ConvertTo-Json | Set-Content -LiteralPath $stoppedPath
        $result
        if ($new.Count) { throw 'Defender recorded a change during the native client run. Stop here and retain the fixture logs.' }
        return
    }

    $state = Get-Content -LiteralPath (Join-Path $root 'state.json') -Raw | ConvertFrom-Json
    $endpoint = $state.Server
    $address = [Net.IPAddress]::Parse([string]$endpoint.Host)
    if ($state.Status -ne 'Ready' -or $state.Database -notmatch '^mxotest_' -or
        (Test-Path -LiteralPath (Join-Path $root 'stopped.json')) -or
        $address.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or
        ![Net.IPAddress]::IsLoopback($address) -or $address.ToString() -ne $endpoint.Host -or
        $address.Equals([Net.IPAddress]::Loopback) -or $state.Account -ne 'Codex_SecondQA' -or
        $endpoint.AuthPort -ne 11000 -or $endpoint.MarginPort -ne 10000 -or $endpoint.WorldPort -ne 10000) {
        throw 'Expected a ready secondary NativeQa fixture on a distinct IPv4 loopback address and the stock ports.'
    }
    foreach ($port in @($endpoint.AuthPort, $endpoint.MarginPort)) {
        $listener = Get-NetTCPConnection -State Listen -LocalAddress $endpoint.Host -LocalPort $port -ErrorAction SilentlyContinue
        if (!$listener -or $listener.OwningProcess -ne $state.ServerPid) {
            throw "Fixture TCP listener $($endpoint.Host):$port is unavailable or belongs to another process."
        }
    }
    $world = Get-NetUDPEndpoint -LocalAddress $endpoint.Host -LocalPort $endpoint.WorldPort -ErrorAction SilentlyContinue
    if (!$world -or $world.OwningProcess -ne $state.ServerPid) { throw 'The fixture UDP listener is unavailable or belongs to another process.' }
    $dns = $endpoint.Host.Replace('.', '-') + '.nip.io'
    foreach ($name in @($dns, "Recursion.$dns")) {
        $answers = @([Net.Dns]::GetHostAddresses($name))
        if (!$answers.Count -or @($answers | Where-Object { !$_.Equals($address) }).Count) {
            throw "The client hostname $name must resolve only to $address."
        }
    }

    $source = (Get-Item -LiteralPath $InstalledClientDir).FullName.TrimEnd('\')
    $privateClient = (Get-Item -LiteralPath $SecondaryClientDir).FullName.TrimEnd('\')
    $primary = (Join-Path $env:USERPROFILE 'Developer/mxo-client/MxO').Replace('/', '\')
    if ($privateClient -in @($source, $primary)) { throw 'Choose a dedicated secondary client directory.' }
    for ($folder = Get-Item -LiteralPath $privateClient; $folder; $folder = $folder.Parent) {
        if ($folder.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
            throw 'The secondary client directory must be a plain copy.'
        }
    }
    if (Test-Path -LiteralPath $manifestPath) { throw 'This fixture already owns a client launch. Use a fresh fixture for another launch.' }
    $clientDirLease = [IO.FileStream]::new((Join-Path $privateClient '.native-qa-client.lock'), [IO.FileMode]::CreateNew,
        [IO.FileAccess]::ReadWrite, [IO.FileShare]::None, 4096, [IO.FileOptions]::DeleteOnClose)
    $ownerPath = Join-Path $privateClient '.native-qa-client-owner.json'
    if (Test-Path -LiteralPath $ownerPath) {
        $owner = Get-Content -LiteralPath $ownerPath -Raw | ConvertFrom-Json
        $running = Get-Process -Id $owner.Pid -ErrorAction SilentlyContinue
        if ($running -and $running.ProcessName -eq 'matrix' -and
            $running.StartTime.ToUniversalTime().Ticks -eq [long]$owner.StartTicks) {
            throw "The secondary copy is in use by client PID $($owner.Pid)."
        }
    }
    $exe = Join-Path $privateClient 'launcher.exe'
    $hash = (Get-FileHash -LiteralPath $exe).Hash
    if ($hash -ne (Get-FileHash -LiteralPath (Join-Path $source 'launcher.exe')).Hash) {
        throw 'The private client launcher differs from the installed launcher.'
    }
    $cfg = Get-Item -LiteralPath (Join-Path $privateClient 'autoexec.cfg')
    if ($cfg.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint) -or $cfg.LinkType) {
        throw 'The secondary autoexec.cfg must be a private file.'
    }
    $encoding = [Text.Encoding]::GetEncoding(28591)
    $options = [ordered]@{ UseLaunchPad = '0'; AuthServerDNSName = "`"$dns`"";
        MarginServerDNSSuffix = "`".$dns`""; Windowed = '1'; ScreenWidth = '800'; ScreenHeight = '600' }
    $text = [IO.File]::ReadAllText($cfg.FullName, $encoding)
    foreach ($key in $options.Keys) {
        $text = [regex]::Replace($text, "(?im)^[ \t]*$key[ \t]*=[^\r\n]*(?:\r?\n|$)", '')
    }
    $lines = foreach ($key in $options.Keys) { "$key = $($options[$key])" }
    [IO.File]::WriteAllText($cfg.FullName, $text.TrimEnd("`r", "`n") + "`r`n" + ($lines -join "`r`n") + "`r`n", $encoding)
    if ($PrepareOnly) {
        return [pscustomobject]@{ ClientDir = $privateClient; Server = $endpoint; Account = $state.Account }
    }
    $baseline = @(Get-DefenderSnapshot)
    $launchedAt = Get-Date
    $launcher = Start-Process -FilePath $exe -ArgumentList '-nopatch','-noeula' -WorkingDirectory $privateClient -WindowStyle Normal -PassThru
    $deadline = (Get-Date).AddSeconds(25)
    $child = $null
    while (!$child -and (Get-Date) -lt $deadline) {
        $children = @(Get-CimInstance Win32_Process -Filter "Name='matrix.exe' AND ParentProcessId=$($launcher.Id)" |
            Where-Object { $_.CreationDate -ge $launchedAt.AddSeconds(-1) })
        if ($children.Count -gt 1) { throw 'The launcher started multiple child clients.' }
        if ($children.Count -eq 1) { $child = $children[0] } else { Start-Sleep -Milliseconds 250 }
    }
    if (!$child) {
        if (!$launcher.HasExited) { Stop-Process -InputObject $launcher }
        if (@(Get-DefenderSnapshot | Where-Object { $_ -notin $baseline }).Count) { throw 'Defender changed during launch.' }
        throw 'The launcher did not start a matrix client.'
    }
    $client = Get-Process -Id $child.ProcessId
    $announced = $false
    try {
        $launch = [pscustomobject]@{ Pid = $client.Id; StartTicks = $client.StartTime.ToUniversalTime().Ticks;
            ClientDir = $privateClient; Server = $endpoint; Account = $state.Account; DefenderBaseline = $baseline }
        $launch | Select-Object Pid, StartTicks | ConvertTo-Json | Set-Content -LiteralPath $ownerPath
        $launch | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath
        Write-Lifecycle up $client.Id
        $announced = $true
        if (@(Get-DefenderSnapshot | Where-Object { $_ -notin $baseline }).Count) { throw 'Defender changed during launch.' }
        if ((Get-FileHash -LiteralPath $child.ExecutablePath).Hash -ne $hash) {
            throw 'The unpacked client differs from the installed launcher.'
        }
        [pscustomobject]@{ Pid = $client.Id; ClientDir = $privateClient; Server = $endpoint; Account = $state.Account }
    } catch {
        if (!$client.HasExited) { Stop-Process -InputObject $client; $client.WaitForExit(5000) | Out-Null }
        if ($announced) { Write-Lifecycle down $client.Id }
        $changes = @(Get-DefenderSnapshot | Where-Object { $_ -notin $baseline })
        if ($changes.Count) { Write-Warning "Defender changes after client shutdown: $($changes.Count)." }
        throw
    }
} finally {
    if ($clientDirLease) { $clientDirLease.Dispose() }
    $lease.Dispose()
}
