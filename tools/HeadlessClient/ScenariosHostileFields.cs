namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task BuffDispelProcNpc(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(NearNpc("NpcProcCaster") with {InnerStrength=2000,MaxInnerStrength=2000});
        await LoadAbilitiesAsync(context,caster,133);
        await caster.EnterWorldAsync();
        var view=await caster.WaitForViewNamedAsync("WinterFrost");
        bool skipped=false;
        for(int attempt=0;attempt<8;attempt++)
        {
            int before=context.Server.DebugLog().Split("261 lands on view").Length;
            int failures=caster.Received("81 67 ?? ?? ?? ?? 97 01 00 c6").Count;
            Cast(caster,261,view.ViewId);
            await caster.WaitUntilAsync(()=>context.Server.DebugLog().Split("261 lands on view").Length>before,
                "Normal261 lands on NPC",TimeSpan.FromSeconds(12));
            await Task.Delay(500);
            if(caster.Received("81 67 ?? ?? ?? ?? 97 01 00 c6").Count==failures) {skipped=true;break;}
            await Task.Delay(10500);
        }
        Check.That(skipped,"NPC no-upgrade failure output is skipped when261 does not proc");
        Check.Equal(0,caster.World.Problems.Count,"NPC dispel proc protocol");
    }

    // Real 80 b9 dispatch, outside interlock: a landed 261 may leave an upgrade intact.
    // Deterministic unit tests cover both sides and the exact 0.2 boundary; this bounded
    // smoke test never changes production RNG or adds a dev chance override.
    private static async Task BuffDispelProcPvp(ScenarioContext context)
    {
        var caster = await context.AddPlayerAsync(new CharacterSeed("ProcCaster") { Level=50, Organization=1, InnerStrength=2000, MaxInnerStrength=2000 });
        var target = await context.AddPlayerAsync(new CharacterSeed("ProcTarget") { X=17120, Level=50, Organization=2, Health=10000, MaxHealth=10000 });
        await LoadAbilitiesAsync(context,caster,133);
        await caster.EnterWorldAsync(); await target.EnterWorldAsync();
        target.SendChat("?pvpflag on"); await target.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        var view = await caster.WaitForViewNamedAsync(target.Handle);
        //600s Gaussian Blur,8s recast: each proc retry can restore a real active upgrade.
        Cast(target,49); await target.WaitForRpcAsync(ModPattern(49,520));
        bool skipped=false;
        for(int attempt=0;attempt<8;attempt++)
        {
            int before=context.Server.DebugLog().Split("MachinegunFistComboAbility lands on ProcTarget").Length;
            int off=target.Received(ModPattern(49,520,true)).Count;
            Cast(caster,261,view.ViewId);
            await target.WaitUntilAsync(()=>context.Server.DebugLog().Split("MachinegunFistComboAbility lands on ProcTarget").Length>before,
                "Normal Machinegun Fist Combo lands",TimeSpan.FromSeconds(12));
            await Task.Delay(500);
            if(target.Received(ModPattern(49,520,true)).Count==off) { skipped=true; break; }
            await Task.Delay(10500);
            Cast(target,49); await target.WaitForRpcAsync(ModPattern(49,520));
        }
        Check.That(skipped,"A landed261 skips its20% dispel proc within eight attempts");
        await PrepareFreeCastViralFixture(context,caster,324);
        Cast(caster,324,view.ViewId);
        await target.WaitForRpcAsync(ModPattern(49,520,true),TimeSpan.FromSeconds(10));
        Check.That(context.Server.DebugLog().Contains("324 removes buff 49 from ProcTarget"),"Unqualified Delete Upgrade still removes the surviving upgrade");
        Check.Equal(0,caster.World.Problems.Count+target.World.Problems.Count,"Proc-qualified dispel protocol");
    }

    /// <summary>Hostile fields, viruses and effect removal between players under PvP rules.</summary>
    private static async Task HostileFieldsPvp(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("FieldHacker") { Level = 50, Organization = 1 },
            new CharacterSeed("FieldVictim") { X = 17320, Level = 50, Organization = 2, Health = 1000, MaxHealth = 1000 },
            new CharacterSeed("FieldBystander") { X = 17420, Level = 50, Organization = 2, Health = 1000, MaxHealth = 1000 });
        var caster = players[0]; var victim = players[1]; var bystander = players[2];
        foreach (var target in new[] { victim, bystander })
        {
            target.SendChat("?pvpflag on");
            await target.WaitForViewUpdateAsync(2, OwnPvpFlagOn);
        }
        var victimView = await caster.WaitForViewNamedAsync("FieldVictim");
        await caster.WaitForViewNamedAsync("FieldBystander");

        // Fumble Field: radius 8 around the target, so the bystander one metre away is caught too
        await PrepareFreeCastViralFixture(context,caster,324,350,361,476);
        Cast(caster, 361, victimView.ViewId);
        await victim.WaitForRpcAsync(ModPattern(361, 524), TimeSpan.FromSeconds(10));
        await bystander.WaitForRpcAsync(ModPattern(361, 1032), TimeSpan.FromSeconds(10));
        await victim.WaitForRpcAsync("81 67 ?? ?? ?? ?? 18 00 00 c4"); // "%2s's %1z disarmed you!" (0xc4000018)

        // Code Infection 1.0: 100 over 30 s (times the caster's viral influence), ticks every 2.5 s
        await Task.Delay(500);
        Cast(caster, 350, victimView.ViewId);
        await victim.WaitForRpcAsync(ModPattern(350, 1157), TimeSpan.FromSeconds(10));
        await victim.WaitForRpcAsync("81 67 ?? ?? ?? ?? 1a 00 00 c6"); // "%2s's %1z infects you!"
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains("350 ticks on FieldVictim for "),
            "Code Infection ticks on the player", TimeSpan.FromSeconds(10));

        // Destroy Health lowers the victim's maximum health by the stored 520
        await Task.Delay(500);
        Cast(caster, 476, victimView.ViewId);
        await victim.WaitForRpcAsync(ModPattern(476, 106), TimeSpan.FromSeconds(10));
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains("476 on FieldVictim"), "Destroy Health lands", TimeSpan.FromSeconds(5));

        // Delete Upgrade removes the bystander's buff, never the bystander's debuffs
        Cast(bystander, 327);
        await bystander.WaitForRpcAsync(ModPattern(327, 172), TimeSpan.FromSeconds(10));
        var bystanderView = await caster.WaitForViewNamedAsync("FieldBystander");
        await Task.Delay(500);
        Cast(caster, 324, bystanderView.ViewId);
        await bystander.WaitForRpcAsync(ModPattern(327, 172, true), TimeSpan.FromSeconds(10));
        Check.That(context.Server.DebugLog().Contains("324 removes buff 327 from FieldBystander"), "Delete Upgrade removes the buff");
        Check.Equal(0, bystander.Received(ModPattern(361, 1032, true)).Count, "Delete Upgrade leaves the Fumble Field debuff");

        // Negative Condition Sweep 2.0 on oneself: a 25 s pulse record, then periodic removal of debuffs only
        Cast(victim, 424);
        await victim.WaitForRpcAsync(ModPattern(424, 424), TimeSpan.FromSeconds(10));
        Check.Equal(0, caster.World.Problems.Count + victim.World.Problems.Count + bystander.World.Problems.Count,
            "Hostile field protocol");
    }

    /// <summary>Miasma 1.0's toggled aura and Plague Zone 1.0 against NPCs.</summary>
    private static async Task HostileFieldsNpc(ScenarioContext context)
    {
        var caster = (await context.AddPlayersInWorldAsync(NearNpc("AuraHacker") with { InnerStrength = 200 }))[0];
        var frost = await caster.WaitForViewNamedAsync("WinterFrost");
        Cast(caster, 61);
        await caster.WaitForRpcAsync("80 bd 05 3d 00 00 00 00 00 00 01", TimeSpan.FromSeconds(10));
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains("[EFFECTS] 701 on WinterFrost: Stat"),
            "Miasma pulse debuffs the nearby NPC", TimeSpan.FromSeconds(10));
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Split('\n').Count(l => l.Contains("[AURA] 61 pulse 701")) >= 2,
            "The aura pulses again after its interval", TimeSpan.FromSeconds(10));
        await Task.Delay(1000);
        Cast(caster, 61);
        await caster.WaitForRpcAsync("80 bd 05 3d 00 00 00 00 00 00 00", TimeSpan.FromSeconds(5));
        Check.That(context.Server.DebugLog().Contains("[AURA] 61 off"), "Using the aura again switches it off");

        await PrepareFreeCastViralFixture(context,caster,400);
        Cast(caster, 400, frost.ViewId);
        await caster.WaitUntilAsync(() => context.Server.DebugLog().Contains("400 ticks on WinterFrost for 10"),
            "Plague Zone 1.0 ticks int(59 / 15 * 2.5 + .5)", TimeSpan.FromSeconds(12));
        Check.Equal(0, caster.World.Problems.Count, "Hostile aura protocol");
    }
}
