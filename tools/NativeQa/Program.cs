using HeadlessClient;
using hds;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using NativeQa;

// Reject mistyped inspection flags before even resolving a fixture mode or creating a path.
if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
{
    Console.WriteLine("NativeQa --fixture-plan [mode] | --build-info [repository] | --bootstrap-checks <repository> <proof-directory> [mode ...]");
    Console.WriteLine("Native preparation: NativeQa <repository> <fixture-directory> <commit> [mode] [ability-ids]");
    return;
}
if (args.Length > 0 && args[0].StartsWith('-') &&
    args[0] != "--fixture-plan" && args[0] != "--build-info" && args[0] != "--bootstrap-checks")
{
    Console.Error.WriteLine("Unknown NativeQa option: " + args[0] + ". Use --help for supported commands.");
    Environment.ExitCode = 2;
    return;
}

#if DEBUG
const string configuration = "Debug";
#else
const string configuration = "Release";
#endif
// Integration-only pre-client checks use dynamic ports and owned disposable schemas, never the client lock.
if (args.Length > 0 && args[0] == "--bootstrap-checks")
{
    if (args.Length < 3) throw new ArgumentException("--bootstrap-checks <repository> <proof-directory> [mode ...]");
    string[] checks = args.Length > 3 ? args.Skip(3).ToArray() : NativeQaModes.Names.ToArray();
    await NativeQaBootstrapChecks.RunAsync(args[1], args[2], checks);
    return;
}
// This is the same plan passed to StartAsync, and exits before any path, lock or database setup.
if (args.Length > 0 && args[0] == "--fixture-plan")
{
    string mode = args.Length > 1 ? args[1] : "";
    NativeQaModes.RequireKnown(mode);
    Console.WriteLine(JsonSerializer.Serialize(new { Mode=mode, Environment=NativeQaFixturePlan.EnvironmentFor(mode), Plan=NativeQaModes.Find(mode).Plan, SeedDisguiseTool=NativeQaFixturePlan.SeedDisguiseTool(mode), CompileArchiveValue=NativeQaFixturePlan.CompileArchiveValue },
        new JsonSerializerOptions { WriteIndented=true }));
    return;
}
// Read-only provenance inspection exits before fixture directories, client locks or database access.
if (args.Length > 0 && args[0] == "--build-info")
{
    string inspectRoot = Path.GetFullPath(args.Length > 1 ? args[1] : Directory.GetCurrentDirectory());
    string assembly = Path.Combine(inspectRoot, "hds", "bin", configuration, "net8.0", "hds.dll");
    ushort[] requested = args.Length > 2 ? args[2].Split(',').Select(ushort.Parse).Distinct().ToArray() : Array.Empty<ushort>();
    using var planCatalog = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(inspectRoot,"data","ability_catalog.json")));
    Console.WriteLine(JsonSerializer.Serialize(new { Configuration=configuration, SourceAssembly=assembly,
        SourceAssemblySha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assembly))),
        RequestedAbilities=requested, OwnedUnloadedDisciplineAncestors=NativeQaFixtureSeeder.DisciplineAncestors(planCatalog.RootElement,requested) },
        new JsonSerializerOptions { WriteIndented=true }));
    return;
}

// Server/data preparation only. The real client's inputs are supplied through Computer Use.
var fixtureMode=NativeQaModes.RequireKnown(args.Length > 3 ? args[3] : "");
string root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
if (!File.Exists(Path.Combine(root,"SQL","reality_hd.sql")))
{
    Console.Error.WriteLine("NativeQa repository must contain SQL/reality_hd.sql: " + root);
    Environment.ExitCode = 2;
    return;
}
string qaRoot = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "mxo-native-ability-qa"));
string commit = args.Length > 2 ? args[2] : "local";
// Optional fifth argument keeps a focused native checklist small enough to avoid legacy UI scrolling.
ushort[] requestedAbilities = args.Length > 4 ? args[4].Split(',', StringSplitOptions.RemoveEmptyEntries)
    .Select(s => ushort.Parse(s.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture)).Distinct().ToArray()
    : Array.Empty<ushort>();
if (args.Length > 4 && (requestedAbilities.Length == 0 || requestedAbilities.Any(id => id == 0 || id >= 4096)))
    throw new ArgumentException("The optional loadout must contain canonical player ability IDs1..4095.");
var fixturePlan=fixtureMode.Plan;
if (fixturePlan.HotbarAbilities.Count != 0 && requestedAbilities.Length != 0)
    throw new ArgumentException("Seeded hotbars use the mode's fixed loadout.");
byte[]? characterUi = fixturePlan.HotbarAbilities.Count != 0
    ? AbilitySweepHotbar.FromEnvironment(fixturePlan.HotbarAbilities, required: true)
    : null;
const string handoff = "C:/Users/russe/AppData/Local/Temp/mxo-claude-codex-handoff.txt";
const string lockPath = "C:/Users/russe/Developer/mxo-client.lock";
const string account = "CSR_AbilityQA";
const uint nativeId = 110;
Directory.CreateDirectory(qaRoot);
Directory.CreateDirectory(Path.Combine(qaRoot, "requests"));
Directory.CreateDirectory(Path.Combine(qaRoot, "responses"));
// Other agents use an existence-based text reservation. CreateNew respects both that convention and
// exclusive open handles; OpenOrCreate could overwrite their text lock and delete it on shutdown.
using FileStream clientLock = new(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
await clientLock.WriteAsync(Encoding.UTF8.GetBytes($"Codex native ability QA {Environment.ProcessId}, CSR_Tester 110, {commit}"));
await clientLock.FlushAsync();
string statePath = Path.Combine(qaRoot, "state.json");
await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(new { Status = "Preparing", BuildCommit = commit }));
await File.AppendAllTextAsync(handoff, $"\nUpdate {DateTime.Now:HH:mm} ET (Codex): Took mxo-client.lock for native QA, build {commit}. Native account {account} exposes only CSR_Tester110 copied into a disposable database; helper players are headless QA fixtures. Main110 will be reset to Mara, full health, flag0 and mission986 only before release.\n");
using ThrowawayDatabase database = await ThrowawayDatabase.CreateAsync(root, "native_ability");
ServerInstance? server = null;
var helpers = new Dictionary<string, HeadlessPlayer>(StringComparer.OrdinalIgnoreCase);
var helperNames = NativeQaFixtureSeeder.HelperNames;
try
{
    var seeded = await NativeQaFixtureSeeder.PrepareAsync(root, database, fixturePlan, requestedAbilities);
    ushort[] abilities = seeded.Abilities;
    ushort[] disciplineAncestors = seeded.DisciplineAncestors;
    string snapshot = Path.Combine(qaRoot, "snapshot");
    string sourceBin = Path.Combine(root, "hds", "bin", configuration, "net8.0");
    string targetBin = Path.Combine(snapshot, "hds", "bin", configuration, "net8.0");
    foreach (string file in Directory.EnumerateFiles(sourceBin, "*", SearchOption.AllDirectories))
    {
        string target = Path.Combine(targetBin, Path.GetRelativePath(sourceBin, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target, true);
    }
    string sourceAssembly = Path.Combine(sourceBin, "hds.dll");
    string sourceAssemblySha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(sourceAssembly)));
    string snapshotAssemblySha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(targetBin, "hds.dll"))));
    if (sourceAssemblySha256 != snapshotAssemblySha256)
        throw new InvalidOperationException("Server assembly changed during the QA snapshot; rebuild and start a fresh fixture.");
    server = await ServerInstance.StartAsync(snapshot, database.Name,
        NativeQaFixturePlan.EnvironmentFor(args.Length > 3 ? args[3] : ""),
        fixturePlan.MissionFixture==null?null:Path.Combine(root,"tools","HeadlessClient","Fixtures",fixturePlan.MissionFixture),
        testMobFixture: fixturePlan.TestMobFixture == null ? null : Path.Combine(root, "tools", "NativeQa", "Fixtures", fixturePlan.TestMobFixture),
        testCharacterUi: characterUi);
    byte[] shieldAck = await NativeQaReadiness.BootstrapAsync(fixtureMode,
        new NativeQaBootstrapContext(qaRoot, helperNames, helpers, OwnedDatabase: database, ActualServer: server, RepositoryRoot: root));
    await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(fixturePlan.StateWithMetadata(new NativeQaFixtureState
    { Database=database.Name, Directory=server.WorkingDirectory, Account=account, Character="CSR_Tester", CharId=nativeId, BuildCommit=commit,
        Configuration=configuration, SourceAssembly=sourceAssembly, SourceAssemblySha256=sourceAssemblySha256,
        SnapshotAssemblySha256=snapshotAssemblySha256, Status="Ready", Abilities=abilities,
        StealthShieldQa=fixturePlan.StealthShieldQa,ShieldReady=shieldAck.Length>0,CasterOrganization=fixturePlan.StealthShieldQa?1:(int?)null,CasterPvpFlag=fixturePlan.StealthShieldQa,
        OwnedUnloadedDisciplineAncestors=disciplineAncestors, LoadedAbilityAncestors=seeded.LoadedAncestors,
        HealthCallbackQa=fixturePlan.HealthCallbackQa, CompileItemsQa=fixturePlan.CompileItemsQa,
        CallbackRoll15=fixturePlan.HealthCallbackQa?0:(int?)null, CallbackRoll100=fixturePlan.HealthCallbackQa?0:(int?)null }), new JsonSerializerOptions { WriteIndented=true }));
    foreach(string message in fixturePlan.ReadyMessages) Console.WriteLine(message);
    Console.WriteLine("Native ability QA ready: CSR_AbilityQA / placeholder password / CSR_Tester110.");
    int seen = 0;
    (string Identity, int Phase)? announcedEntrance = null;
    var handled = new HashSet<string>();
    DateTime deadline = DateTime.UtcNow.AddHours(4);
    while (!File.Exists(Path.Combine(qaRoot,"stop")) && DateTime.UtcNow < deadline && !server.HasExited)
    {
        if (fixtureMode is NativeQa.Modes.StealthDetectionMode)
            await NativeQa.Modes.StealthDetectionMode.RecordResultAsync(qaRoot, server, helpers);
        if (fixtureMode.Name == "mission-catalog-sample")
        {
            string checkpoint = await database.ScalarAsync("SELECT IFNULL(HEX(payload),'') FROM char_active_missions WHERE charId=110");
            if (!string.IsNullOrWhiteSpace(checkpoint))
            {
                var progress = MervFollowupCheckpointFixture.ParseHex(checkpoint);
                if (progress != null && announcedEntrance != (progress.Identity, progress.Phase))
                {
                    announcedEntrance = (progress.Identity, progress.Phase);
                    foreach (string message in MissionCatalogEntrances.Describe(progress, root))
                        Console.WriteLine("READY " + message);
                    foreach (var approach in MissionCatalogObjectApproaches.Find(progress, root))
                        Console.WriteLine("READY " + approach.Describe());
                    foreach (string message in PigeonEscortExitFixture.Describe(progress))
                        Console.WriteLine("READY " + message);
                }
            }
        }
        if (File.Exists(Path.Combine(qaRoot,"helpers")) && helpers.Count == 0)
            foreach (string name in helperNames)
            {
                var player = await HeadlessPlayer.LoginAsync(new ServerAddress("127.0.0.1",11000,10000,10000), name, name);
                helpers.Add(name, player);
                await player.EnterWorldAsync();
            }
        foreach (string file in Directory.GetFiles(Path.Combine(qaRoot,"requests"), "*.json").Order())
        {
            if (handled.Contains(file)) continue;
            HeadlessPlayer? duelActor = null;
            try
            {
                using var request = JsonDocument.Parse(await File.ReadAllTextAsync(file));
                JsonElement q = request.RootElement;
                var actor = helpers[q.GetProperty("actor").GetString()!];
                string action = q.GetProperty("action").GetString()!;
                object result = "sent";
                if (q.TryGetProperty("delayMs", out var delay)) await Task.Delay(Math.Clamp(delay.GetInt32(), 0, 30000));
                if (action == "chat") actor.SendChat(q.GetProperty("text").GetString()!);
                else if (action == "cast" || action == "ranged" || action == "melee")
                {
                    var target = await actor.WaitForViewNamedAsync(q.GetProperty("target").GetString()!);
                    if (action == "cast") actor.SendRpc($"80 b9 {Le(q.GetProperty("ability").GetUInt16())} {Le(target.ViewId)}");
                    else if (action == "melee" && fixtureMode is NativeQa.Modes.MobiusCodeFixtureMode)
                    {
                        if (actor.Handle != "QAOther")
                            throw new InvalidOperationException("Use QAOther for Mobius melee and QAEnemy for rifle fire.");
                        result = await NativeQa.Modes.MobiusCodeFixtureMode.SendForcedEntryAsync(actor, target);
                    }
                    else if (action == "melee") actor.SendRpc($"40 {Le(target.ViewId)} {target.SpawnId:x2} 00 01 6b");
                    else actor.SendRpc($"41 {Le(target.ViewId)} {target.SpawnId:x2} 00");
                }
                else if (action == "status-debuff" && fixtureMode is NativeQa.Modes.AbilityStatusMode)
                    result = await NativeQa.Modes.AbilityStatusMode.CastDebuffAsync(actor, q.GetProperty("ability").GetUInt16());
                else if (action == "direct-heal-health" && fixtureMode is NativeQa.Modes.AbilityEvidenceDirectHealsMode && actor.Handle == "QAFriend")
                {
                    ushort health = await NativeQa.Modes.AbilityEvidenceDirectHealsMode.ReadHealthAsync(actor);
                    Console.WriteLine($"{actor.Handle} before heal: {health} HP");
                    result = new { actor.Handle, Health = health };
                }
                else if (action == "duel-accept")
                {
                    duelActor = actor;
                    result = await HelperDuelAcceptance.AcceptNextAsync(actor,q.GetProperty("challenger").GetString()!,onArmed:armed =>
                        File.WriteAllTextAsync(Path.Combine(qaRoot,"responses",Path.GetFileNameWithoutExtension(file)+".armed.json"),JsonSerializer.Serialize(new { Armed=true, State=armed, At=DateTime.UtcNow })));
                }
                else if (action == "approve")
                {
                    var pending = await actor.WaitForRpcAsync("81 88");
                    byte[] reply = fixtureMode is NativeQa.Modes.AbilityHealthParityMode
                        ? NativeQa.Modes.AbilityHealthParityMode.RejuvenateReply(pending.Bytes)
                        : new byte[54];
                    reply[0] = 0x81; reply[1] = 0x88;
                    Array.Copy(pending.Bytes, 2, reply, 2, 4);
                    reply[6] = q.TryGetProperty("timeout", out var timeout) && timeout.GetBoolean() ? (byte)1 : (byte)0;
                    reply[7] = q.GetProperty("accept").GetBoolean() ? (byte)0 : (byte)1;
                    if (fixtureMode is NativeQa.Modes.AbilityHealthParityMode && reply[6] == 0 && reply[7] == 0)
                    {
                        var maxima = await NativeQa.Modes.AbilityHealthParityMode.ReadMaximaAsync(actor);
                        Console.WriteLine($"Rejuvenate approval expects {actor.Handle} HP {maxima.Health} and IS {maxima.InnerStrength}.");
                        int viewCursor = actor.World.ViewSnapshot().Count;
                        actor.SendRpc(reply);
                        result = await NativeQa.Modes.AbilityHealthParityMode.CheckRejuvenatedAsync(actor, maxima, viewCursor);
                    }
                    else actor.SendRpc(reply);
                }
                else if (action == "invite") actor.SendRpc(Invite(q.GetProperty("target").GetString()!));
                else if (action == "accept") actor.SendRpc(Answer(q.GetProperty("inviter").GetString()!));
                else if (action == "trade" && fixtureMode is NativeQa.Modes.SecureTradeMode)
                    await NativeQa.Modes.SecureTradeMode.SendPartnerActionAsync(actor, q);
                else if (fixtureMode is NativeQa.Modes.SocialBreadthMode && action == "social")
                    result = await NativeQa.Modes.SocialBreadthMode.SendActionAsync(actor, q);
                else if (fixtureMode is NativeQa.Modes.TeamBreadthMode && action == "team-breadth")
                    result = await NativeQa.Modes.TeamBreadthMode.SendActionAsync(actor, q);
                else if ((fixtureMode is NativeQa.Modes.HackerParitySweepMode or NativeQa.Modes.HackerSecondPassMode) && action == "hacker-group-arrive")
                    result = await NativeQa.Modes.HackerParitySweepMode.ArriveAsync(helpers,
                        fixtureMode is NativeQa.Modes.HackerSecondPassMode ? NativeQa.Modes.HackerSecondPassMode.Stands : null);
                else if (fixtureMode is NativeQa.Modes.MarketplaceBreadthMode market && action == "marketplace")
                    result = await market.SendActionAsync(actor, q, helpers, database);
                else if (action == "stop") actor.SendRpc("44 00");
                else if (action == "move")
                {
                    var bytes = new List<byte>(WireFormat.FromHex("02 00 01 08"));
                    foreach(string field in new[]{"x","y","z"}) bytes.AddRange(BitConverter.GetBytes(q.GetProperty(field).GetSingle()));
                    bytes.AddRange(new byte[]{0,0}); actor.World.SendViewList(bytes.ToArray());
                }
                else throw new InvalidOperationException("Unknown helper action " + action);
                await File.WriteAllTextAsync(Path.Combine(qaRoot,"responses",Path.GetFileName(file)), JsonSerializer.Serialize(new { Ok=true, Result=result, At=DateTime.UtcNow }));
                handled.Add(file);
            }
            catch(Exception ex)
            {
                if(duelActor!=null)
                    await File.WriteAllTextAsync(Path.Combine(qaRoot,"responses",Path.GetFileName(file)),JsonSerializer.Serialize(new { Ok=false, Error=ex.Message, ErrorType=ex.GetType().FullName, At=DateTime.UtcNow,
                        Actor=duelActor.Handle, AllRpc=duelActor.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(), ReceivedViewLists=duelActor.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(), Problems=duelActor.World.Problems.ToArray() }));
                else
                await File.WriteAllTextAsync(Path.Combine(qaRoot,"responses",Path.GetFileName(file)), JsonSerializer.Serialize(new { Ok=false, Error=ex.Message, At=DateTime.UtcNow }));
                handled.Add(file);
            }
        }
        string[] lines = server.DebugLog().Split('\n');
        foreach (string line in lines.Skip(seen).Where(l => new[]{"[PVP","[DUEL","[DEV]","[COMBAT]","[COMBAT-VIEW]","[ABILITY]","[BUFF]","[EFFECTS]","[AURA]","[STEALTH]","[STEALTH-FREE]","[HOSTILE]","[SWEEP]","[RESURRECTION]","[HEALTH-CALLBACK]","[HEALTH-EFFECT]","[MOBIUS"}.Any(l.Contains)))
            await File.AppendAllTextAsync(Path.Combine(qaRoot,"events.jsonl"), JsonSerializer.Serialize(new { At=DateTime.UtcNow, Line=line.Trim() })+"\n");
        seen = Math.Max(0,lines.Length-1);
        await File.WriteAllTextAsync(Path.Combine(qaRoot,"server.log"),server.FullOutput());
        await Task.Delay(500);
    }
}
catch (Exception error)
{
    await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(new { Status = "Blocked", BuildCommit = commit, Error = error.Message }));
    if (server != null) await File.WriteAllTextAsync(Path.Combine(qaRoot, "server.log"), server.FullOutput());
    throw;
}
finally
{
    foreach(var player in helpers.Values) player.Dispose();
    server?.Dispose();
    await database.ExecuteAsync(@"
DELETE FROM reality_hd.char_missions WHERE charId=110;
INSERT INTO reality_hd.char_missions(charId,missionId) VALUES(110,986);
UPDATE reality_hd.characters SET x=23828,y=495,z=13984,district='slums',districtId=1,is_online=0,healthC=healthM,innerStrC=innerStrM,pvpflag=0 WHERE charId=110;
");
    string reset=await database.ScalarAsync("SELECT CONCAT(x,',',y,',',z,',health=',healthC,'/',healthM,',flag=',pvpflag) FROM reality_hd.characters WHERE charId=110");
    string missions=await database.ScalarAsync("SELECT GROUP_CONCAT(missionId ORDER BY missionId) FROM reality_hd.char_missions WHERE charId=110");
    await File.WriteAllTextAsync(Path.Combine(qaRoot,"stopped.json"),JsonSerializer.Serialize(new { Reset=reset,Missions=missions,At=DateTime.UtcNow }));
    await File.AppendAllTextAsync(handoff,$"\nUpdate {DateTime.Now:HH:mm} ET (Codex): Native ability server stopped; main CSR110 reset/verified {reset}, missions {missions}. Releasing mxo-client.lock. No other character was selected natively.\n");
}

static string Le(ushort value)=>WireFormat.ToHex(BitConverter.GetBytes(value));
static byte[] Sized(string text)=>BitConverter.GetBytes((ushort)(text.Length+1)).Concat(Encoding.ASCII.GetBytes(text)).Append((byte)0).ToArray();
static byte[] Invite(string target)
{
    var who=Sized(target);
    return new byte[]{0x6e,3,6,0}.Concat(BitConverter.GetBytes((ushort)(6+who.Length))).Concat(who).Concat(Sized("")).ToArray();
}
static byte[] Answer(string inviter)=>new byte[]{0x6f,3,8,0,0,0,0,0}.Concat(Sized(inviter)).ToArray();
