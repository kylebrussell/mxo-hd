using System.Text.Json;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task<HeadlessPlayer> StartRelogMission(ScenarioContext context, bool acceptanceFailure = false)
    {
        HeadlessPlayer player=(await context.AddPlayersInWorldAsync(new CharacterSeed("MissionRelog"){Level=50,X=23828,Y=495,Z=13984}))[0];
        if(acceptanceFailure)
        {
            await context.Database.InstallMissionRollbackTriggerAsync(false,true);
            player.SendChat("?missionpersist start");await player.WaitForTextAsync("Mission progress could not be saved");
            Check.Equal(0,player.Received("80 9c").Count,"Failed acceptance does not publish a mission name");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId={player.CharId} AND payload IS NOT NULL"),"Failed acceptance leaves no active checkpoint");
            await context.Database.RemoveMissionRollbackTriggerAsync();
        }
        player.SendChat("?missionpersist start");
        await player.WaitForRpcAsync("80 9c");
        await player.WaitForRpcAsync("80 9e 00");
        return player;
    }
    private static async Task<MissionProgress.SavedState> MissionCheckpoint(ScenarioContext context,HeadlessPlayer player)
    {
        string json=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={player.CharId}");
        return JsonSerializer.Deserialize<MissionProgress.SavedState>(json,new JsonSerializerOptions{IncludeFields=true})!;
    }
    private static async Task MissionRelogPersistence(ScenarioContext context)
    {
        HeadlessPlayer original=await StartRelogMission(context,true);
        // Invoke the real checkpoint repository against this owned disposable DB, with an already occupied same-GOID slot.
        // Config parsing opens no connection; replace and validate the DB name before constructing the repository.
        var config=new ServerConfig(Path.Combine(context.RepositoryRoot,"hds","bin","Debug","net8.0","Config.xml.dist"));config.LoadDbParams();
        Check.That(context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal),"Owned disposable collision fixture");
        config.dbParams.DatabaseName=context.Database.Name;
        var previousConfig=hds.shared.Store.config;hds.shared.Store.config=config;
        try
        {
            var access=new hds.databases.MyWorldDbAccess();
            await context.Database.ExecuteAsync($"INSERT INTO inventory(charId,slot,goid,count) VALUES({original.CharId},30,6825,1)");
            string expected=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={original.CharId}");
            var candidate=JsonSerializer.Deserialize<MissionProgress.SavedState>(expected,new JsonSerializerOptions{IncludeFields=true})!;
            candidate.Items[6825]=30;
            bool refused=false;
            try{access.CommitActiveMission(original.CharId,expected,JsonSerializer.Serialize(candidate,new JsonSerializerOptions{IncludeFields=true}));}
            catch(MissionCheckpointException ex) when(ex.Message.Contains("slot occupied")){refused=true;}
            Check.That(refused,"New grant cannot adopt unrelated same-GOID item");
            Check.That(!(await MissionCheckpoint(context,original)).Items.ContainsKey(6825),"Failed grant preserves empty ownership");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={original.CharId} AND slot=30 AND goid=6825"),"Foreign identical item untouched");
            await context.Database.ExecuteAsync($"DELETE FROM inventory WHERE charId={original.CharId} AND slot=30 AND goid=6825");
        }
        finally{hds.shared.Store.config=previousConfig;}

        ViewCreation herschel=await VisitMissionNpc(context,original,"Herschel");
        await context.Database.InstallMissionRollbackTriggerAsync(false);
        original.SendRpc($"80 c7 {MissionNpcId(herschel)} 0d 00");
        await original.WaitForTextAsync("Mission progress could not be saved");
        Check.Equal(0,original.Received("80 a0 01 06 00 01").Count,"Failed goal checkpoint never publishes done");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={original.CharId} AND goid=6825"),"Earlier committed grant retained");
        Check.That(!(await MissionCheckpoint(context,original)).Done.Contains(1),"Cached durable phase restored before retry");
        Check.Equal(1,original.Received("5e ?? ?? a9 1a 00 00").Count,"Discarded grant output resynchronized from durable ownership");
        await context.Database.RemoveMissionRollbackTriggerAsync();
        herschel=await original.WaitForViewNamedAsync("Herschel");

        original.SendRpc($"80 c7 {MissionNpcId(herschel)} 0d 00");
        await original.WaitForRpcAsync("80 a0 01 06 00 01");
        MissionProgress.SavedState before=await MissionCheckpoint(context,original);
        Check.That(before.Done.Contains(1),"First Get completed before relog");
        Check.Equal(1,before.Items.Count,"Exactly one owned mission item");
        Check.That(before.Mobs.Count>=3,"Placed mission NPCs and generated enemies captured");
        // Stop only this disposable harness server. The next process has no runtime/static mission state.
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={original.CharId}");
        original.SendChat("?save");
        await original.WaitForRpcAsync("2e 07");
        original.Dispose();context.Server.Dispose();
        using ServerInstance restarted=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs);
        using HeadlessPlayer player=await HeadlessPlayer.LoginAsync(restarted.Address,username,original.Handle);
        Check.That(!string.IsNullOrEmpty(await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={player.CharId}")),"Checkpoint still present before world entry");
        await player.EnterWorldAsync();
        try { await player.WaitForRpcAsync("80 9c"); }
        catch(Exception ex) {throw new ScenarioFailure(ex.Message+" RESTART OUTPUT: "+restarted.OutputTail(35));}
        await player.WaitForRpcAsync("80 a0 01 06 00 01");
        MissionProgress.SavedState after=await MissionCheckpoint(context,player);
        Check.Equal(before.Phase,after.Phase,"Same phase after process restart");
        Check.Equal(before.Instance,after.Instance,"Same mission key after restart");
        Check.Equal(before.Mobs.Count,after.Mobs.Count,"No duplicated NPC records");
        Check.Equal(before.Items[6825],after.Items[6825],"Same owned inventory slot");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6825"),"No duplicate item grant");
        herschel=await player.WaitForViewNamedAsync("Herschel",TimeSpan.FromSeconds(20));
        player.SendRpc($"80 c7 {MissionNpcId(herschel)} 0d 00");
        await player.WaitForRpcAsync("80 a0 02 06 00 01");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6825"),"Second objective does not replay first grant");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6820"),"Second Get grants its own item");
        await context.Database.InstallMissionRollbackTriggerAsync(true);
        player.SendRpc("80 a6");await player.WaitForTextAsync("Mission progress could not be saved");
        Check.Equal(0,player.Received("80 a2 08").Count,"Failed abandon cannot publish abort");
        Check.Equal(2,(await MissionCheckpoint(context,player)).Items.Count,"Failed abandon retains exact owned effects");
        await context.Database.RemoveMissionRollbackTriggerAsync();
        player.SendRpc("80 a6");
        await player.WaitForRpcAsync("80 a2 08");
        Check.Equal("NULL",await context.Database.ScalarAsync($"SELECT COALESCE(payload,'NULL') FROM char_active_missions WHERE charId={player.CharId}"),"Abandon clears checkpoint");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid IN(6825,6820)"),"Abandon removes owned mission items");
        Check.Equal(0,player.World.Problems.Count,"Restored records readable");
    }
    private static Task MissionRelogObjectsAndRollback(ScenarioContext context) => MissionRelogObjectsAndRollbackCore(context,false);
    private static Task MissionRelogObjectsAndRollback842(ScenarioContext context) => MissionRelogObjectsAndRollbackCore(context,true);
    private static async Task MissionRelogObjectsAndRollbackCore(ScenarioContext context,bool exact842)
    {
        ServerInstance? seededReturn=null;ServerInstance currentOwnedServer=context.Server;
        var proofActors=new List<HeadlessPlayer>();
        var proofServers=new List<(ServerInstance Server,int Pid,string Dll,string Hash,DateTime Mtime)>();
        var rawCheckpoints=new List<object>();var operations=new List<object>();
        if(exact842)operations.Add(new{Step="exact842-reproduction",InitialRawSeed=Rollback842Fixture.InitialSeed,Phase1RawSeed=Rollback842Fixture.Phase1Seed,OriginalFailedSchema="mxotest_run_1_98d89a144c"});
        var clock=System.Diagnostics.Stopwatch.StartNew();string step="entry";Exception? failure=null;uint character=0;
        void TrackServer(ServerInstance server)
        {
            string dll=server.SourceAssembly;proofServers.Add((server,server.ProcessId,dll,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(dll))),File.GetLastWriteTimeUtc(dll)));
        }
        async Task<string> Raw(string label)
        {
            string payload=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={character}");
            rawCheckpoints.Add(new{Label=label,ElapsedMs=clock.ElapsedMilliseconds,Payload=payload});return payload;
        }
        async Task<MissionProgress.SavedState> Checkpoint(HeadlessPlayer player)
        {
            string payload=await Raw(step);return JsonSerializer.Deserialize<MissionProgress.SavedState>(payload,new JsonSerializerOptions{IncludeFields=true})!;
        }
        void Window(string label,HeadlessPlayer player)
        {
            step=label;operations.Add(new{Step=step,ElapsedMs=clock.ElapsedMilliseconds,RpcCursor=player.World.RpcSnapshot().Count,
                CreationCursor=player.World.CreationSnapshot().Count,ViewCursor=player.World.ViewSnapshot().Count});
        }
        void RequireExited(int pid)
        {
            bool exited;try{using var process=System.Diagnostics.Process.GetProcessById(pid);exited=process.HasExited;}catch(ArgumentException){exited=true;}
            Check.That(exited,"Owned predecessor exited before successor starts");
        }
        TrackServer(context.Server);
        try
        {

        var original=await context.AddPlayerAsync(new CharacterSeed("Checkpoint"){Level=50,Experience=int.MaxValue-10,Info=int.MaxValue-5,Items=new[]{(20,48125u,1),(21,4u,1)}});
        proofActors.Add(original);character=original.CharId;
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={original.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({original.CharId},300,{AbilityGoid(142)},10,1)");
        await original.EnterWorldAsync();
        original.SendRpc("59 14");
        await original.WaitForRpcAsync("80 9c");await original.WaitForRpcAsync("80 9e 00");
        string acceptedRaw=await Raw("accepted-before-courier");
        try
        {
            using var state=JsonDocument.Parse(acceptedRaw);var saved=state.RootElement;
            int mission=saved.GetProperty("Mission").GetInt32(),nextPhase=saved.GetProperty("Phase").GetInt32()+1;
            uint started=saved.GetProperty("Started").GetUInt32();
            int legacySeed=unchecked((int)character*7919+mission*31+(nextPhase+1)+(int)started);
            string? rawQaSeed=exact842?Rollback842Fixture.Phase1Seed.ToString():Environment.GetEnvironmentVariable("MXO_MISSION_SITE_QA_SEED");
            int? qaSeed=MissionStaging.QaSiteSeed("1",context.Database.Name,rawQaSeed,mission,nextPhase);
            operations.Add(new{Step="next-phase-source-inputs",ElapsedMs=clock.ElapsedMilliseconds,Character=character,Mission=mission,NextPhase=nextPhase,Started=started,
                LegacyFallbackSeed=legacySeed,RawQaSeed=rawQaSeed,EffectiveNextPhaseSeed=qaSeed??legacySeed,
                Qualification="Read-only reconstruction of unchanged LayoutRandom inputs; no Pending choice is claimed before it persists."});
        }
        catch(Exception decode){operations.Add(new{Step="next-phase-input-decode-failed",Failure=decode.ToString()});}
        var courier=await VisitMissionNpc(context,original,"Checkpoint Courier");
        if(exact842)
        {
            step="exact842-save-before-cold-return";
            var first=await Checkpoint(original);Rollback842Fixture.Phase0(first);
            byte[] courierPose=courier.Attributes["Position"];
            Rollback842Fixture.PreGet(new[]{BitConverter.ToDouble(courierPose)+100,BitConverter.ToDouble(courierPose,8),BitConverter.ToDouble(courierPose,16)});
            original.SkipReceived();original.SendChat("?save");await original.WaitForRpcAsync("2e 07");
            var savedPosition=(await context.Database.ScalarAsync($"SELECT x+0e0,y+0e0,z+0e0 FROM characters WHERE charId={character}"))
                .Split('\t').Select(v=>double.Parse(v,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            operations.Add(new{Step="exact842-save-ack-db-position",SavedPosition=savedPosition});
            Rollback842Fixture.PreGet(savedPosition);
            string account=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={character}");
            string identity=first.Identity;long started=first.Started;string rawBefore=await Raw("exact842-saved-phase0");
            int oldPid=context.Server.ProcessId;original.Dispose();context.Server.Dispose();
            bool exited;try{using var process=System.Diagnostics.Process.GetProcessById(oldPid);exited=process.HasExited;}catch(ArgumentException){exited=true;}
            Check.That(exited,"Owned initial server exited before seeded successor starts");
            seededReturn=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,
                new Dictionary<string,string>(StillNpcs){["MXO_MISSION_SITE_QA_SEED"]=Rollback842Fixture.Phase1Seed.ToString()},"mission-persistence.xml");
            currentOwnedServer=seededReturn;TrackServer(seededReturn);original=await HeadlessPlayer.LoginAsync(seededReturn.Address,account,original.Handle);proofActors.Add(original);
            await original.EnterWorldAsync();await original.WaitForRpcAsync("80 9c");
            var restored=await Checkpoint(original);Rollback842Fixture.Phase0(restored);Check.Equal(identity,restored.Identity,"Seed transition preserves acceptance identity");
            Check.Equal(started,restored.Started,"Seed transition preserves Started");
            byte[] self=original.World.CreationSnapshot().Single(c=>c.ViewId==2).Attributes["Position"];
            var coldSelf=new[]{BitConverter.ToDouble(self),BitConverter.ToDouble(self,8),BitConverter.ToDouble(self,16)};
            operations.Add(new{Step="exact842-cold-phase0",InitialSeed=Rollback842Fixture.InitialSeed,Phase1Seed=Rollback842Fixture.Phase1Seed,
                PredecessorPid=oldPid,PredecessorExited=exited,SavedPosition=savedPosition,ColdSelf=coldSelf,RawBefore=rawBefore,RawAfter=await Raw("exact842-restored-phase0")});
            Rollback842Fixture.PreGet(coldSelf);courier=await original.WaitForViewNamedAsync("Checkpoint Courier");
            byte[] restoredCourier=courier.Attributes["Position"];
            Rollback842Fixture.PreGet(new[]{BitConverter.ToDouble(restoredCourier)+100,BitConverter.ToDouble(restoredCourier,8),BitConverter.ToDouble(restoredCourier,16)});
        }
        Window("courier-get",original);
        var courierPosition=courier.Attributes["Position"];
        var positionBeforeGet=new[]{(double)(float)(BitConverter.ToDouble(courierPosition)+100),(double)(float)BitConverter.ToDouble(courierPosition,8),(double)(float)BitConverter.ToDouble(courierPosition,16)};
        byte[] teleportHeader=WireFormat.FromHex("02 00 01 1c");var positionRecords=new List<string>();
        foreach(byte[] section in original.World.ViewSnapshot())
            for(int at=BytePattern.IndexOf(section,teleportHeader,0);at>=0;at=BytePattern.IndexOf(section,teleportHeader,at+1))
                if(at+28<=section.Length&&Enumerable.Range(0,3).All(i=>(double)BitConverter.ToSingle(section,at+12+i*4)==positionBeforeGet[i]))
                    positionRecords.Add(Convert.ToHexString(section.AsSpan(at,28)));
        operations.Add(new{Step="courier-current-target",ElapsedMs=clock.ElapsedMilliseconds,courier.ViewId,courier.SpawnId,courier.Goid,
            Attributes=courier.Attributes.ToDictionary(a=>a.Key,a=>Convert.ToHexString(a.Value)),Request=$"80 c7 {MissionNpcId(courier)} 0d 00",
            PositionBeforeGet=positionBeforeGet,ScopedMatchingReceivedTeleportRecords=positionRecords,
            PositionQualification="Unchanged VisitMissionNpc adds100X; existing ?tp stores f32 coordinates. Actual matching28-byte03 records retained; not a new world-position decoder."});
        original.SendRpc($"80 c7 {MissionNpcId(courier)} 0d 00");
        await original.WaitForRpcAsync("80 a0 01 06 00 01");
        await Raw("courier-goal-ack-before-next-phase");
        Window("await-next-phase-briefing",original);
        await original.WaitForTextAsync("Unlock the safe");
        await original.WaitForRpcAsync("80 9e 00");
        await Raw("next-phase-briefing");
        if(exact842)Rollback842Fixture.Phase1(await Checkpoint(original));
        Window("enter-safe-area",original);
        int objectCursor=original.World.CreationSnapshot().Count;
        await EnterMissionArea(currentOwnedServer,original);
        await ApproachMissionObjectFixture(context,original,1);
        ViewCreation safe;
        if(exact842)
        {
            ushort concrete=(await Checkpoint(original)).Objects.Single(o=>o.Id==1).Goid;
            await original.WaitUntilAsync(()=>original.World.CreationSnapshot().Skip(objectCursor).Any(v=>v.Goid==concrete),"Current admitted safe view",TimeSpan.FromSeconds(10));
            safe=original.World.CreationSnapshot().Skip(objectCursor).Last(v=>v.Goid==concrete);
        }
        else safe=await MissionObjectView(context,original,"Safe");
        original.SendRpc($"80 c7 {MissionNpcId(safe)} 08 00");
        await original.WaitForRpcAsync(AbilityLine(0x7a000145));
        original.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");await original.WaitForRpcAsync("81 19");
        original.SendRpc("81 1f");await original.WaitForRpcAsync("80 a0 01 06 00 01");
        original.SendChat("?missionpersist status");await original.WaitForTextAsync("phase 2");
        var before=await Checkpoint(original);
        Check.Equal(1,before.Phase,"Second phase saved");Check.That(before.Objects.Single().Unlocked,"Actual lockpick persisted");
        Check.Equal(2,before.Items.Count,"Prior phase unused disk and current search disk owned");
        original.SendChat("?save");await original.WaitForRpcAsync("2e 07");
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={original.CharId}");
        int currentPid=currentOwnedServer.ProcessId;original.Dispose();currentOwnedServer.Dispose();
        if(exact842)RequireExited(currentPid);
        using var server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs,"mission-persistence.xml");
        TrackServer(server);
        using var p=await HeadlessPlayer.LoginAsync(server.Address,username,original.Handle);proofActors.Add(p);Window("cold-safe-restore",p);await p.EnterWorldAsync();
        await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 a0 01 06 00 01");
        var after=await Checkpoint(p);
        Check.Equal(before.Identity,after.Identity,"Same acceptance identity");
        Check.Equal(1,after.Phase,"No phase restart");Check.Equal(before.Objects.Single().Spot.pos_x,after.Objects.Single().Spot.pos_x,"Same object placement");
        Check.That(after.Objects.Single().Unlocked,"Safe stays unlocked");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(before.Objects.Single().Goid).Count>0,"Restored safe",TimeSpan.FromSeconds(15));
        safe=p.ViewsOfGoid(before.Objects.Single().Goid).Single();
        Check.Equal(1,p.ViewsOfGoid(before.Objects.Single().Goid).Count,"Exactly one live safe view");
        p.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");await p.WaitForRpcAsync("81 19");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6831"),"Prior phase item not duplicated");
        Check.Equal("InnoDB",await context.Database.ScalarAsync("SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='characters'"),"Character engine migrated");
        Check.Equal("InnoDB",await context.Database.ScalarAsync("SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='char_missions'"),"Mission engine migrated");
        Window("completion-SQL-rollback",p);
        await context.Database.InstallMissionRollbackTriggerAsync(true);
        int disk=after.Items[6786];p.SendRpc($"81 21 {disk:x2}");await p.WaitForRpcAsync("80 a0 02 06 00 01");
        await p.WaitUntilAsync(()=>server.OutputLines("[MISSION] Completion transaction deferred").Any(),"Deliberate completion rollback",TimeSpan.FromSeconds(10));
        Check.Equal((long)int.MaxValue-10,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Rolled back XP");
        Check.Equal((long)int.MaxValue-5,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Rolled back info");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId=399905"),"Rolled back completed record");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=48125"),"Rolled back ticket consumption");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=48134"),"Rolled back ticket reward");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6831"),"Rolled back owned-item cleanup");
        Check.That((await Checkpoint(p)).Done.Contains(2),"Completed goal remains retryable active phase");
        await context.Database.RemoveMissionRollbackTriggerAsync();
        var completion=await p.WaitForRpcAsync("80 a2 00");
        Check.Equal(5u,BitConverter.ToUInt32(completion.Bytes,11),"Summary reports actual capped info delta");
        Check.Equal(10u,BitConverter.ToUInt32(completion.Bytes,15),"Summary reports actual capped XP delta");
        Check.Equal((long)int.MaxValue,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"XP paid once");
        Check.Equal((long)int.MaxValue,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Info paid once");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId=399905"),"One completed record");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid IN(48125,6831,6786)"),"Owned cleanup and ticket commit");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=48134"),"Exactly one ticket reward");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_active_missions WHERE charId={p.CharId} AND payload IS NULL AND receipt IS NOT NULL"),"Durable completion receipt");
        int coldPid=server.ProcessId;p.Dispose();server.Dispose();
        if(exact842)RequireExited(coldPid);
        using var last=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs,"mission-persistence.xml");
        TrackServer(last);
        using var again=await HeadlessPlayer.LoginAsync(last.Address,username,original.Handle);proofActors.Add(again);Window("completed-cold-return",again);await again.EnterWorldAsync();
        again.SendChat("?missionpersist status");await again.WaitForTextAsync("Mission persistence: none");
        Check.Equal((long)int.MaxValue,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={again.CharId}"),"Relog does not replay completed payout");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={again.CharId} AND goid=48134"),"Relog does not repeat ticket reward");
        Check.Equal(0,again.World.Problems.Count,"Postcompletion records readable");
        step="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-mission-relog-object-rollback-"+context.Database.Name);
            try
            {
                if(character!=0)
                {
                    try{await Raw("finally-before-decode");}
                    catch(Exception read){rawCheckpoints.Add(new{Label="finally-checkpoint-read-failed",ElapsedMs=clock.ElapsedMilliseconds,ReadFailure=read.ToString()});}
                }
                var env=new[]{"MXO_MISSION_SITE_QA_SEED","MXO_DEV_COMMANDS"}.ToDictionary(k=>k,k=>Environment.GetEnvironmentVariable(k));
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Step=step,Failure=failure?.ToString(),ElapsedMs=clock.ElapsedMilliseconds,
                    Database=context.Database.Name,Character=character,RawCheckpoints=rawCheckpoints,Operations=operations,InheritedEnvironment=env,
                    Fixture="mission-persistence.xml",FixtureSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(context.RepositoryRoot,"tools","HeadlessClient","Fixtures","mission-persistence.xml")))),
                    Servers=proofServers.Select(s=>new{ProcessId=s.Pid,SourceAssembly=s.Dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(s.Dll)!)!.Name,SourceSha256=s.Hash,SourceMtimeUtc=s.Mtime,
                        Output=s.Server.FullOutput(),Debug=s.Server.DebugLog()}),
                    Sessions=proofActors.Select(p=>new{p.CharId,p.Handle,Rpcs=p.World.RpcSnapshot().Select(r=>r.Hex).ToArray(),
                        Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()}),NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
                File.WriteAllText(stem+".log",string.Join("\n--- owned server ---\n",proofServers.Select(s=>s.Server.FullOutput()+"\n--- Debug ---\n"+s.Server.DebugLog())));
                Console.WriteLine("MISSION RELOG OBJECT ROLLBACK evidence before teardown="+stem);
            }
            catch(Exception evidence){if(failure==null)throw;Console.Error.WriteLine("Rollback evidence failure; preserving primary: "+evidence);}
            finally{foreach(var player in proofActors)player.Dispose();seededReturn?.Dispose();}
        }
    }

    private static async Task MissionRelogDisconnectAndMove(ScenarioContext context)
    {
        var original=await StartRelogMission(context);
        var npc=await VisitMissionNpc(context,original,"Herschel");
        original.SendRpc($"80 c7 {MissionNpcId(npc)} 0d 00");await original.WaitForRpcAsync("80 a0 01 06 00 01");
        var before=await MissionCheckpoint(context,original);ushort slot=before.Items[6825];
        original.SendRpc($"65 {Le(slot)} 0a 00");await original.WaitForRpcAsync($"65 {Le(slot)} 0a 00 00");
        Check.Equal(10L,await context.Database.LongAsync($"SELECT slot FROM inventory WHERE charId={original.CharId} AND goid=6825"),"Owned physical item moved");
        Check.Equal((ushort)10,(await MissionCheckpoint(context,original)).Items[6825],"Owned checkpoint moved atomically");
        original.SendChat("?save");await original.WaitForRpcAsync("2e 07");
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={original.CharId}");
        original.SendRpc("80 fe 00");await original.WaitForRpcAsync("80 fd");original.Dispose();
        // Natural client timeout invokes production CleanDeadPlayers/Suspend and releases the margin reservation.
        await Task.Delay(TimeSpan.FromSeconds(33));
        using var p=await HeadlessPlayer.LoginAsync(context.Server.Address,username,original.Handle);
        await p.EnterWorldAsync();await p.WaitForRpcAsync("80 9c");await p.WaitForRpcAsync("80 a0 01 06 00 01");
        var restored=await MissionCheckpoint(context,p);
        Check.Equal(before.Identity,restored.Identity,"Same acceptance survives ordinary disconnect");
        Check.Equal((ushort)10,restored.Items[6825],"Moved slot survives disconnect");
        Check.Equal(before.Mobs.Count,restored.Mobs.Count,"Suspended actors recreated once");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6825"),"No duplicate moved item");
        p.SendRpc("80 a6");await p.WaitForRpcAsync("80 a2 08");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6825"),"Abandon cleans moved ownership");
    }

    private static async Task MissionRelogCorpseLoot(ScenarioContext context)
    {
        var original=await context.AddPlayerAsync(new CharacterSeed("CorpseOwner"){Level=50});
        await original.EnterWorldAsync();
        original.SendRpc("80 94 05 00 03");await original.WaitForTextAsync("Mission Corpse Persistence Fixture");
        await original.WaitForRpcAsync("80 97 05 00");original.SendRpc("80 98 06 00 00 00 01 02 00");await original.WaitForRpcAsync("80 99");
        original.SendRpc("80 9b 07 00 01");await original.WaitForRpcAsync("80 9e 00");
        var carrier=await VisitMissionNpc(context,original,"Pending Carrier");
        await DefeatMissionNpc(original,carrier);
        original.SendChat("?missionpersist status");await original.WaitForTextAsync("Mission persistence:");
        var saved=await MissionCheckpoint(context,original);
        var body=saved.Mobs.Single(m=>m.Name=="Pending Carrier");
        Check.That(body.Dead&&body.Lootable&&!body.ScriptedCorpse,"Ordinary killed mission actor remains an ordinary lootable corpse");
        Check.That(body.Loot!=null,"Generated pending loot checkpointed");
        Check.Equal(6791u,body.Loot!.MissionItem,"Unclaimed mission card persisted");
        long killXp=await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={original.CharId}");
        Check.That(killXp>0&&body.RewardExperience>0,"Kill XP and actor marker committed together");
        original.SendChat("?save");await original.WaitForRpcAsync("2e 07");
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={original.CharId}");
        original.Dispose();context.Server.Dispose();
        using var server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs,"mission-persistence-loot.xml");
        using var p=await HeadlessPlayer.LoginAsync(server.Address,username,original.Handle);await p.EnterWorldAsync();await p.WaitForRpcAsync("80 9c");
        carrier=await p.WaitForViewNamedAsync("Pending Carrier",TimeSpan.FromSeconds(20));
        Check.Equal((byte)1,carrier.Attributes["IsDead"][0],"Restored corpse is introduced dead");
        Check.Equal(killXp,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Relog does not duplicate kill XP");
        p.SendRpc($"80 c7 {MissionNpcId(carrier)} 05 00");
        var window=await p.WaitForRpcAsync("81 19");
        Check.Equal(body.Loot.Info,BitConverter.ToUInt32(window.Bytes,8),"Same generated pending cash, no reroll");
        p.SendRpc("81 1f");await p.WaitForRpcAsync("80 a0 01 06 00 01");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6791"),"One claimed mission card");
        long paid=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}");
        Check.Equal(1000L+body.Loot.Info,paid,"Unpaid corpse cash credited exactly once");
        p.SendRpc($"80 c7 {MissionNpcId(carrier)} 05 00");await p.WaitForRpcAsync("81 19");p.SendRpc("81 1f");await p.WaitForRpcAsync("81 20");
        Check.Equal(paid,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Reopen cannot repay pocket cash");
        Check.Equal(killXp,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Reopen cannot repay kill XP");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=6791"),"Reopen cannot duplicate mission card");
    }

}
