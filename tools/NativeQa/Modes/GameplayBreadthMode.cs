using System.Text;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class GameplayBreadthMode:NativeQaMode
{
    public override string Name=>"gameplay-breadth";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2},19403,60401,
        new[]{"DELETE FROM inventory WHERE charId=110"},new[]{"UPDATE char_abilities SET level=1 WHERE char_id=110"},
        new[]{"UPDATE characters SET level=2,exp=4900,alignment=3,cash=1000,y=-705,rotation=0,healthC=200,healthM=200,innerStrC=103,innerStrM=103,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET x={TeamMissionFixture.HelperX},y={TeamMissionFixture.Y},z={TeamMissionFixture.Z},alignment=3,pvpflag=0 WHERE handle='QAEnemy'",
            $"UPDATE characters SET x={TeamMissionFixture.HolderX},y={TeamMissionFixture.Y},z={TeamMissionFixture.Z},alignment=3,pvpflag=0 WHERE handle='QAFriend'"},
        DevCommands:true,missionFixture:"team-progress.xml",
        environmentOverrides:new(){["MXO_MOBS"]="world",["MXO_AGGRO"]="1",["MXO_MOB_WANDER"]="1",["MXO_QA_MOB_WANDER_SEED"]="42"},
        readyMessages:new[]{"CSR110 starts level2/XP4900. Watch the Chopper wander, then kill one normally: expect335XP, level3/5235XP, MaxHP250/MaxIS106; relog without reseeding.",
            $"For the team check, use ?tp {TeamMissionFixture.HolderX} {TeamMissionFixture.Y} {TeamMissionFixture.Z} 0 0 0. Invite QAEnemy to your team, call Flood and accept Team Progress Fixture, then tell QAEnemy 'assist'. Verify goal 1 credits its kill and talk to Team Contact for one completion. See docs/GAMEPLAY-BREADTH-QA.md."});

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if(context.OwnedDatabase==null||!context.OwnedDatabase.Name.StartsWith("mxotest_",StringComparison.Ordinal)||context.ActualServer==null)
            throw new InvalidOperationException("Gameplay breadth requires an owned fixture server.");
        var helper = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy");
        context.Helpers.Add("QAEnemy", helper);
        await helper.EnterWorldAsync();
        // Observe a whole aggro retry interval while a separate character verifies the actual mission actors.
        await Task.WhenAll(CheckTeamMissionAsync(context), Task.Delay(TimeSpan.FromSeconds(hds.MobAggro.RetrySeconds + 1)));
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer, helper);
        var chase=WorldNpcMotionFixture.Chase(Path.GetDirectoryName(context.ActualServer.SourceAssembly)!);
        Console.WriteLine($"Chase:  ?tp {chase.Start.x:0.###} {chase.Start.y:0.###} {chase.Start.z:0.###} 0 0 0; nearby {chase.Name}. Withdraw after the approach, then return to Mara.");
        _=RunHelper(context,helper);
        return Array.Empty<byte>();
    }
    private static async Task CheckTeamMissionAsync(NativeQaBootstrapContext context)
    {
        using var probe = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        await probe.EnterWorldAsync();
        probe.SendRpc("80 94 05 00 03");
        await probe.WaitForTextAsync("Team Progress Fixture");
        await probe.WaitForRpcAsync("80 97 05 00");
        probe.SendRpc("80 98 06 00 00 00 01 02 00");
        await probe.WaitForRpcAsync("80 99");
        probe.SendRpc("80 9b 07 00 01");
        await probe.WaitForRpcAsync("80 9c");
        foreach (string name in new[] { "Team Security", "Team Contact" })
        {
            var actor = await probe.WaitForViewNamedAsync(name);
            Check.That(actor.Goid == 599 && actor.Attributes.TryGetValue("Health", out var hp)
                && hp.Length == 2 && BitConverter.ToUInt16(hp) > 0, $"{name} is present and alive");
        }
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, probe);
        probe.SendRpc("80 a6");
        await probe.WaitForRpcAsync("80 a2 08");
        probe.SkipReceived();
        probe.SendRpc("80 fe 00");
        await probe.WaitForRpcAsync("80 fd");
        Check.Equal(0, probe.World.Problems.Count, "Team mission fixture probe has no wire errors");
        Check.Equal(0L, await context.OwnedDatabase!.LongAsync($"SELECT COUNT(*) FROM char_active_missions " +
            $"WHERE charId={probe.CharId} AND payload IS NOT NULL"), "The probe aborted its own active mission");
    }
    private static async Task RunHelper(NativeQaBootstrapContext context,HeadlessPlayer helper)
    {
        int cursor=helper.World.RpcSnapshot().Count;bool joined=false,assist=false,attacked=false;
        try
        {
            while(!context.ActualServer!.HasExited)
            {
                var records=helper.World.RpcSnapshot();
                foreach(var rpc in records.Skip(cursor))
                {
                    var b=rpc.Bytes;
                    if(b.Length>=4&&b[0]==0x80&&b[1]==0x8f&&b[2]==0x0c&&b[3]==0&&Encoding.UTF8.GetString(b).Contains("CSR_Tester",StringComparison.Ordinal))
                    {
                        var name=Encoding.UTF8.GetBytes("CSR_Tester\0");
                        helper.SendRpc(new byte[]{0x6f,3,8,0,0,0,0,0}.Concat(BitConverter.GetBytes((ushort)name.Length)).Concat(name).ToArray());
                    }
                    if(b.Length>=2&&b[0]==0x80&&b[1]==0x8e)joined=true;
                    if(b.Length>7&&b[0]==0x2e&&BitConverter.ToUInt32(b,3)==110&&Encoding.UTF8.GetString(b).Contains("assist",StringComparison.OrdinalIgnoreCase))assist=true;
                }
                cursor=records.Count;
                if(joined&&assist&&!attacked)
                {
                    var target=helper.World.CreationSnapshot().LastOrDefault(v=>v.Goid==599&&v.Name=="Team Security");
                    if(target!=null)
                    {
                        helper.SendRpc("3d 01");
                        helper.SendRpc($"40 {WireFormat.ToHex(BitConverter.GetBytes(target.ViewId))} {WireFormat.ToHex(BitConverter.GetBytes((ushort)target.SpawnId))} 00 00 00 00 00 00 00");
                        attacked=true;Console.WriteLine("Breadth helper: ordinary teammate attack requested.");
                    }
                }
                await Task.Delay(250);
            }
        }
        catch(Exception error) when(context.ActualServer!.HasExited||!helper.World.CurrentOwnViewLife().Current)
        { Console.WriteLine("Breadth helper stopped: "+error.GetType().Name); }
        catch(Exception error) { Console.WriteLine("Breadth helper failed: "+error.Message); }
    }
}
