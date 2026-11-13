using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task ZionPropagandaCount(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("ZionPropaganda"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=1,
            X=17000,Y=495,Z=2300,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        await context.Database.ExecuteAsync(ZionPropagandaFixture.SeedPrerequisites(p.CharId));
        string phase="entry";Exception? primary=null;var checkpoints=new List<object>();var actions=new List<object>();
        async Task<MissionProgress.SavedState> Saved()
        {var saved=await MissionCheckpoint(context,p);checkpoints.Add(new{Step=phase,State=saved});return saved;}
        try
        {
            await p.EnterWorldAsync();phase="normal-offer";int cursor=p.World.RpcSnapshot().Count;
            p.SendRpc("80 94 05 00 01");await p.WaitForRpcAsync("80 97 05 00");
            var entries=p.World.RpcSnapshot().Skip(cursor).Select(r=>r.Bytes).Where(b=>b.Length>17&&b[0]==0x80&&b[1]==0x96&&BitConverter.ToUInt16(b,2)==5).ToArray();
            int index=MissionContactChoice.CriticalIndex(entries,"The Power of Propaganda");
            p.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await p.WaitForRpcAsync("80 99");p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");
            Check.Equal(101202,(await Saved()).Mission,"Normal selected definition, never granted completion");
            for(int area=0;area<2;area++)
            {
                phase=$"area{area+1}-enter";await EnterMissionArea(context,p);var allocated=await Saved();int wanted=area==0?6:4;
                Check.Equal(area,allocated.Phase,"Normal phase index");Check.Equal(wanted,allocated.Objects.Count,"All required proxies allocated before actions");
                Check.Equal(wanted,allocated.Objects.Select(o=>o.Spot.mxoStaticId).Distinct().Count(),"Distinct actual source keys");
                Check.Equal(wanted,allocated.Objects.Select(o=>(o.Spot.pos_x,o.Spot.pos_y,o.Spot.pos_z)).Distinct().Count(),"Distinct actual source positions");
                foreach(var obj in allocated.Objects.OrderBy(o=>o.Id))
                {
                    phase=$"area{area+1}-object{obj.Id}";var view=await ZionObject(context,p,obj.Id);
                    Check.Equal(obj.Goid,view.Goid,"Actual view matches saved chosen prototype");
                    byte use=ObjectStates.UseNumber(view.Goid,"Open")??throw new ScenarioFailure("Actual authored proxy has no source-backed Open action");
                    uint state=view.Attributes.TryGetValue("CurrentState",out var bytes)?BitConverter.ToUInt32(bytes):ObjectStates.DefaultState(view.Goid);
                    Check.That(ObjectStates.Offered(view.Goid,state).Contains(use),"Actual received or inherited native state offers Open");
                    actions.Add(new{Area=area,Object=obj.Id,Source=obj.Spot,Concrete=obj.Goid,View=view.ViewId,view.SpawnId,Use=use,State=state,InheritedDefault=!view.Attributes.ContainsKey("CurrentState")});
                    p.SendRpc($"80 c7 {MissionNpcId(view)} {use:x2} 00");await p.WaitForRpcAsync($"80 a0 {obj.Id:x2} 06 00 01");
                    if(obj.Id<wanted)
                    {
                        var partial=await Saved();Check.Equal(area,partial.Phase,"Area cannot advance before the sixth/fourth required goal");
                        Check.Equal((int)obj.Id,partial.Done.Length,"Only earned required goals are complete");
                        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId=101202"),"No premature completion receipt");
                    }
                }
                // Existing proximity policy may already advance when the last required object is next to an exit.
                if(area==0)
                {
                    var completed=await Saved();
                    if(completed.Phase==0){phase="area1-exit";await LeaveMissionArea(p);await p.WaitForRpcAsync("80 9e 00");}
                    Check.Equal(1,(await Saved()).Phase,"Six earned actions then normal exit/proximity advance to second area");
                }
                else if(!p.Received("80 a2 00").Any())
                {phase="area2-exit";await LeaveMissionArea(p);await p.WaitForRpcAsync("80 a2 00",TimeSpan.FromSeconds(20));}
            }
            phase="complete";Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId=101202"),"One earned receipt");
            Check.Equal((long)PlayerProgression.ExperienceForLevel(50)+137500,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Existing level50 policy pays captured base XP once");
            Check.Equal(92000L,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Existing info quote paid once");
            Check.Equal(10,actions.Count,"Ten normal object actions");Check.Equal(0,p.World.Problems.Count,"Protocol parser clean");
        }
        catch(Exception e){primary=e;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-zion-propaganda-count-"+context.Database.Name);
            try
            {
                string dll=context.Server.SourceAssembly;
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Database=context.Database.Name,Step=phase,Failure=primary?.ToString(),SourceAssembly=dll,
                    SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),
                    Checkpoints=checkpoints,Actions=actions,Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),ReceivedViewLists=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),
                    ProtocolProblems=p.World.Problems.ToArray(),NativePlacementProven=false,NativePickingProven=false,Movement="Existing test-only movement/elevator helpers; StillNpcs disables unsolicited aggro"},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog());Console.WriteLine("  PROPAGANDA evidence: "+stem+" step="+phase);
            }
            catch(Exception e){if(primary==null)throw;Console.Error.WriteLine("Secondary evidence failure; preserving original failure: "+e);}
            finally{p.Dispose();}
        }
    }
}
