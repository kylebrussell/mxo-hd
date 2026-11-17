using System.Text;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class GameplayBreadthMode:NativeQaMode
{
    public override string Name=>"gameplay-breadth";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2},19403,60401,
        new[]{"DELETE FROM inventory WHERE charId=110"},new[]{"UPDATE char_abilities SET level=1 WHERE char_id=110"},
        new[]{"UPDATE characters SET level=2,exp=4900,alignment=3,cash=1000,y=-705,rotation=0,healthC=200,healthM=200,innerStrC=103,innerStrM=103,pvpflag=0 WHERE charId=110",
            "UPDATE characters SET x=17050,y=495,z=2693,alignment=3,pvpflag=0 WHERE handle='QAEnemy'"},
        DevCommands:true,missionFixture:"team-progress.xml",
        environmentOverrides:new(){["MXO_MOBS"]="world",["MXO_AGGRO"]="1",["MXO_MOB_WANDER"]="1",["MXO_QA_MOB_WANDER_SEED"]="42"},
        readyMessages:new[]{"CSR110 starts level2/XP4900. Watch the Chopper wander, then kill one normally: expect335XP, level3/5235XP, MaxHP250/MaxIS106; relog without reseeding.",
            "Use the printed chase setup and watch the NPC approach before interlock. Then ?tp 17020 495 2693 0 0 0, invite QAEnemy to your team, call Flood and accept Team Progress Fixture. Tell QAEnemy 'assist'; it accepts your invite and fights Team Security. Your goal1 must tick; talk to Team Contact for one completion. See docs/GAMEPLAY-BREADTH-QA.md."});

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if(context.OwnedDatabase==null||!context.OwnedDatabase.Name.StartsWith("mxotest_",StringComparison.Ordinal)||context.ActualServer==null)
            throw new InvalidOperationException("Gameplay breadth requires an owned fixture server.");
        var helper=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");
        context.Helpers.Add("QAEnemy",helper);await helper.EnterWorldAsync();
        var chase=WorldNpcMotionFixture.Chase(Path.GetDirectoryName(context.ActualServer.SourceAssembly)!);
        Console.WriteLine($"READY chase: ?tp {chase.Start.x:0.###} {chase.Start.y:0.###} {chase.Start.z:0.###} 0 0 0; nearby {chase.Name}. Withdraw after the approach, then return to Mara.");
        _=RunHelper(context,helper);
        return Array.Empty<byte>();
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
