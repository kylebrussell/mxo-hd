namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task ResistDamageRadius(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(new CharacterSeed("RadiusCaster"){Level=50,InnerStrength=2000,MaxInnerStrength=2000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={caster.CharId}");
        await LoadAbilitiesAsync(context,caster,371,74);
        await context.Database.ExecuteAsync($"UPDATE char_abilities SET is_loaded=0 WHERE char_id={caster.CharId} AND slot=401");
        await caster.EnterWorldAsync();
        var near=await context.AddPlayerAsync(new CharacterSeed("RadiusNear"){Level=50,X=17620,Health=100,MaxHealth=5000});
        var edge=await context.AddPlayerAsync(new CharacterSeed("RadiusExpanded"){Level=50,X=17820,Health=100,MaxHealth=5000});
        var far=await context.AddPlayerAsync(new CharacterSeed("RadiusOutside"){Level=50,X=17920,Health=100,MaxHealth=5000});
        // Execute the actual native-mode SQL builder on this disposable DB and verify its boundary coordinates.
        await context.Database.ExecuteAsync(ResistDamageFixture.TargetsSql(near.CharId,edge.CharId,far.CharId,-1,17020,2693));
        Check.Equal(17820L,await context.Database.LongAsync($"SELECT x FROM characters WHERE charId={edge.CharId}"),"Native fixture expanded coordinate");
        Check.Equal(5000L,await context.Database.LongAsync($"SELECT healthM FROM characters WHERE charId={far.CharId}"),"Native fixture target maximum");
        foreach(var member in new[]{near,edge,far})
        {
            await member.EnterWorldAsync();caster.SendRpc(GroupInvite(Team,member.Handle,""));
            await member.WaitForRpcAsync("80 8f 0c 00");member.SendRpc(InviteAnswer(Team,0,caster.Handle));await member.WaitForRpcAsync("80 8e");
        }
        Cast(caster,371);
        await caster.WaitUntilAsync(()=>context.Server.DebugLog().Contains("Heal 371 -> RadiusNear"),"baseline group radius",TimeSpan.FromSeconds(12));
        Check.That(!context.Server.DebugLog().Contains("Heal 371 -> RadiusExpanded"),"Unloaded74 excludes eight-metre teammate");
        Check.That(!context.Server.DebugLog().Contains("Heal 371 -> RadiusOutside"),"Baseline excludes nine-metre teammate");
        SetAbilityLoaded(caster,401,74,50,true);await caster.WaitForRpcAsync(PassiveRecord(74,127,24));
        await Task.Delay(15000);Cast(caster,371);
        await caster.WaitUntilAsync(()=>context.Server.DebugLog().Contains("Heal 371 -> RadiusExpanded"),"loaded74 expands seven metres to8.68",TimeSpan.FromSeconds(12));
        Check.That(!context.Server.DebugLog().Contains("Heal 371 -> RadiusOutside"),"Nine metres remains outside expanded radius");
        SetAbilityLoaded(caster,401,74,50,false);await caster.WaitForRpcAsync(PassiveRecord(74,127,24,true));
        foreach(var p in new[]{caster,near,edge,far})Check.Equal(0,p.World.Problems.Count,"Radius protocol problems");
    }
}
