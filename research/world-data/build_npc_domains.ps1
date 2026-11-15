param([Parameter(Mandatory=$true)][string]$Repository,[string]$Configuration='Release')
$ErrorActionPreference='Stop'
$Repository=(Resolve-Path -LiteralPath $Repository).Path
$bin=Join-Path $Repository "hds/bin/$Configuration/net8.0"
[Reflection.Assembly]::LoadFrom((Join-Path $bin 'hds.dll'))|Out-Null
$assembly=[hds.MissionProgress].Assembly;$flags=[Reflection.BindingFlags]'Static,NonPublic'
$type=$assembly.GetType('hds.MissionNpcDomainCertificates')
$clock=[Diagnostics.Stopwatch]::StartNew()
$world=$type.GetMethod('ReadOfflineWorld',$flags).Invoke($null,@([string](Join-Path $Repository 'data')))
$sites=[hds.MissionSites]::Build($world,[ushort]1)
[hds.SectorProperties]::Load((Join-Path $Repository 'data/sector_properties.csv'))
[hds.NavMesh]::UseDirectory((Join-Path $Repository 'data/navmesh'))
$directory=Join-Path $Repository 'data/mission_interior_evidence_v2/1'
$result=$type.GetMethod('BuildOffline',$flags).Invoke($null,@($sites.PSObject.BaseObject,$world.PSObject.BaseObject,[string]$directory))
$options=[Text.Json.JsonSerializerOptions]::new()
$property=$result.GetType().GetProperty('Document');$document=$property.GetValue($result)
$bytes=[Text.Json.JsonSerializer]::SerializeToUtf8Bytes($document,$document.GetType(),$options)
$path=Join-Path $directory 'npc-domains.json.gz'
$stream=[IO.File]::Create($path);try{$gzip=[IO.Compression.GZipStream]::new($stream,[IO.Compression.CompressionLevel]::SmallestSize,$true);try{$gzip.Write($bytes)}finally{$gzip.Dispose()}}finally{$stream.Dispose()}
$summary=@{Sites=$sites.Count;Rows=$document.Sites.Count;Domains=$document.Domains.Count;Positive=@($document.Domains|Where-Object {$_.Capacity-gt0}).Count;SceneLoads=$result.SceneLoads;ParseMilliseconds=$result.ParseMilliseconds;BuildSeconds=$result.Seconds;TotalSeconds=$clock.Elapsed.TotalSeconds;Sha256=(Get-FileHash -LiteralPath $path).Hash;NoServer=$true}
$summary|ConvertTo-Json -Depth 3
