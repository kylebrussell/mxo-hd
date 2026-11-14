using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MachinesDisturbingTrends(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("DisturbingTrends"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=2,
            X=17000,Y=495,Z=2300,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        await context.Database.ExecuteAsync(DisturbingTrendsFixture.SeedPrerequisites(p.CharId));
        uint id=p.CharId;string handle=p.Handle,step="world-entry";Exception? primary=null;var checkpoints=new List<object>();var sessions=new List<object>();var npcs=new List<object>();
        var options=new JsonSerializerOptions{IncludeFields=true};var mission=XElement.Load(Path.Combine(context.RepositoryRoot,"data","missions","machines_1_2_1_disturbing_trends.xml"));
        async Task<MissionProgress.SavedState> Saved(){var s=await MissionCheckpoint(context,p);checkpoints.Add(new{Step=step,State=s});return s;}
        void Record(){sessions.Add(new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()});Check.Equal(0,p.World.Problems.Count,"Every owned session parser clean");}
        async Task<ViewCreation> Ophiuchi(int number)
        {
            var def=mission.Elements("npc").Single(n=>(int)n.Attribute("id")! ==number);
            // Resolve latest mission placement and side across relog; do not reuse a historical same-name view.
            var v=await PrimaNpc(context,p,def,0,false);npcs.Add(new{Definition=number,Step=step,v.ViewId,v.SpawnId,v.Name,Attributes=v.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value))});return v;
        }
        try
        {
            await p.EnterWorldAsync();step="normal-offer";int cursor=p.World.RpcSnapshot().Count;p.SendRpc("80 94 05 00 02");await p.WaitForRpcAsync("80 97 05 00");
            var entries=p.World.RpcSnapshot().Skip(cursor).Select(r=>r.Bytes).Where(b=>b.Length>17&&b[0]==0x80&&b[1]==0x96&&BitConverter.ToUInt16(b,2)==5).ToArray();
            int selected=MissionContactChoice.CriticalIndex(entries,"Disturbing Trends");p.SendRpc($"80 98 06 00 {Le((ushort)selected)} 01 02 00");await p.WaitForRpcAsync("80 99");p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");
            Check.Equal(201201,(await Saved()).Mission,"Normal accepted definition, never granted");
            step="phase1-talk";await EnterMissionArea(context,p);var first=await Ophiuchi(1);p.SendRpc($"80 c7 {MissionNpcId(first)} 09 00");await p.WaitForRpcAsync("80 a0 01 06 00 01");
            await LeaveMissionArea(p);await p.WaitForRpcAsync("80 9e 00");step="phase2-computer";await EnterMissionArea(context,p);var before=await Saved();Check.Equal(1,before.Phase,"Operations post phase");
            var source=before.Objects.Single(o=>o.Id==1);Check.Equal((ushort)3,source.Goid,"Actual normal GO3");Check.Equal(0x0d1001edu,source.Spot.mxoStaticId,"Existing seed source unchanged");
            var computer=await ZionObject(context,p,1);var position=computer.Attributes["Position"];
            Check.That(new[]{28740d,881d,13200d}.SequenceEqual(new[]{BitConverter.ToDouble(position),BitConverter.ToDouble(position,8),BitConverter.ToDouble(position,16)}),"Reviewed parent196 native source pose");
            Check.Equal("00000000F204353F00000000F40435BF",Convert.ToHexString(computer.Attributes["Orientation"]),"Exact parent source quaternion");
            byte take=ObjectStates.UseNumber(computer.Goid,"Take")??throw new ScenarioFailure("No native Take definition on actual Computer");
            await p.WaitUntilAsync(()=>{var updates=ViewUpdateValues(p,computer.ViewId,"01 08");uint state=updates.Count>0?updates[^1]:BitConverter.ToUInt32(computer.Attributes["CurrentState"]);return ObjectStates.Offered(computer.Goid,state).Contains(take);},"Actual Computer state offers Take",TimeSpan.FromSeconds(10));
            p.SendRpc($"80 c7 {MissionNpcId(computer)} {take:x2} 00");await p.WaitForRpcAsync("80 a0 01 06 00 01");step="carry-before-relog";var carried=await Saved();Check.Equal(1,carried.Phase,"Carry before normal exit");Check.That(carried.Done.Contains(1)&&carried.Items.ContainsKey(6783),"Normal Take earned tracked Map Disk6783");ushort slot=carried.Items[6783];
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid=6783"),"Exactly one earned disk");string savedObjects=JsonSerializer.Serialize(carried.Objects,options);
            p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");Record();p.Dispose();Console.WriteLine("  DISTURBING normal carry logout; waiting33s reservation cleanup");await Task.Delay(TimeSpan.FromSeconds(33));
            step="carry-relog";p=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+id,handle);await p.EnterWorldAsync();await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 9e 00");var restored=await Saved();
            Check.Equal(carried.Identity,restored.Identity,"Same active mission identity");Check.Equal(slot,restored.Items[6783],"Exact owned disk slot survives relog");Check.That(restored.Done.Contains(1),"Take goal survives relog");Check.Equal(savedObjects,JsonSerializer.Serialize(restored.Objects,options),"Saved source/prototype/state survives relog unchanged");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid=6783"),"Relog does not duplicate disk");
            await LeaveMissionArea(p);await p.WaitForRpcAsync("80 9e 00");step="phase3-give";await EnterMissionArea(context,p);var delivery=await Saved();Check.Equal(2,delivery.Phase,"New phase3 Ophiuchi");var npc=delivery.Mobs.Single(m=>m.Npc==6);
            var last=await Ophiuchi(6);Check.That(last.Attributes.TryGetValue("TakerActiveTracker",out var active)&&active.Any(b=>b!=0),"Actual phase3 NPC exposes Give");
            Check.That(last.Attributes.TryGetValue("Position",out var at)&&Math.Abs(BitConverter.ToDouble(at)-npc.X)<1&&Math.Abs(BitConverter.ToDouble(at,8)-npc.Y)<1&&Math.Abs(BitConverter.ToDouble(at,16)-npc.Z)<1,"Resolved current NPC6 placement, not old Ophiuchi1");
            p.SendRpc($"80 c7 {MissionNpcId(last)} 0e 00");await p.WaitForRpcAsync("80 a0 01 06 00 01");Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid=6783"),"Normal Give consumes tracked disk");
            step="normal-finish";if(!p.Received("80 a2 00").Any()){await LeaveMissionArea(p);await p.WaitForRpcAsync("80 a2 00",TimeSpan.FromSeconds(20));}
            var summary=p.Received("80 a2 00").Single().Bytes;Check.Equal(DisturbingTrendsFixture.ExpectedInfo,BitConverter.ToUInt32(summary,11),"Current policy information");Check.Equal(DisturbingTrendsFixture.ExpectedXp,BitConverter.ToUInt32(summary,15),"Current policy XP");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={id} AND missionId=201201"),"One earned completion");Check.Equal((long)PlayerProgression.ExperienceForLevel(50)+54871,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={id}"),"Current default50 quote paid once");Check.Equal(46339L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={id}"),"Current information paid once");
            Check.Equal(0,p.World.Problems.Count,"Final session parser clean");step="complete";
        }
        catch(Exception e){primary=e;throw;}
        finally
        {
            Exception? proofFailure=null;try{Record();}catch(Exception e){proofFailure=e;}
            string stem=Path.Combine(Path.GetTempPath(),"mxo-disturbing-trends-"+context.Database.Name);
            try
            {
                string dll=context.Server.SourceAssembly;
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Database=context.Database.Name,Step=step,Failure=primary?.ToString()??proofFailure?.ToString(),Checkpoints=checkpoints,Npcs=npcs,Sessions=sessions,SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),NativePickingProven=false,Dependency="PR196 981dda27; native HOLD"},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog());Console.WriteLine("  DISTURBING evidence: "+stem+" step="+step);
            }
            catch(Exception e){proofFailure??=e;Console.Error.WriteLine("Proof failure: "+e);}
            finally{p.Dispose();}
            if(primary==null&&proofFailure!=null)throw new ScenarioFailure("Required Disturbing Trends evidence failed: "+proofFailure);
        }
    }
}
