namespace HeadlessClient;
public static partial class Scenarios
{
    private static void HealthBoostItemUse(HeadlessPlayer p, ushort ability, byte slot)
    {
        byte[] body=new byte[43]; body[0]=0x80;body[1]=0xb9;
        BitConverter.GetBytes(ability).CopyTo(body,2);body[36]=8;body[37]=slot;body[38]=1;
        p.SendRpc(WireFormat.ToHex(body));
    }
    private static async Task HealthBoostPillUse(ScenarioContext context)
    {
        foreach(var pair in new[]{(Ability:(ushort)187,Item:2407u,Amount:100),(Ability:(ushort)672,Item:39411u,Amount:100),
            (Ability:(ushort)188,Item:39412u,Amount:200),(Ability:(ushort)189,Item:39413u,Amount:300),(Ability:(ushort)673,Item:39414u,Amount:400)})
        {
            var p=await context.AddPlayerAsync(new CharacterSeed("Boost"+pair.Ability){Level=50,MaxHealth=500,Health=400,Items=new[]{(1,pair.Item,2),(2,2399u,1)}});
            await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}");
            await p.EnterWorldAsync();
            Cast(p,pair.Ability); await p.WaitForRpcAsync($"80 ba {Le(pair.Ability)}");
            await Task.Delay(650); p.SkipReceived();
            HealthBoostItemUse(p,pair.Ability,2); await p.WaitForRpcAsync($"80 ba {Le(pair.Ability)}");
            await Task.Delay(650);p.SkipReceived();
            if(pair.Ability==672)p.SendRpc("80 c6 01 00"); else HealthBoostItemUse(p,pair.Ability,1);
            await p.WaitForRpcAsync($"80 ac {Le(pair.Ability)}");
            Check.Equal(2L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=1"),"Cast does not spend early");
            await p.WaitForRpcAsync(ModPattern(pair.Ability,106));
            await p.WaitForRpcAsync($"80 bd 01 {Le(pair.Ability)} 04 00 00 f0 41 01");
            Check.Equal(1L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=1"),"Exactly one owned pill spent");
            Check.Equal(0,p.Received("80 bd 01 c7 02").Count,"No fabricated shared HealPill timer");
            p.SendRpc("80 c6 01 00");await p.WaitForRpcAsync($"80 ba {Le(pair.Ability)}");
            p.SendRpc($"80 bb {Le(pair.Ability)}");await p.WaitForRpcAsync(ModPattern(pair.Ability,106,true));
            Check.Equal(0,p.World.Problems.Count,"Health boost presentation protocol");p.Dispose();
        }
        var low=await context.AddPlayerAsync(new CharacterSeed("LowBoost"){Level=1,Items=new[]{(1,39411u,1)}});
        await low.EnterWorldAsync();HealthBoostItemUse(low,672,1);await low.WaitForRpcAsync("80 ba a0 02");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={low.CharId} AND slot=1"),"Level gate retains pill");
        var moved=await context.AddPlayerAsync(new CharacterSeed("MovedBoost"){Level=50,Items=new[]{(1,39414u,1)}});
        await moved.EnterWorldAsync();HealthBoostItemUse(moved,673,1);await moved.WaitForRpcAsync("80 ac a1 02");
        await context.Database.ExecuteAsync($"UPDATE inventory SET slot=2 WHERE charId={moved.CharId} AND slot=1");
        await Task.Delay(1300);Check.Equal(0,moved.Received(ModPattern(673,106)).Count,"Moved pill applies no buff");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={moved.CharId} AND slot=2"),"Moved pill retained");
        var zone=await context.AddPlayerAsync(new CharacterSeed("ZoningBoost"){Level=50,Items=new[]{(1,39414u,1)}});
        await zone.EnterWorldAsync();HealthBoostItemUse(zone,673,1);await zone.WaitForRpcAsync("80 ac a1 02");
        await GoToLoadingAreaAsync(zone);await Task.Delay(1300);
        Check.Equal(1L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={zone.CharId} AND slot=1"),"Zoning cancels cast without spending");
        Check.Equal(0,zone.Received(ModPattern(673,106)).Count,"Zoning applies no boost");
        var stun=await context.AddPlayerAsync(new CharacterSeed("StunnedBoost"){Level=50,Items=new[]{(1,39414u,1)}});
        await stun.EnterWorldAsync();stun.SendChat("?combatdepth stun");await stun.WaitForRpcAsync(ModPattern(315,102));
        HealthBoostItemUse(stun,673,1);await stun.WaitForRpcAsync("80 ba a1 02");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={stun.CharId} AND slot=1"),"Stun gate retains pill");
    }
}

