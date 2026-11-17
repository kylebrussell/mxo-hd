using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static IReadOnlyDictionary<string,string> EarnedLaterLevelEnvironment => new Dictionary<string,string>
    { ["MXO_DEV_COMMANDS"]="1",["MXO_MOBS"]="world",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0" };

    private static async Task EarnedLaterLevel(ScenarioContext context,int level,int startingExperience,uint gain)
    {
        // Boundary setup only: all XP after login must come from an ordinary captured world NPC.
        string handle="EarnedLevel"+(level+1);
        var seed=OpenWorldFirstLevelFixture.Seed with {Handle=handle,Level=level,Experience=startingExperience,
            Health=100+50*level,MaxHealth=100+50*level,InnerStrength=97+3*level,MaxInnerStrength=97+3*level};
        var p=(await context.AddPlayersInWorldAsync(seed))[0];
        string where=" WHERE charId="+p.CharId;
        Check.Equal((long)startingExperience,await context.Database.LongAsync("SELECT exp FROM characters"+where),"Boundary XP is setup, never granted after login");
        var source=OpenWorldFirstLevelFixture.Spawns[0];OpenWorldCurrentNpc.Current? npc=null;
        await p.WaitUntilAsync(()=>{try{npc=OpenWorldCurrentNpc.Require(p.World.CurrentOwnViewLife(),source[0],source[1],source[2]);return true;}catch(InvalidDataException){return false;}},"ordinary Chopper Runner",TimeSpan.FromSeconds(20));
        var point=OpenWorldFirstLevelFixture.Approach(npc!.Position);
        await MoveForMissionTest(p,point.x,point.y,point.z);
        var current=OpenWorldCurrentNpc.Require(p.World.CurrentOwnViewLife(),source[0],source[1],source[2]);
        int rpc=p.World.RpcSnapshot().Count,view=p.World.ViewSnapshot().Count;
        p.SendRpc("3d 01");p.SendRpc($"40 {Le(current.View.ViewId)} {Le((ushort)current.View.SpawnId)} 00 00 00 00 00 00 00");
        await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(rpc).Any(r=>OpenWorldFirstLevelFixture.FightingGain(r.Bytes,gain)),"ordinary kill earns the next level",TimeSpan.FromSeconds(120));
        await p.WaitUntilAsync(()=>context.Database.LongAsync("SELECT level FROM characters"+where).GetAwaiter().GetResult()==level+1,"earned level committed",TimeSpan.FromSeconds(10));
        Check.Equal(startingExperience+(long)gain,await context.Database.LongAsync("SELECT exp FROM characters"+where),"Only the observed kill gain was credited");
        Check.Equal(1,p.World.RpcSnapshot().Skip(rpc).Count(r=>OpenWorldFirstLevelFixture.FightingGain(r.Bytes,gain)),"One XP gain per kill");
        ushort hp=(ushort)(100+50*(level+1)),strength=(ushort)(97+3*(level+1));
        var prefix=Convert.FromHexString($"020002808083{Le(hp).Replace(" ","")}{level+1:x2}D4{Le(strength).Replace(" ","")}");
        await p.WaitUntilAsync(()=>p.World.ViewSnapshot().Skip(view).Any(v=>v.AsSpan().IndexOf(prefix)>=0),"own-view level, max health and inner strength",TimeSpan.FromSeconds(10));
        Check.Equal((long)hp,await context.Database.LongAsync("SELECT healthM FROM characters"+where),"Maximum health grew by50");
        Check.Equal((long)strength,await context.Database.LongAsync("SELECT innerStrM FROM characters"+where),"Maximum inner strength grew by3");
        if(level==2)Check.That(p.World.RpcSnapshot().Skip(rpc).Any(r=>r.Bytes.AsSpan().IndexOf(BitConverter.GetBytes(0x8000002eu))>=0),"Level3 advice reaches the client");
        string username=await context.Database.ScalarAsync("SELECT username FROM users JOIN characters USING(userId)"+where);
        p.SkipReceived();p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");p.Dispose();await Task.Delay(TimeSpan.FromSeconds(33));
        using var restored=await HeadlessPlayer.LoginAsync(context.Server.Address,username,handle);await restored.EnterWorldAsync();
        Check.Equal((long)level+1,await context.Database.LongAsync("SELECT level FROM characters"+where),"Earned level survives ordinary quit/relog");
        Check.Equal(startingExperience+(long)gain,await context.Database.LongAsync("SELECT exp FROM characters"+where),"Earned XP survives relog without replay");
        Check.Equal(0,restored.World.RpcSnapshot().Count(r=>OpenWorldFirstLevelFixture.FightingGain(r.Bytes,gain)),"Relog never replays the kill reward");
    }
}
