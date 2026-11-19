using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using hds;

namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task MobiusPositive(ScenarioContext c)=>MobiusFreeAttack(c,true);
    private static Task MobiusNegative(ScenarioContext c)=>MobiusFreeAttack(c,false);
    private static async Task MobiusFreeAttack(ScenarioContext context,bool positive)
    {
        // This mode alone needs the explicitly guarded CSR110 callback control. Clone only disposable rows,
        // before its FIRST Margin login; the ordinary ScenarioContext/default character allocator stays unchanged.
        Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal),"Disposable Mobius fixture");
        await context.Database.ExecuteAsync($@"
INSERT INTO users(userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
 SELECT 990110,'mobiuscsr',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId=28;
DELETE FROM inventory WHERE charId=110; DELETE FROM char_abilities WHERE char_id=110;
DELETE FROM char_missions WHERE charId=110; DELETE FROM rsivalues WHERE charid=110; DELETE FROM characters WHERE charId=110;
CREATE TEMPORARY TABLE mobius_char AS SELECT * FROM characters WHERE charId=2;
UPDATE mobius_char SET charId=110,userId=990110,handle='MobiusCSR',x=17000,y=495,z=2300,rotation=0,district='slums',districtId=1,
 healthC=2000,healthM=5000,innerStrC=1000,innerStrM=1000,level=50,exp={PlayerProgression.ExperienceForLevel(50)},alignment=1,pvpflag=1,crewId=0,factionId=0,is_online=0;
INSERT INTO characters SELECT * FROM mobius_char; DROP TEMPORARY TABLE mobius_char;
CREATE TEMPORARY TABLE mobius_rsi AS SELECT * FROM rsivalues WHERE charid=2;
UPDATE mobius_rsi SET charid=110; INSERT INTO rsivalues SELECT * FROM mobius_rsi; DROP TEMPORARY TABLE mobius_rsi;
INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES
(110,400,{0x80000000L|2L<<10},1,1,'Awakened'),(110,401,{0x80000000L|967L<<10},1,1,'Mobius Code');
");
        using var victim=await HeadlessPlayer.LoginAsync(context.Server.Address,"mobiuscsr","MobiusCSR");
        var shooter=await context.AddPlayerAsync(new CharacterSeed("MobiusShooter") {X=17300,Y=495,Z=2300,Level=50,
            Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=2,Health=5000,MaxHealth=5000,
            Items=new[]{(105,692u,1)},OwnedLoadedAbilities=new[]{((ushort)2,(ushort)1)}});
        var challenger = positive ? await context.AddPlayerAsync(new CharacterSeed("MobiusChallenger")
        {
            X=16900,Y=495,Z=2300,Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),
            Organization=2,Health=5000,MaxHealth=5000,OwnedLoadedAbilities=new[]{((ushort)2,(ushort)1)}
        }) : null;
        bool passed=false;string step="entry";Exception? failure=null;
        try
        {
            await victim.EnterWorldAsync();await shooter.EnterWorldAsync();victim.SendRpc("0c 01");shooter.SendRpc("0c 01");
            victim.SendChat("?pvpflag on");shooter.SendChat("?pvpflag on");
            await victim.WaitForViewUpdateAsync(2,OwnPvpFlagOn);await shooter.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
            var target=await shooter.WaitForViewNamedAsync(victim.Handle);var attacker=await victim.WaitForViewNamedAsync(shooter.Handle);
            string? forcedEntry = null;
            string? refusedEntry = null;
            if (challenger != null)
            {
                await challenger.EnterWorldAsync();
                challenger.SendChat("?pvpflag on");
                await challenger.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
                string handle = HandleOf(await challenger.WaitForViewNamedAsync(victim.Handle));
                // Retail 40 <view,spawn> 01 6b, with the live handle in the decoded field.
                forcedEntry = "40 " + handle + " 01 6b";
                refusedEntry = "4d 0e 00 01 00 " + handle;
            }
            string branch=positive?"positive":"negative";
            victim.SendChat("?combatdepth mobius "+branch+" next");await victim.WaitForTextAsync("Mobius callback chance armed once: "+branch);
            Check.That(!context.Server.DebugLog().Contains("[MOBIUS-QA] callback-only one-shot consumed"),"Arming itself cannot install protection");
            step="ordinary-incoming-fire";shooter.SendRpc("3d 01");shooter.SendRpc("41 "+HandleOf(target));
            await victim.WaitForRpcAsync("81 68");
            int viewCursor=victim.World.ViewSnapshot().Count, rpcCursor=victim.World.RpcSnapshot().Count;
            ushort healthBefore=RefusalOwnValue(victim,"Health","02 80 80 80 40");
            ushort inner=RefusalOwnValue(victim,"InnerStrengthAvailable","02 80 80 80 10");
            step="normal967-callback";
            // abilities_mobiuscode stock capture, lines 10–11: original target/ground words and slot bytes.
            victim.SendRpc("80 b9 c7 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 e0 c7 05 64 41 08 6b 00 00 00 00 00");
            //967 is instant. Its sourced callback and actual600/10 timers are the barriers, not a cast-bar packet.
            await victim.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[MOBIUS] PARTIAL967 callback roll="+(positive?0:99)+" proc="+positive),"Actual967 landed producer",TimeSpan.FromSeconds(10));
            var interval=System.Diagnostics.Stopwatch.StartNew();
            if (challenger != null)
            {
                challenger.SendRpc(forcedEntry!);
                await challenger.WaitForRpcAsync(refusedEntry!);
            }
            string fxBytes=positive ? "10 31 46 12" : "12 31 46 12";
            await victim.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 "+fxBytes+" ??");
            await shooter.WaitForViewUpdateAsync(target.ViewId, "02 80 80 80 0c "+fxBytes+" ??");
            await victim.WaitForRpcAsync("80 bd 01 c7 03 04 00 00 16 44 01");
            await victim.WaitForRpcAsync("80 bd 01 c7 02 00 00 00 20 41 01");
            Check.That(context.Server.DebugLog().Contains("Heal 967 -> MobiusCSR for 500"),"Unchanged maxHP/10 healing");
            bool HealDeltaReceived()
            {
                var health=new[]{healthBefore}.Concat(ViewUpdateValues(victim,2,"02 80 80 80 40",viewCursor)).ToArray();
                return health.Zip(health.Skip(1),(a,b)=>(int)b-a).Contains(500);
            }
            await victim.WaitUntilAsync(HealDeltaReceived,"Independent received500 health delta; regeneration remains separate",TimeSpan.FromSeconds(5));
            await victim.WaitUntilAsync(()=>ViewUpdateValues(victim,2,"02 80 80 80 10",viewCursor).Contains((ushort)(inner-5)),"Unchanged five IS debit",TimeSpan.FromSeconds(5));
            var timers=victim.World.RpcSnapshot().Skip(rpcCursor).Where(r=>r.Bytes.Length>=11&&r.Bytes[0]==0x80&&r.Bytes[1]==0xbd).ToArray();
            Check.That(timers.Any(r=>r.Bytes[3]==0xc7&&r.Bytes[4]==3&&BitConverter.ToSingle(r.Bytes,6)==600),"Independent ordinary967 timer600");
            Check.That(timers.Any(r=>r.Bytes[3]==0xc7&&r.Bytes[4]==2&&r.Bytes[5]==0&&BitConverter.ToSingle(r.Bytes,6)==10&&r.Bytes[10]==1),"Independent ordinary family711 timer10");
            string log=context.Server.DebugLog();int callback=log.IndexOf("[MOBIUS] PARTIAL967 callback",StringComparison.Ordinal);
            // Inspect a bounded interval well inside the proc, with ordinary rolls. No RNG retry.
            Check.That(interval.Elapsed<TimeSpan.FromSeconds(8),"Wire accounting completed inside the bounded proc interval");
            await Task.Delay(TimeSpan.FromSeconds(8)-interval.Elapsed);log=context.Server.DebugLog()[callback..];
            var shots=Regex.Matches(log,@"MobiusShooter fires at MobiusCSR: (miss|hit for \d+) \(roll (\d+) vs (\d+)\)");
            Check.That(shots.Count>=2,"Existing fire continues without a second41");
            if(positive)
            {
                Check.That(shots.All(m=>m.Groups[1].Value=="miss"),"Protected shots use ordinary failed outcome");
                Check.That(shots.Any(m=>int.Parse(m.Groups[2].Value)>int.Parse(m.Groups[3].Value)),"Observed winning contest forced to failed outcome; otherwise this one pass is inconclusive");
                Check.That(victim.World.RpcSnapshot().Skip(rpcCursor).All(r=>r.Bytes.Length<2||r.Bytes[0]!=0x80||r.Bytes[1]!=0xbc),"No invented modifier/presentation record");
            }
            else Check.That(shots.Any(m=>m.Groups[1].Value.StartsWith("hit for ",StringComparison.Ordinal)),"Negative callback leaves ordinary incoming damage");
            int continued=CountViewUpdate(victim,attacker.ViewId,"032a");
            Check.That(continued>=2,"Ordinary received attack animations continue");
            step=positive?"natural-expiry":"negative-continuation";int after=context.Server.DebugLog().Length;
            await victim.WaitUntilAsync(()=>Regex.IsMatch(context.Server.DebugLog()[after..],@"MobiusShooter fires at MobiusCSR: hit for [1-9]"),positive?"Natural expiry restores hits without a second41":"Negative branch continues normal damage without a second41",TimeSpan.FromSeconds(30));
            Check.Equal(1,context.Server.DebugLog().Split("[MOBIUS-QA] callback-only one-shot consumed").Length-1,"Exactly one producer callback override");
            Check.Equal(0,victim.Received("47").Count+shooter.Received("47").Count,"Freefire does not become interlock");
            shooter.SendRpc("44 00");
            if (challenger != null)
            {
                challenger.SendRpc(forcedEntry!);
                await challenger.WaitForRpcAsync("47");
                Check.Equal(1,challenger.Received(refusedEntry!).Count,"One forced refusal during proc followed by entry after expiry");
                Check.Equal(0,challenger.World.Problems.Count,"Challenger session parser clean");
            }
            Check.Equal(0,victim.World.Problems.Count+shooter.World.Problems.Count,"All session parsers clean");
            step="complete";passed=true;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            string proof=Path.Combine(Path.GetTempPath(),"mxo-mobius-"+context.Database.Name+".json");
            try {await File.WriteAllTextAsync(proof,JsonSerializer.Serialize(new {Passed=passed,Step=step,Positive=positive,Failure=failure?.ToString(),
                Source=context.Server.SourceAssembly,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),
                Sessions=new[]{victim,shooter,challenger}.OfType<HeadlessPlayer>().Select(p=>new{p.Handle,p.CharId,Rpcs=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems}),
                Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),NativeUiProven=false,Partial=true},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Mobius proof: "+proof);}
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine(proofError);}
        }
    }
}
