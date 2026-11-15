using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using hds;
namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task MissionBoundCabinet(ScenarioContext context)
    {
        MissionBoundCabinetFixture.RequireSource(System.Environment.GetEnvironmentVariable(MissionBoundCabinetFixture.SourcePin),context.Server.SourceAssemblySha256);
        const string handle="BoundCabinet";
        var p=await context.AddPlayerAsync(new CharacterSeed(handle){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=2,X=17000,Y=495,Z=2300,
            Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,CompletedMissions=MissionBoundCabinetFixture.Prerequisites,
            OwnedLoadedAbilities=MissionBoundCabinetFixture.Abilities.Select(id=>(id,(ushort)50)).ToArray()});
        var sessions=new List<HeadlessPlayer>{p};var proof=new List<object>();var checkpoints=new List<object>();string step="entry";Exception? failure=null;bool passed=false;
        var json=new JsonSerializerOptions{IncludeFields=true};string PacketPath()=>Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt");
        async Task<MissionProgress.SavedState> Saved()
        {
            string raw=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={p.CharId}");
            checkpoints.Add(new{Step=step,Payload=raw});var s=JsonSerializer.Deserialize<MissionProgress.SavedState>(raw,json)!;MissionBoundCabinetFixture.RequireOriginal(s);return s;
        }
        async Task<string> Account()=>await context.Database.ExecuteAsync($"SELECT exp,cash,(SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId=201203) FROM characters WHERE charId={p.CharId}");
        async Task<ViewCreation> Cabinet(int cursor,int sections)
        {
            ViewCreation? body=null;await p.WaitUntilAsync(()=>(body=MissionBoundCabinetFixture.Body(p.World.CreationSnapshot(),cursor))!=null,"Actual original bound326 at native factory centre",TimeSpan.FromSeconds(15));
            string raw=MissionBoundCabinetFixture.RequireBoundRecord(p.World.ViewSnapshot().Skip(sections),body!);
            var s=await Saved();Check.That(body!.Attributes.TryGetValue("MissionKey",out var key)&&key.SequenceEqual(new[]{(byte)0xff,(byte)s.Instance,(byte)(s.Instance>>8),(byte)2}),"Bound executor retains current mission instance");
            proof.Add(new{Step=step,body.ViewId,body.SpawnId,BoundSection=raw,Source=s.Objects.Single(),Attributes=body.Attributes.ToDictionary(x=>x.Key,x=>Convert.ToHexString(x.Value))});return body;
        }
        async Task Claim(byte[] window,uint item)
        {
            var identities=MissionBoundCabinetFixture.LootIdentities(window,false);Check.That(identities.Any(id=>BitConverter.ToUInt32(Convert.FromHexString(id),4)==item),"Actual expected earned item in loot window");
            ushort instance=(await Saved()).Instance;
            int cursor=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendRpc("81 1f");var reply=await p.WaitForRpcAsync("81 20");
            Check.That(identities.SequenceEqual(MissionBoundCabinetFixture.LootIdentities(reply.Bytes,true)),"8120 claims exact actual8119 identities");
            await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>MissionBoundCabinetFixture.MissionItemGrant(r.Bytes,item,instance)),"Normal item grant",TimeSpan.FromSeconds(15));
            proof.Add(new{Step=step,Item=item,Window=Convert.ToHexString(window),Accepted=reply.Hex});
        }
        try
        {
            Check.That(!MissionBoundCabinetFixture.Prerequisites.Contains(201203)&&!MissionBoundCabinetFixture.Prerequisites.Contains(201204),"No selected/next receipt grant");
            Check.Equal(23L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId}"),"Exactly22 earlier introduction/critical receipts plus tutorial986 before Margin/world");
            await p.EnterWorldAsync();int offer=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendRpc("80 94 05 00 02");await p.WaitForRpcAsync("80 97 05 00");
            int index=MissionContactChoice.CriticalIndex(p.World.RpcSnapshot().Skip(offer).Select(r=>r.Bytes),"Hard Time");
            p.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await p.WaitForRpcAsync("80 99");
            int views=p.World.CreationSnapshot().Count,sections=p.World.ViewSnapshot().Count;
            step="ordinary-accept";p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");var initial=await Saved();
            string RawSources(MissionProgress.SavedState state)=>JsonSerializer.Serialize(state.Objects.Select(o=>new{o.Id,o.Goid,o.Spot,o.OpenFloor}),json);
            string rawObjects=RawSources(initial);var body=await Cabinet(views,sections);
            var door=initial.Waypoints.Single();var lobby=initial.Exits.Single();uint lobbyKey=InteriorAdmissionFixture.DoorAt(new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}.SelectMany(f=>File.ReadLines(Path.Combine(context.RepositoryRoot,"data",f))),lobby);
            step="ordinary-saved-door-ride";await MoveForMissionTest(p,lobby[0],lobby[1]-50,lobby[2]);int log=context.Server.DebugLog().Length;p.SkipReceived();
            p.SendRpc($"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(lobbyKey))} 03 00");await p.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[MISSION] Teleported to "),"Ordinary saved-site elevator Ride",TimeSpan.FromSeconds(15));
            var ride=Regex.Matches(context.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").Last();
            var root=Enumerable.Range(1,3).Select(i=>double.Parse(ride.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data/navmesh"));
            Check.That(NavMesh.Locate(1,root[0],root[1],root[2],out var rootNav)&&rootNav.floorY+95==root[1]&&rootNav.sector==209&&rootNav.region==47,"Actual Ride root is on original source component");
            proof.Add(new{Step=step,LobbyKey=lobbyKey,Door=door,Root=root,Nav=rootNav,RawRide=context.Server.DebugLog()[log..]});
            step="ordinary-current-crew-combat";var killed=new HashSet<(ushort,byte)>();ViewCreation? phage=null;
            while(!(await Saved()).Done.Contains((ushort)1))
            {
                var current=await Saved();var actors=current.Mobs.Where(m=>!m.Dead&&!m.Friendly&&(m.Hostile||m.Npc==1)).ToArray();
                ViewCreation? target=null;await p.WaitUntilAsync(()=>
                {
                    target=p.World.CreationSnapshot().Skip(views).GroupBy(v=>v.ViewId).Select(g=>g.Last()).LastOrDefault(v=>v.Goid==599&&!killed.Contains((v.ViewId,v.SpawnId))&&
                        v.Attributes.TryGetValue("IsEnemy",out var enemy)&&enemy.Length==1&&enemy[0]!=0&&actors.Any(m=>m.Name==v.Name&&MissionBoundCabinetFixture.NpcLevel(v.Attributes)==m.Level&&
                            v.Attributes.TryGetValue("Position",out var at)&&at.Length==24&&BitConverter.ToDouble(at)==m.X&&BitConverter.ToDouble(at,8)==m.Y&&BitConverter.ToDouble(at,16)==m.Z));return target!=null;
                },"Current saved authored/generated hostile",TimeSpan.FromSeconds(15));
                if(target!.Name==current.Mobs.Single(m=>m.Npc==1).Name)phage=target;
                proof.Add(new{Step=step,Target=target,CurrentActors=actors});p.SkipReceived();await DefeatMissionNpc(p,target);killed.Add((target.ViewId,target.SpawnId));
            }
            Check.That(phage!=null,"Actual current Phage was defeated, not omitted by generated-only filter");
            step="earned-flyer";p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(phage!)} 05 00");var loot=await p.WaitForRpcAsync("81 19");await Claim(loot.Bytes,6796);
            await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Any(r=>BytePattern.Parse("80 a0 02 06 00 01").IsMatch(r.Bytes)),"Flyer goal acknowledgement",TimeSpan.FromSeconds(15));
            var carry=await Saved();Check.That(carry.Items.ContainsKey(6796)&&!carry.Done.Contains((ushort)3),"Earned Flyer while cabinet Search pending");string beforeAccount=await Account();
            string history=JsonSerializer.Serialize(new{carry.Objects,carry.SiteDoors,carry.OpenFloorCurrentSiteIndex,carry.OpenFloorSites},json);
            string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={p.CharId}");
            step="ordinary-carry-relog";p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");p.Dispose();await Task.Delay(TimeSpan.FromSeconds(33));
            p=await HeadlessPlayer.LoginAsync(context.Server.Address,username,handle);sessions.Add(p);views=p.World.CreationSnapshot().Count;sections=p.World.ViewSnapshot().Count;
            await p.EnterWorldAsync();await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 9e 00");var restored=await Saved();
            Check.Equal(carry.Identity,restored.Identity,"Same mission identity after normal relog");Check.Equal(carry.Started,restored.Started,"Same mission start");Check.Equal(carry.Instance,restored.Instance,"Same executor instance");
            Check.That(carry.Done.SequenceEqual(restored.Done)&&carry.EverDone.SequenceEqual(restored.EverDone),"Exact pending/completed history");
            Check.Equal(history,JsonSerializer.Serialize(new{restored.Objects,restored.SiteDoors,restored.OpenFloorCurrentSiteIndex,restored.OpenFloorSites},json),"Raw source and site history are immutable");
            Check.Equal(beforeAccount,await Account(),"Relog awards no XP/cash/receipt");body=await Cabinet(views,sections);
            step="grounded-original-cabinet-open";var standing=MissionBoundCabinetFixture.Standing;
            Check.That(NavMesh.Locate(1,standing[0],standing[1],standing[2],out var nav)&&nav.floorY+95==standing[1]&&nav.sector==rootNav.sector&&nav.region==rootNav.region,"Actual grounded same-placement approach, no legacy position fallback");
            Check.That(ContainerActions.InReach(standing.SelectMany(BitConverter.GetBytes).ToArray(),28040,1282,11435),"Unchanged server600/vertical200 authority");
            await MoveForMissionTest(p,standing[0],standing[1],standing[2]);string beforeSearch=await Account();p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(body)} 03 00");var folderWindow=await p.WaitForRpcAsync("81 19");await Claim(folderWindow.Bytes,6795);
            await p.WaitForRpcAsync("80 a0 03 06 00 01");var final=await Saved();
            Check.Equal(rawObjects,RawSources(final),"Original selected raw bound object remains unchanged after Open/Take");
            Check.That(final.Items.ContainsKey(6796)&&final.Items.ContainsKey(6795),"Both normal earned item identities remain owned");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6795 AND slot={final.Items[6795]}"),"One earned Folder at committed slot");
            Check.Equal(beforeSearch,await Account(),"Cabinet Search does not award mission XP/cash/receipt");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId=201203"),"No selected mission completion injection");
            foreach(var actor in sessions)Check.Equal(0,actor.World.Problems.Count,"Actual parser remains clean across sessions");
            proof.Add(new{Step=step,Standing=standing,Nav=nav,RawSource=final.Objects,Identity=final.Identity,final.Started,final.Instance,final.Items,BeforeSearch=beforeSearch,AfterSearch=await Account()});passed=true;step="complete";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            string? account=null,checkpoint=null,readError=null;try{account=await Account();checkpoint=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={p.CharId}");}catch(Exception error){readError=error.ToString();}
            string stem=Path.Combine(Path.GetTempPath(),"mxo-bound-cabinet-"+context.Database.Name),raw=InteriorAdmissionDeliveryProof.Read(PacketPath());File.WriteAllText(stem+"-PacketLog.txt",raw);
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),Database=context.Database.Name,ServerPid=context.Server.ProcessId,
                Source=context.Server.SourceAssembly,SourceSha256=context.Server.SourceAssemblySha256,ExpectedSource=System.Environment.GetEnvironmentVariable(MissionBoundCabinetFixture.SourcePin),Proof=proof,Checkpoints=checkpoints,
                FinalAccount=account,FinalCheckpoint=checkpoint,ReadError=readError,RawPacketLog=stem+"-PacketLog.txt",RawPacketSha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))),
                Sessions=sessions.Select(a=>new{a.CharId,Rpcs=a.World.RpcSnapshot(),Views=a.World.ViewSnapshot().Select(Convert.ToHexString),Problems=a.World.Problems}),
                Qualification="Normal-wire bound/static/item proof only; native hover/walk/rendering unproved. Test movement/weaken1 affects all nearby NPCs including friends; aggro/wander off; no unaided combat claim."},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
            File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n---Debug---\n"+context.Server.DebugLog());Console.WriteLine("BOUND CABINET EVIDENCE="+stem);foreach(var actor in sessions)actor.Dispose();
        }
    }
}
