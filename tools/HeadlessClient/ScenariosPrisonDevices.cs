using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static void PrisonRequest(HeadlessPlayer p,ushort ability,byte slot=106)
    {
        var request=new byte[43];request[0]=0x80;request[1]=0xb9;
        BitConverter.GetBytes(ability).CopyTo(request,2);request[37]=slot;request[38]=1;
        p.SendRpc(WireFormat.ToHex(request));
    }
    private static async Task PrisonDeviceUse(ScenarioContext context)
    {
        var sessions=new List<object>();var databaseProof=new List<object>();HeadlessPlayer? current=null;Exception? primary=null;string step="start";
        void Record(HeadlessPlayer p)
        {
            sessions.Add(new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()});
            Check.Equal(0,p.World.Problems.Count,"Prison device parser clean");
        }
        try
        {
            foreach(ushort ability in new ushort[]{949,957})
            {
                step="prepare-"+ability;
                var p=current=await context.AddPlayerAsync(new CharacterSeed("Prison"+ability){Level=50,Experience=(int)hds.PlayerProgression.ExperienceForLevel(50),
                    Health=5000,MaxHealth=5000,InnerStrength=50,MaxInnerStrength=2000,Items=new[]{(1,hds.PrisonDevices.Item(ability),1),(2,ability==949?42723u:42722u,1)}});
                await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({p.CharId},1,2147485696,50,1)");
                if(ability==949)
                {
                    step="atomic-move-race";
                    var config=new hds.ServerConfig(Path.Combine(Path.GetDirectoryName(context.Server.SourceAssembly)!,"Config.xml.dist"));config.LoadDbParams();
                    Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal),"Owned disposable DB only");config.dbParams.DatabaseName=context.Database.Name;
                    var previous=hds.shared.Store.config;hds.shared.Store.config=config;
                    try
                    {
                        var access=new hds.databases.MyWorldDbAccess();var owned=access.ReadPrisonDevice(p.CharId,1);
                        using var barrier=new Barrier(2);
                        Task<bool> Move()=>Task.Run(()=>{Check.That(barrier.SignalAndWait(TimeSpan.FromSeconds(10)),"Concurrent move start barrier");return access.MovePrisonDevice(p.CharId,owned,106);});
                        var race=await Task.WhenAll(Move(),Move());Check.Equal(1,race.Count(x=>x),"Only one independent transaction can move the same owned row");
                        var equipped=access.ReadPrisonDevice(p.CharId,106);Check.Equal(owned.Identity,equipped.Identity,"Move retains physical row identity");
                        Check.That(access.MovePrisonDevice(p.CharId,equipped,1),"Restore before normal wire test");
                        databaseProof.Add(new{Step=step,Identity=owned.Identity,RaceAccepted=race.Count(x=>x),RestoredSlot=1});
                    }
                    finally{hds.shared.Store.config=previous;}
                }
                await p.EnterWorldAsync();
                if(ability==949)
                {
                    step="failed-move-no-ack";await context.Database.InstallPrisonMoveFailureAsync(p.CharId);
                    try
                    {
                        int acks=p.Received("65 01 00 6a 00").Count;p.SendRpc("63 01 6a");await p.WaitForTextAsync("device could not be moved");
                        p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");
                        Check.Equal(acks,p.Received("65 01 00 6a 00").Count,"Failed SQL publishes no move acknowledgement");
                        Check.Equal(1L,await context.Database.LongAsync($"SELECT slot FROM inventory WHERE charId={p.CharId} AND goid=42722"),"Failed SQL retains original item slot");
                        databaseProof.Add(new{Step=step,MoveAcknowledged=false,RetainedSlot=1});
                    }
                    finally{await context.Database.RemovePrisonMoveFailureAsync();}
                }
                Cast(p,ability);await p.WaitForRpcAsync($"80 ba {Le(ability)}");
                PrisonRequest(p,ability,1);await p.WaitForRpcAsync($"80 ba {Le(ability)}");
                Check.Equal(1L,await context.Database.LongAsync($"SELECT slot FROM inventory WHERE charId={p.CharId} AND goid={hds.PrisonDevices.Item(ability)}"),"Bag tool cannot activate");
                p.SendRpc("63 01 6a");await p.WaitForRpcAsync("65 01 00 6a 00");
                Check.Equal(106L,await context.Database.LongAsync($"SELECT slot FROM inventory WHERE charId={p.CharId} AND goid={hds.PrisonDevices.Item(ability)}"),"Normal equipment move");
                if(ability==957)
                {
                    step="cancel-pending";PrisonRequest(p,ability);await p.WaitForRpcAsync($"80 ac {Le(ability)}");
                    p.SendRpc("64 6a 03");await p.WaitForRpcAsync("65 6a 00 03 00");await Task.Delay(1800);
                    Check.Equal(0,p.Received(ModPattern(ability,144)).Count,"Unequip cancels pending cast");
                    p.SendRpc("63 03 6a");await p.WaitForRpcAsync("65 03 00 6a 00");
                    step="same-goid-replacement";PrisonRequest(p,ability);await p.WaitForRpcAsync($"80 ac {Le(ability)}");
                    long oldId=await context.Database.LongAsync($"SELECT invId FROM inventory WHERE charId={p.CharId} AND slot=106");
                    await context.Database.ExecuteAsync($"DELETE FROM inventory WHERE charId={p.CharId} AND slot=106; INSERT INTO inventory(charId,goid,slot,count,purity) VALUES({p.CharId},42723,106,1,0)");
                    long newId=await context.Database.LongAsync($"SELECT invId FROM inventory WHERE charId={p.CharId} AND slot=106");
                    await Task.Delay(1800);Check.That(oldId!=newId,"Different physical row with same GOID");
                    Check.Equal(0,p.Received(ModPattern(ability,144)).Count,"Same-GOID replacement cannot inherit a pending cast");
                    databaseProof.Add(new{Step=step,OldIdentity=oldId,NewIdentity=newId,BuffApplied=false});
                }
                step="activate-"+ability;PrisonRequest(p,ability);
                var on=await p.WaitForRpcAsync(ModPattern(ability,144));
                int initial=ability==949?1:2,increment=ability==949?2:3;
                Check.Equal(initial,BitConverter.ToInt32(on.Bytes,13),"Stored initial value");
                p.SendChat("?health 4900");
                var changed=await p.WaitForRpcAsync(ModPattern(ability,144));
                Check.Equal(initial+increment,BitConverter.ToInt32(changed.Bytes,13),"Positive health damage increments source value");
                int count=p.Received(ModPattern(ability,144)).Count;
                p.SendChat("?health 5000");p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");
                Check.Equal(count,p.Received(ModPattern(ability,144)).Count,"Healing does not increment");
                if(ability==949)
                {
                    step="expiry";await p.WaitForRpcAsync(ModPattern(ability,144,true),TimeSpan.FromSeconds(32));
                }
                else
                {
                    step="unequip-active";p.SendRpc("64 6a 03");await p.WaitForRpcAsync("65 6a 00 03 00");
                    var off=await p.WaitForRpcAsync(ModPattern(ability,144,true));
                    Check.Equal(initial+increment,BitConverter.ToInt32(off.Bytes,13),"Term removes accumulated value");
                }
                Check.Equal(1L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND goid={hds.PrisonDevices.Item(ability)}"),"Activation never consumes device");
                PrisonRequest(p,ability);await p.WaitForRpcAsync($"80 ba {Le(ability)}");
                if(ability==949)
                {
                    p.SendRpc("64 6a 03");await p.WaitForRpcAsync("65 6a 00 03 00");
                    p.SendRpc("63 02 6a");await p.WaitForRpcAsync("65 02 00 6a 00");
                    PrisonRequest(p,957);await p.WaitForRpcAsync("80 ba bd 03");
                    Check.Equal(0,p.Received(ModPattern(957,144)).Count,"Shared family cooldown survives device replacement");
                }
                Record(p);p.Dispose();current=null;
            }
            step="complete";
        }
        catch(Exception e){primary=e;throw;}
        finally
        {
            Exception? proofFailure=null;
            try{if(current!=null)Record(current);}catch(Exception e){proofFailure=e;}
            string stem=Path.Combine(Path.GetTempPath(),"mxo-prison-devices-"+context.Database.Name);
            try
            {
                string dll=context.Server.SourceAssembly;
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Step=step,Failure=primary?.ToString()??proofFailure?.ToString(),Database=context.Database.Name,DatabaseProof=databaseProof,Sessions=sessions,SourceAssembly=dll,
                    SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),AcquisitionImplemented=false},new JsonSerializerOptions{WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog());
                Console.WriteLine("  PRISON evidence: "+stem+" step="+step);
            }
            catch(Exception e){proofFailure??=e;Console.Error.WriteLine(e);}
            finally{current?.Dispose();}
            if(primary==null&&proofFailure!=null)throw new ScenarioFailure("Required Prison evidence failed: "+proofFailure);
        }
    }
}
