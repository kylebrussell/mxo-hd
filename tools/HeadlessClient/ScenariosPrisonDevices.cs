using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static void PrisonRequest(HeadlessPlayer p,ushort ability,byte slot=106)
    {
        p.SendRpc(WireFormat.ToHex(PrisonDeviceWire.Activate(ability,slot)));
    }
    private static async Task PrisonDenied(HeadlessPlayer p,ushort ability,Action send)
    {
        string pattern=$"80 ba {Le(ability)}";int before=p.Received(pattern).Count;
        p.SkipReceived();send();await p.WaitForRpcAsync(pattern);
        Check.Equal(before+1,p.Received(pattern).Count,"A new denial answers this request, not historical traffic");
    }
    private static async Task PrisonDeviceUse(ScenarioContext context)
    {
        var sessions=new List<object>();var databaseProof=new List<object>();var resourceProof=new List<object>();HeadlessPlayer? current=null;Exception? primary=null;string step="start";
        void Record(HeadlessPlayer p)
        {
            sessions.Add(new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray(),OrderedInnerStrength=ViewUpdateValues(p,2,"02 80 80 80 10").ToArray()});
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
                await PrisonDenied(p,ability,()=>Cast(p,ability));
                await PrisonDenied(p,ability,()=>PrisonRequest(p,ability,1));
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
                // Preserve ordered absolute resource records; normal regeneration is +8 and is not disabled.
                await p.WaitUntilAsync(()=>ViewUpdateValues(p,2,"02 80 80 80 10").Count>0,"An actual IS update anchors terminal accounting",TimeSpan.FromSeconds(10));
                int terminalStart=ViewUpdateValues(p,2,"02 80 80 80 10").Count;
                int expectedRefund=ability==949?6:10;
                resourceProof.Add(new{Ability=ability,Stage="before-terminal",ExpectedRefund=expectedRefund,Updates=ViewUpdateValues(p,2,"02 80 80 80 10").ToArray()});
                if(ability==949)
                {
                    step="expiry";await p.WaitForRpcAsync(ModPattern(ability,144,true),TimeSpan.FromSeconds(32));
                }
                else
                {
                    step="unequip-active";
                    int responseStart=p.World.RpcSnapshot().Count;
                    var movePattern=BytePattern.Parse("65 6a 00 03 00");var offPattern=BytePattern.Parse(ModPattern(ability,144,true));
                    bool CorrectOff(RpcRecord r)=>offPattern.IsMatch(r.Bytes) && BitConverter.ToInt32(r.Bytes,13)==initial+increment;
                    p.SendRpc("64 6a 03");
                    await p.WaitUntilAsync(()=>
                    {
                        var replies=p.World.RpcSnapshot().Skip(responseStart).ToArray();
                        return replies.Any(r=>movePattern.IsMatch(r.Bytes)) && replies.Any(CorrectOff);
                    },"This unequip emits its move acknowledgement and accumulated buff-off in either order",TimeSpan.FromSeconds(10));
                    var off=p.World.RpcSnapshot().Skip(responseStart).Single(CorrectOff);
                    Check.Equal(initial+increment,BitConverter.ToInt32(off.Bytes,13),"Term removes accumulated value");
                }
                int refundPair=-1;
                await p.WaitUntilAsync(()=>
                {
                    var values=ViewUpdateValues(p,2,"02 80 80 80 10");
                    refundPair=Enumerable.Range(Math.Max(0,terminalStart-1),Math.Max(0,values.Count-Math.Max(0,terminalStart-1)-1))
                        .FirstOrDefault(i=>values[i+1]-values[i]==expectedRefund,-1);
                    return refundPair>=0;
                },"Exact terminal IS refund distinct from +8 regeneration",TimeSpan.FromSeconds(4));
                var resourceValues=ViewUpdateValues(p,2,"02 80 80 80 10");
                Check.Equal(expectedRefund,resourceValues[refundPair+1]-resourceValues[refundPair],"Actual ordered wire resource delta");
                Check.That(resourceValues[refundPair+1]<2000,"Refund witness is not hidden by the IS cap");
                if(ability==949)Check.That(resourceValues.Zip(resourceValues.Skip(1),(a,b)=>b-a).Any(delta=>delta==8),"Ordinary regeneration remains independently visible");
                resourceProof.Add(new{Ability=ability,Stage="terminal",ExpectedRefund=expectedRefund,PairIndex=refundPair,Before=resourceValues[refundPair],After=resourceValues[refundPair+1],Updates=resourceValues.ToArray()});
                Check.Equal(1L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND goid={hds.PrisonDevices.Item(ability)}"),"Activation never consumes device");
                await PrisonDenied(p,ability,()=>PrisonRequest(p,ability));
                if(ability==949)
                {
                    p.SendRpc("64 6a 03");await p.WaitForRpcAsync("65 6a 00 03 00");
                    p.SendRpc("63 02 6a");await p.WaitForRpcAsync("65 02 00 6a 00");
                    await PrisonDenied(p,957,()=>PrisonRequest(p,957));
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
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Step=step,Failure=primary?.ToString()??proofFailure?.ToString(),Database=context.Database.Name,DatabaseProof=databaseProof,ResourceProof=resourceProof,Sessions=sessions,SourceAssembly=dll,
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
