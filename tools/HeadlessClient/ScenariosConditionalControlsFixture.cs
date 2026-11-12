namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task ConditionalDazeFixtureCommand(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(NearNpc("DazeFixtureCaller") with {Level=50,Organization=1,Health=5000,MaxHealth=5000,InnerStrength=1000,MaxInnerStrength=1000});
        var target=await context.AddPlayerAsync(NearNpc("DazeFixtureHelper",100) with {Level=50,Organization=2,Health=5000,MaxHealth=5000,InnerStrength=200,MaxInnerStrength=200});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({caster.CharId},{target.CharId})");
        await LoadAbilitiesAsync(context,caster,347,1135,1112);
        await caster.EnterWorldAsync();await target.EnterWorldAsync();
        caster.SendChat("?combatdepth daze");await caster.WaitForTextAsync("Select a live visible");
        caster.SendChat("?combatdepth daze NaN");await caster.WaitForTextAsync("Usage: ?combatdepth daze");
        var npc=await caster.WaitForViewNamedAsync("WinterFrost");
        caster.SendRpc($"81 51 {Le(npc.ViewId)} {(byte)(npc.SpawnId+1):x2}");
        caster.SendChat("?combatdepth daze");await caster.WaitForTextAsync("Select a live visible");
        caster.SendRpc($"81 51 {Le(npc.ViewId)} {npc.SpawnId:x2}");
        caster.SendChat("?combatdepth daze");await caster.WaitForTextAsync("applied884/199 to WinterFrost for 30 seconds");
        await caster.WaitUntilAsync(()=>context.Server.DebugLog().Contains("884 on WinterFrost: State 199"),"Real NPC Dazed state",TimeSpan.FromSeconds(5));
        Cast(caster,347,npc.ViewId);
        await caster.WaitUntilAsync(()=>context.Server.DebugLog().Contains("347 on WinterFrost: State 102"),"Prepared NPC receives conditional4s stun",TimeSpan.FromSeconds(8));
        await Task.Delay(8500);
        target.SendChat("?pvpflag on");await target.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        var view=await caster.WaitForViewNamedAsync(target.Handle);
        caster.SendRpc($"81 51 {Le(view.ViewId)} {view.SpawnId:x2}");
        caster.SendChat("?combatdepth daze 20");await caster.WaitForTextAsync("applied884/199 to DazeFixtureHelper for 20 seconds");
        var daze=await target.WaitForRpcAsync(ModPattern(884,199));
        Check.Equal(20f,BitConverter.ToSingle(daze.Bytes,18),"QA duration affects real player Daze state");
        Check.Equal(0,target.Received(ModPattern(734,681)).Count,"Daze preparation creates no stun immunity");
        Check.Equal(5000L,await context.Database.LongAsync($"SELECT healthC FROM characters WHERE charId={target.CharId}"),"Daze command leaves health untouched");
        Check.Equal(200L,await context.Database.LongAsync($"SELECT innerStrC FROM characters WHERE charId={target.CharId}"),"Daze command leaves Inner Strength untouched");
        Cast(caster,1135,view.ViewId);
        var stun=await target.WaitForRpcAsync(ModPattern(1135,102),TimeSpan.FromSeconds(8));
        Check.Equal(8f,BitConverter.ToSingle(stun.Bytes,18),"Spy throw retains original8s conditional callback");
        var immunity=target.Received(ModPattern(734,681)).Last();Check.Equal(8f,BitConverter.ToSingle(immunity.Bytes,18),"Production callback creates its own8s immunity");
        await target.WaitForRpcAsync(ModPattern(1135,102,true),TimeSpan.FromSeconds(10));
        Check.Equal(0,caster.World.Problems.Count+target.World.Problems.Count,"Selected-target daze fixture protocol");
    }

    private static async Task ConditionalDazeFixtureDisabled(ScenarioContext context)
    {
        var caller=(await context.AddPlayersInWorldAsync(new CharacterSeed("DisabledDaze")))[0];
        caller.SendChat("?combatdepth daze 30");await caller.WaitForTextAsync("requires MXO_DEV_COMMANDS=1");
        Check.Equal(0,caller.Received(ModPattern(884,199)).Count,"Disabled fixture adds no state");
    }
}
