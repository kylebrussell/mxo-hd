using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task SharpshooterWire(ScenarioContext context)
    {
        string pin=Environment.GetEnvironmentVariable("MXO_SHARPSHOOTER_SOURCE_SHA256")??throw new InvalidOperationException("Sharpshooter source pin required");
        Check.Equal(pin,context.Server.SourceAssemblySha256,"Actual source before authentication");
        var shooter=await context.AddPlayerAsync(SharpshooterFixture.Seed("SharpShooter",17000,2300,true));
        var close=await context.AddPlayerAsync(SharpshooterFixture.Seed("SharpFists",17120,2300));
        var far=await context.AddPlayerAsync(SharpshooterFixture.Seed("SharpFar",17500,2300));
        var gun=await context.AddPlayerAsync(SharpshooterFixture.Seed("SharpGun",17000,2420,gun:true));
        var ally=await context.AddPlayerAsync(SharpshooterFixture.Seed("SharpAlly",16880,2300) with{Organization=1});
        var players=new[]{shooter,close,far,gun,ally};var measurements=new List<object>();bool passed=false;string stage="entry";Exception? failure=null;
        try
        {
            foreach(var p in players)await PvpOpponentsFixture.EnterAsync(p);
            async Task Equip(HeadlessPlayer p)
            {
                p.SkipReceived();p.SendRpc("63 0a 69");await p.WaitForRpcAsync("65 0a 00 69 00 00");
                Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND slot=105 AND goid=537"),"Owned normal pistol equip SQL");
            }
            await Equip(shooter);await Equip(gun);shooter.SendRpc("3f 69 00 10 00 00 00");
            string parents=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={shooter.CharId} AND ability_id<>2147635200 ORDER BY slot,id");
            async Task Loaded(bool on)
            {
                shooter.SkipReceived();SetAbilityLoaded(shooter,SharpshooterFixture.Slot,148,50,on);
                await shooter.WaitForRpcAsync(on?"80 b2 94 00":"80 b3 94 00");
                Check.Equal(on?1L:0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={shooter.CharId} AND slot={SharpshooterFixture.Slot} AND ability_id=2147635200"),"Normal148 owned loaded state");
                Check.Equal(parents,await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={shooter.CharId} AND ability_id<>2147635200 ORDER BY slot,id"),"Same parent loadout");
            }
            int? baseAttack=null;
            async Task Fire(HeadlessPlayer target,bool loaded,int expected,bool sampled,int opponents)
            {
                var view=await shooter.WaitForViewNamedAsync(target.Handle);shooter.SkipReceived();target.SkipReceived();int start=context.Server.DebugLog().Length;
                shooter.SendRpc("3d 01");shooter.SendRpc("41 "+HandleOf(view));
                await shooter.WaitForRpcAsync("4e 00");
                var own=await shooter.WaitForRpcAsync(r=>SharpshooterFixture.ReadShot(r.Bytes,shooter.Handle,target.Handle,true,out _),"Complete own ordinary8168 shot");
                var other=await target.WaitForRpcAsync(r=>SharpshooterFixture.ReadShot(r.Bytes,shooter.Handle,target.Handle,false,out _),"Complete target ordinary8168 shot");
                SharpshooterFixture.ReadShot(own.Bytes,shooter.Handle,target.Handle,true,out var ownValues);SharpshooterFixture.ReadShot(other.Bytes,shooter.Handle,target.Handle,false,out var otherValues);
                Check.Equal(ownValues,otherValues,"Independent wire attack/defense/damage agrees for both actors");
                var input=context.Server.DebugLog()[start..].Split('\n').Select(l=>SharpshooterFixture.ReadInput(l.TrimEnd('\r'),shooter.Handle)).FirstOrDefault(i=>i!=null)??throw new InvalidOperationException("Current shot input diagnostic missing");
                Check.Equal((byte)0x38,input.Class,"Actual owned pistol class");Check.Equal(loaded,input.Loaded,"Current148 membership");Check.Equal(expected,input.Modifier,"Recovered literal modifier");
                Check.Equal(sampled,input.CountSampled,"Only relevant opponent count scanned");if(sampled)Check.Equal(opponents,input.Opponents,"Distinct current live opponents");
                baseAttack??=input.Before;Check.Equal(baseAttack.Value,input.Before,"Same actual base attack before/after148; no invented stat");
                int stopStart=context.Server.DebugLog().Length;shooter.SendRpc("3d 00");
                await shooter.WaitUntilAsync(()=>context.Server.DebugLog()[stopStart..].Contains("[COMBAT] Combat stance idle"),"Ordinary holster completes after stopping the fire timer",TimeSpan.FromSeconds(10));
                measurements.Add(new{Stage=stage,Target=target.Handle,Input=input,Wire=ownValues,OwnRpc=Convert.ToHexString(own.Bytes),TargetRpc=Convert.ToHexString(other.Bytes)});
            }
            stage="148-off-close";await Loaded(false);await Fire(close,false,-5,true,1);
            stage="148-on-close";await Loaded(true);await Fire(close,true,0,false,0);
            stage="148-on-gun-target";await Fire(gun,true,0,false,0);
            stage="148-on-longrange";await Fire(far,true,5,false,0);
            stage="148-unload-close";await Loaded(false);await Fire(close,false,-5,true,1);
            stage="148-off-longrange";await Fire(far,false,5,true,1);
            stage="ordinary-other-melee";var closeForAlly=await ally.WaitForViewNamedAsync(close.Handle);ally.SkipReceived();close.SkipReceived();
            ally.SendRpc("40 "+HandleOf(closeForAlly)+" 01 6b");await ally.WaitForRpcAsync("47",TimeSpan.FromSeconds(10));
            stage="148-off-into-melee";await Fire(close,false,-20,true,2);
            stage="148-on-into-melee";await Loaded(true);await Fire(close,true,0,false,0);
            ally.SkipReceived();close.SkipReceived();ally.SendRpc("44 00");close.SendRpc("44 00");await ally.WaitForRpcAsync("46",TimeSpan.FromSeconds(20));await close.WaitForRpcAsync("46",TimeSpan.FromSeconds(20));
            foreach(var p in players)Check.Equal(0,p.World.Problems.Count,"Each parser clean");passed=true;stage="complete";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try
            {
                string file=Path.Combine(Path.GetTempPath(),"mxo-sharpshooter-"+context.Database.Name+".json");
                await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),ExpectedSource=pin,ActualSource=context.Server.SourceAssemblySha256,
                    Source=context.Server.SourceAssembly,SourceFileSha=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),ServerPid=context.Server.ProcessId,Schema=context.Database.Name,Workdir=context.Server.WorkingDirectory,
                    Measurements=measurements,AbilityRows=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={shooter.CharId} ORDER BY slot,id"),
                    Sessions=players.Select(p=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems}),
                    Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),Policy="Recovered gun-vs-fists math; OURS ordinary free-fire/range/opponent mapping",RngOverrides=false,NativeProven=false},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("Sharpshooter proof: "+file);
            }
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine(proofError);}
        }
    }
}
