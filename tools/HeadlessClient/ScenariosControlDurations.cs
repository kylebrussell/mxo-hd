namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task AbilityControlDurationBonuses(ScenarioContext context)
    {
        var caster = await context.AddPlayerAsync(new CharacterSeed("DurationCaster") { Level = 50, Organization = 1 });
        var stunned = await context.AddPlayerAsync(new CharacterSeed("LongStun") { X = 17120, Level = 50, Organization = 2 });
        var rooted = await context.AddPlayerAsync(new CharacterSeed("LongRoot") { X = 17220, Level = 50, Organization = 2 });
        int Goid(ushort ability) => unchecked((int)(0x80000000u | ((uint)ability << 10)));
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={caster.CharId}; " +
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            $"({caster.CharId},300,{Goid(384)},50,1),({caster.CharId},301,{Goid(278)},50,1)");
        foreach (var player in new[] { caster, stunned, rooted }) await player.EnterWorldAsync();
        foreach (var victim in new[] { stunned, rooted })
        {
            victim.SendChat("?pvpflag on");
            await victim.WaitForViewUpdateAsync(2, OwnPvpFlagOn);
        }
        var stunView = await caster.WaitForViewNamedAsync(stunned.Handle);
        var rootView = await caster.WaitForViewNamedAsync(rooted.Handle);
        await PrepareFreeCastViralFixture(context,caster,40,315,330);
        Cast(caster, 315, stunView.ViewId);
        var stun = await stunned.WaitForRpcAsync(ModPattern(315, 102));
        Check.That(Math.Abs(BitConverter.ToSingle(stun.Bytes, 18) - 8.05f) < .0001f, "Stun packet carries the boosted fractional duration");
        DateTime stunnedAt = DateTime.UtcNow;
        var immunity = stunned.Received(ModPattern(734, 681)).Last();
        Check.Equal(14f, BitConverter.ToSingle(immunity.Bytes, 18), "Explicit immunity timer remains14 seconds");
        Cast(stunned, 967);
        await stunned.WaitForTextAsync("cannot act while stunned");
        Cast(caster, 40, rootView.ViewId);
        var root = await rooted.WaitForRpcAsync(ModPattern(40, 99));
        Check.Equal(27.5f, BitConverter.ToSingle(root.Bytes, 18), "Root packet uses its separate10% duration bonus");
        Cast(caster, 330, rootView.ViewId);
        await rooted.WaitForRpcAsync(ModPattern(40, 99, true));
        Check.That(context.Server.DebugLog().Contains("lands on LongRoot for"), "Damage still breaks the boosted root");
        double wait = (stunnedAt.AddSeconds(8.6) - DateTime.UtcNow).TotalMilliseconds;
        if (wait > 0) await Task.Delay((int)wait);
        await stunned.WaitForRpcAsync(ModPattern(315, 102, true));
        Cast(stunned, 967);
        await stunned.WaitUntilAsync(() => context.Server.DebugLog().Contains("Heal 967 -> LongStun for 50"),
            "The target can act after the boosted stun expires", TimeSpan.FromSeconds(10));
        Check.Equal(0, caster.World.Problems.Count + stunned.World.Problems.Count + rooted.World.Problems.Count,
            "Control duration protocol");
    }

    private static async Task AbilityControlDurationNpc(ScenarioContext context)
    {
        var caster = await context.AddPlayerAsync(NearNpc("DurationNpc") with { Level = 50 });
        int ability = unchecked((int)(0x80000000u | (384u << 10)));
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={caster.CharId}; " +
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES ({caster.CharId},300,{ability},50,1)");
        await caster.EnterWorldAsync();
        var mob = await caster.WaitForViewNamedAsync("WinterFrost");
        await PrepareFreeCastViralFixture(context,caster,315);
        Cast(caster, 315, mob.ViewId);
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains("[EFFECTS] 315 on WinterFrost: State 102 0 8.05 s"),
            "NPC state receives the same fractional caster duration bonus", TimeSpan.FromSeconds(10));
        Check.Equal(0, caster.World.Problems.Count, "NPC control duration protocol");
    }
}
