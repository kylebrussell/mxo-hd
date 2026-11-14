using hds;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using MxoHd.PacketResearch;

namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task EngageFoesTaunt(ScenarioContext context)
    {
        string sourceHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly)));
        EngageFoesFixture.RequireSource(System.Environment.GetEnvironmentVariable(EngageFoesFixture.ExpectedSourceEnvironment),sourceHash);
        var a=await context.AddPlayerAsync(EngageFoesFixture.Seed("EngageBlocker",true));
        var b=await context.AddPlayerAsync(EngageFoesFixture.Seed("EngageCaster"));
        Exception? failure=null;int fullHp=0;ViewCreation? target=null;ViewCreation? secondary=null;string baseline="";byte[]? request=null;int initialIs=0,ownedRoundCursor=0,secondaryAfterSetup=0,speedRoundCount=0,speedDamage=0;double survivalSeconds=0;ushort[] spent=Array.Empty<ushort>(),expectedRecipients=Array.Empty<ushort>();
        try
        {
            await a.EnterWorldAsync();await b.EnterWorldAsync();
            await EngageFoesFixture.BlockNpcAsync(a);
            await EngageFoesFixture.DefaultTacticNpcAsync(b); //Existing647 policy requires B already fighting the secondary for A's fight to divert.
            await b.WaitUntilAsync(()=>EngageFoesFixture.CurrentNpc(b.World.CreationSnapshot(),"Trainman")!=null,"caster's actual NPC view",TimeSpan.FromSeconds(15));
            target=EngageFoesFixture.CurrentNpc(b.World.CreationSnapshot(),"Trainman")!;
            secondary=EngageFoesFixture.CurrentNpc(b.World.CreationSnapshot(),"WinterFrost");
            Check.That(secondary!=null,"Existing A fight's WinterFrost secondary has an actual B view");
            Check.That(EngageFoesFixture.Position(target).SequenceEqual(new[]{EngageFoesFixture.PrimaryX,EngageFoesFixture.PrimaryY,EngageFoesFixture.PrimaryZ}),"Actual primary is visibly separate at authored fixture pose");
            Check.That(EngageFoesFixture.Position(secondary!).SequenceEqual(new[]{EngageFoesFixture.SecondaryX,EngageFoesFixture.SecondaryY,EngageFoesFixture.SecondaryZ}),"Actual secondary pose unchanged");
            Check.Equal((ushort)10000,BitConverter.ToUInt16(secondary!.Attributes["MaxHealth"]),"Actual initial secondary maximum10000");
            var survival=System.Diagnostics.Stopwatch.StartNew();await Task.Delay(TimeSpan.FromSeconds(60));survivalSeconds=survival.Elapsed.TotalSeconds;
            var speedRounds=b.World.RpcSnapshot().Where(r=>r.Bytes.Length==89&&r.Bytes[0]==0x45&&r.Bytes[8]==0&&BitConverter.ToUInt32(r.Bytes,49)==DuelPackets.Handle(secondary!.ViewId,secondary.SpawnId)).ToArray();
            speedRoundCount=speedRounds.Length;speedDamage=speedRounds.Sum(r=>(int)BitConverter.ToUInt16(r.Bytes,73));
            Check.That(speedRoundCount>0&&speedDamage>0,"Actual B/WinterFrost RPC45 records prove default Speed and positive ordinary damage before610");
            secondaryAfterSetup=await EngageFoesFixture.CurrentHealthAsync(b,secondary);Check.That(secondaryAfterSetup>0&&secondaryAfterSetup<10000,"Secondary survives60seconds of actual ordinary Speed damage");
            expectedRecipients=EngageFoesFixture.ExpectedRecipients(b.World.CreationSnapshot(),target);
            fullHp=await EngageFoesFixture.RequireFullHealthAsync(b,target);Check.Equal(500,fullHp,"Untouched Trainman primary is the exact generic-retaliation predicate operand");baseline=context.Server.DebugLog();
            var below=EngageFoesFixture.CurrentNpc(b.World.CreationSnapshot(),"WinterHelper");
            initialIs=RefusalOwnValue(b,"InnerStrengthAvailable","02 80 80 80 10");
            int viewCursor=b.World.ViewSnapshot().Count;b.SkipReceived();
            request=new AbilityUseRecord(610,DuelPackets.Handle(target.ViewId,target.SpawnId),0,0,0,0,0x6b,0x6b,0,0).Encode();
            Check.Equal(43,request.Length,"Full native-layout80b9 request");b.SendRpc(request);
            await b.WaitUntilAsync(()=>context.Server.DebugLog()[baseline.Length..].Split('\n').Any(line=>line.Contains($"[ENGAGE-FOES] source={b.CharId} ") && line.Contains($" view={target.ViewId} ")),"610 actual successful per-recipient initializer",TimeSpan.FromSeconds(15));
            ownedRoundCursor=b.World.RpcSnapshot().Count;
            await b.WaitUntilAsync(()=>context.Server.DebugLog()[baseline.Length..].Contains($" evaluated={b.CharId} owner={b.CharId} diverted=False"),"Existing secondary NPC round consumer attributes an attack attempt to new most-hated owner",TimeSpan.FromSeconds(15));
            await b.WaitUntilAsync(()=>b.World.RpcSnapshot().Skip(ownedRoundCursor).Any(r=>r.Bytes.Length>0&&r.Bytes[0]==0x45),"existing B/WinterFrost fight's current ordinary combat round after initializer",TimeSpan.FromSeconds(15));
            string after=context.Server.DebugLog()[baseline.Length..];
            spent=ViewUpdateValues(b,2,"02 80 80 80 10",viewCursor).ToArray();Check.That(spent.Length>0,"Actual IS update after normal use");
            var isHistory=new[]{(ushort)initialIs}.Concat(spent).ToArray();
            Check.That(isHistory.Zip(isHistory.Skip(1)).Any(pair=>pair.First-pair.Second==50),"Actual50IS payment distinct from ordinary regen/Speed round updates");
            Check.That(b.World.RpcSnapshot().Any(r=>r.Hex=="80bd016202040000f04101"),"Actual30second610 reuse timer");
            Check.That(after.Contains("[EFFECTS] 610 on Trainman: State 93 0 6 s"),"Existing NPC Enraged93 six-second policy retained on actual landing");
            var applied=Regex.Matches(after,$@"\[ENGAGE-FOES\] source={b.CharId} entity=(\d+) view=(\d+) spawn=(\d+) death=(\d+) most-hated applied");
            var actualRecipients=applied.Cast<Match>().Select(m=>ushort.Parse(m.Groups[2].Value)).Order().ToArray();
            Check.That(actualRecipients.SequenceEqual(expectedRecipients),"Exact recipient set derived independently from actual live NPC creation poses within6m/samefloor");
            Check.That(expectedRecipients.Length>=2,"Primary and engaged secondary are both actually in range");
            var secondaryApplied=applied.Cast<Match>().Single(m=>ushort.Parse(m.Groups[2].Value)==secondary!.ViewId);
            string secondaryEntity=secondaryApplied.Groups[1].Value;
            await b.WaitUntilAsync(()=>context.Server.DebugLog()[baseline.Length..].Contains($"turn entity={secondaryEntity} evaluated={a.CharId} owner={b.CharId} diverted=True"),
                "WinterFrost's existing A fight diverts to B's actual most-hated record",TimeSpan.FromSeconds(15));
            after=context.Server.DebugLog()[baseline.Length..];
            Check.That(after.Contains($"[ABILITY] 610 lands on view {target.ViewId} for 0"),"Actual primary ability landing damage is zero, separately from subsequent ordinary combat-opening damage");
            Check.That(after.Contains($"[ABILITY] 610 lands on view {secondary!.ViewId} for 0"),"Actual secondary ability landing damage is zero");
            Check.That(applied.GroupBy(m=>m.Groups[1].Value).All(g=>g.Count()==1),"Exactly one initializer per actual NPC recipient");
            if(below!=null)Check.That(!applied.Any(m=>ushort.Parse(m.Groups[2].Value)==below.ViewId),"Different-floor NPC receives no initializer");
            Check.That(!after.Contains($"[ENGAGE-FOES] source={a.CharId} "),"Blocker never casts or receives an invented taunt");
            Check.Equal(0,a.World.Problems.Count+b.World.Problems.Count,"Normal-wire parser and counter health");
            b.SendRpc("44 00");a.SendRpc("44 00");
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-engage-foes-"+context.Database.Name);
            await File.WriteAllTextAsync(stem+"-debug.log",context.Server.DebugLog());
            await File.WriteAllTextAsync(stem+"-server.log",context.Server.FullOutput());
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{
                SourceAssembly=context.Server.SourceAssembly,SourceSha256=sourceHash,SentAbilityRpc=request==null?null:Convert.ToHexString(request),
                FullHpPrimaryBeforeCast=fullHp,Primary=target,Secondary=secondary,ExpectedRecipientViews=expectedRecipients,SecondaryHealthAfter60s=secondaryAfterSetup,DefaultSpeedSurvivalSeconds=survivalSeconds,DefaultSpeedRoundCount=speedRoundCount,DefaultSpeedRoundDamage=speedDamage,
                PrivateInitialNpcSeed=File.Exists(Path.Combine(context.Server.WorkingDirectory,"engage-foes-initial-npcs.json"))?File.ReadAllText(Path.Combine(context.Server.WorkingDirectory,"engage-foes-initial-npcs.json")):null,GenericRetaliationOperand="Only primary.health<primary.max in AbilityTargets.Land; primary Trainman is500/500 immediately before cast",ActorA=a.CharId,ActorB=b.CharId,BaselineLogLength=baseline.Length,InitialInnerStrength=initialIs,CurrentOperationIsUpdates=spent,OwnerRoundRpcCursor=ownedRoundCursor,
                Requests="A ordinary Block/40 helper, B ordinary40 DEFAULT Speed on initial10000HP WinterFrost; B survives60s; B ordinary610/80b9 targeting untouched Trainman; read-only combatdepth status; ordinary44 leave",NoRandomControls=true,
                Players=new[]{a,b}.Select(p=>new{p.CharId,p.Handle,Rpcs=p.World.RpcSnapshot(),RawViews=p.World.ViewSnapshot().Select(Convert.ToHexString),Creations=p.World.CreationSnapshot(),Problems=p.World.Problems}),
                Failure=failure?.ToString(),NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
