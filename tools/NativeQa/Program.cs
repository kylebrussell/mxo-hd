using HeadlessClient;
using hds;
using System.Text;
using System.Text.Json;

// Server/data preparation only. The real client's inputs are supplied through Computer Use.
string root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
string qaRoot = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "mxo-native-ability-qa"));
string commit = args.Length > 2 ? args[2] : "local";
bool npcStart = args.Length > 3 && args[3] == "npc";
bool outdoorStart = args.Length > 3 && args[3] == "outdoor";
int nativeX = outdoorStart ? 26000 : npcStart ? 24800 : 23828, nativeZ = npcStart || outdoorStart ? -1498 : 13984;
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
await File.AppendAllTextAsync(handoff, $"\nUpdate {DateTime.Now:HH:mm} ET (Codex): Took mxo-client.lock for native QA, build {commit}. Native account {account} exposes only CSR_Tester110 copied into a disposable database; helper players are headless QA fixtures. Main110 will be reset to Mara, full health, flag0 and mission986 only before release.\n");
using ThrowawayDatabase database = await ThrowawayDatabase.CreateAsync(root, "native_ability");
ServerInstance? server = null;
var helpers = new Dictionary<string, HeadlessPlayer>(StringComparer.OrdinalIgnoreCase);
var helperNames = new[] { "QAFriend", "QAFar", "QAOther", "QAEnemy" };
try
{
    await database.ExecuteAsync(@"
INSERT INTO users (userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
SELECT 990110,'CSR_AbilityQA',publicExponent,publicModulus,privateExponent,0,passwordmd5
FROM reality_hd.users WHERE userId=(SELECT userId FROM reality_hd.characters WHERE charId=110);
DELETE FROM characters WHERE charId=110;
INSERT INTO characters SELECT * FROM reality_hd.characters WHERE charId=110;
UPDATE characters SET userId=990110,level=50,healthC=100,healthM=500,innerStrC=200,innerStrM=200,
 x=23828,y=495,z=13984,rotation=126,district='slums',districtId=1,alignment=1,repMero=100,
 crewId=0,factionId=0,is_online=0,pvpflag=0 WHERE charId=110;
DELETE FROM rsivalues WHERE charid=110;
INSERT INTO rsivalues SELECT * FROM reality_hd.rsivalues WHERE charid=110;
DELETE FROM inventory WHERE charId=110;
INSERT INTO inventory (charId,goid,slot,count,purity)
 SELECT 110,goid,slot,count,purity FROM reality_hd.inventory WHERE charId=110;
CREATE TABLE IF NOT EXISTS char_missions
 (charId INT UNSIGNED NOT NULL,missionId INT NOT NULL,completions INT NOT NULL DEFAULT 1,PRIMARY KEY(charId,missionId));
DELETE FROM char_missions WHERE charId=110;
INSERT INTO char_missions (charId,missionId) VALUES (110,986);
DELETE FROM char_abilities WHERE char_id=110;
");
    ushort[] abilities = { 2,17,18,52,128,137,155,1006,77,967,169,371,1064,1067,1089,383,315,333,254,1135,48,352,359,466,329,355,870,450,433,12,184,217 };
    using var catalog = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "data", "ability_catalog.json")));
    var names = catalog.RootElement.GetProperty("abilities").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetUInt16(), e => e.GetProperty("name").GetString()!);
    string loadout = string.Join("\n", abilities.Distinct().Select((id, slot) =>
        $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES(110,{slot},{0x80000000L | ((long)id << 10)},50,1,'{names.GetValueOrDefault(id, "QA").Replace("'", "''")}');"));
    await database.ExecuteAsync(loadout);
    // The native client's login level is derived from XP, independently of the server's stored Level.
    await database.ExecuteAsync($"UPDATE characters SET exp={PlayerProgression.ExperienceForLevel(50)} WHERE charId=110");
    await database.ExecuteAsync($"UPDATE characters SET x={nativeX},z={nativeZ} WHERE charId=110");
    // Helpers are cloned solely from the disposable CSR record, never from another main-DB character.
    for (int i = 0; i < helperNames.Length; i++)
    {
        int id = 6111 + i; // rsivalues.charid is narrower than characters.charId in the legacy schema
        int x = nativeX + (i == 1 ? -1200 : i == 2 ? 0 : i == 3 ? -226 : -278);
        int z = nativeZ + (i == 2 ? 400 : i == 3 ? -107 : 0);
        await database.ExecuteAsync($@"
INSERT INTO users(userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
 SELECT {id},'{helperNames[i]}',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId=990110;
CREATE TEMPORARY TABLE qa_char AS SELECT * FROM characters WHERE charId=110;
UPDATE qa_char SET charId={id},userId={id},handle='{helperNames[i]}',x={x},z={z},healthC={(i==3?5000:100)},healthM={(i==3?5000:500)},alignment={(i==3?2:1)},pvpflag={(i==3?1:0)};
INSERT INTO characters SELECT * FROM qa_char;
DROP TEMPORARY TABLE qa_char;
CREATE TEMPORARY TABLE qa_rsi AS SELECT * FROM rsivalues WHERE charid=110;
UPDATE qa_rsi SET charid={id};
INSERT INTO rsivalues SELECT * FROM qa_rsi;
DROP TEMPORARY TABLE qa_rsi;
INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name)
 SELECT {id},slot,ability_id,level,is_loaded,ability_name FROM char_abilities WHERE char_id=110;
");
    }
    string snapshot = Path.Combine(qaRoot, "snapshot");
    string sourceBin = Path.Combine(root, "hds", "bin", "Debug", "net8.0");
    string targetBin = Path.Combine(snapshot, "hds", "bin", "Debug", "net8.0");
    foreach (string file in Directory.EnumerateFiles(sourceBin, "*", SearchOption.AllDirectories))
    {
        string target = Path.Combine(targetBin, Path.GetRelativePath(sourceBin, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target, true);
    }
    server = await ServerInstance.StartAsync(snapshot, database.Name, new Dictionary<string,string>
    {
        ["MXO_AUTH_PORT"]="11000", ["MXO_MARGIN_PORT"]="10000", ["MXO_WORLD_PORT"]="10000",
        ["MXO_CONSOLE_PORT"]="55557", ["MXO_MOBS"]="test", ["MXO_MOB_WANDER"]="0", ["MXO_AGGRO"]="0", ["MXO_PACKET_LOG"]="1"
    });
    await File.WriteAllTextAsync(Path.Combine(qaRoot,"state.json"), JsonSerializer.Serialize(new
    { Database=database.Name, Directory=server.WorkingDirectory, Account=account, Character="CSR_Tester", CharId=nativeId, BuildCommit=commit, Status="Ready", Abilities=abilities }, new JsonSerializerOptions { WriteIndented=true }));
    Console.WriteLine("Native ability QA ready: CSR_AbilityQA / placeholder password / CSR_Tester110.");
    int seen = 0;
    var handled = new HashSet<string>();
    DateTime deadline = DateTime.UtcNow.AddHours(4);
    while (!File.Exists(Path.Combine(qaRoot,"stop")) && DateTime.UtcNow < deadline && !server.HasExited)
    {
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
            try
            {
                using var request = JsonDocument.Parse(await File.ReadAllTextAsync(file));
                JsonElement q = request.RootElement;
                var actor = helpers[q.GetProperty("actor").GetString()!];
                string action = q.GetProperty("action").GetString()!;
                string result = "sent";
                if (q.TryGetProperty("delayMs", out var delay)) await Task.Delay(Math.Clamp(delay.GetInt32(), 0, 30000));
                if (action == "chat") actor.SendChat(q.GetProperty("text").GetString()!);
                else if (action == "cast" || action == "ranged" || action == "melee")
                {
                    var target = await actor.WaitForViewNamedAsync(q.GetProperty("target").GetString()!);
                    if (action == "cast") actor.SendRpc($"80 b9 {Le(q.GetProperty("ability").GetUInt16())} {Le(target.ViewId)}");
                    else if (action == "melee") actor.SendRpc($"40 {Le(target.ViewId)} {target.SpawnId:x2} 00 01 6b");
                    else actor.SendRpc($"41 {Le(target.ViewId)} {target.SpawnId:x2} 00");
                }
                else if (action == "invite") actor.SendRpc(Invite(q.GetProperty("target").GetString()!));
                else if (action == "accept") actor.SendRpc(Answer(q.GetProperty("inviter").GetString()!));
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
                await File.WriteAllTextAsync(Path.Combine(qaRoot,"responses",Path.GetFileName(file)), JsonSerializer.Serialize(new { Ok=false, Error=ex.Message, At=DateTime.UtcNow }));
                handled.Add(file);
            }
        }
        string[] lines = server.DebugLog().Split('\n');
        foreach (string line in lines.Skip(seen).Where(l => new[]{"[PVP","[DUEL","[DEV]","[COMBAT]","[COMBAT-VIEW]","[ABILITY]","[BUFF]","[EFFECTS]"}.Any(l.Contains)))
            await File.AppendAllTextAsync(Path.Combine(qaRoot,"events.jsonl"), JsonSerializer.Serialize(new { At=DateTime.UtcNow, Line=line.Trim() })+"\n");
        seen = Math.Max(0,lines.Length-1);
        await File.WriteAllTextAsync(Path.Combine(qaRoot,"server.log"),server.FullOutput());
        await Task.Delay(500);
    }
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
