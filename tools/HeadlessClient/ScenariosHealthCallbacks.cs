using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task SetProcHealth(HeadlessPlayer p,ushort value,ushort expected)
    {
        int before=p.World.ViewSnapshot().Count;
        p.SendChat("?health "+value);
        var pattern=BytePattern.Parse("02 00 02 80 80 80 40 "+Le(expected));
        await p.WaitUntilAsync(()=>p.World.ViewSnapshot().Skip(before).Any(pattern.Occurs),
            "New health update after callback: "+expected,TimeSpan.FromSeconds(8));
    }

    private static async Task ZenMaster(ScenarioContext context)
    {
        // Explicit forced level99 disposable actor: shipped parent871 is inaccessible at retail level50.
        var p=await context.AddPlayerAsync(new CharacterSeed("ZenMaster") {Level=99,Health=500,MaxHealth=500,InnerStrength=198,MaxInnerStrength=200});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}");
        await LoadAbilitiesAsync(context,p,871);
        await p.EnterWorldAsync();
        const string activation="81 67 ?? ?? ?? ?? 20 21 00 c6";
        await SetProcHealth(p,400,400);
        var restored=await p.WaitForRpcAsync(activation);
        int values=BitConverter.ToUInt16(restored.Bytes,19);
        Check.Equal((ushort)1,BitConverter.ToUInt16(restored.Bytes,values),"Restore line has its required numeric substitution");
        Check.That(BitConverter.ToInt32(restored.Bytes,values+2) is >=0 and <=5,"Restore line reports capped IS delta");
        await p.WaitForViewUpdateAsync(2,"02 80 80 80 10 c8 00");
        int previous=p.Received(activation).Count;
        await SetProcHealth(p,450,450); // external healing invokes the same sourced callback
        await p.WaitUntilAsync(()=>p.Received(activation).Count>previous,"Zen external-heal activation",TimeSpan.FromSeconds(5));
        await SetProcHealth(p,500,500);
        previous=p.Received(activation).Count;
        Cast(p,872);await Task.Delay(400);
        Check.Equal(previous,p.Received(activation).Count,"Direct872 refused without executing internal effect");
        SetAbilityLoaded(p,400,871,50,false);
        await p.WaitForRpcAsync("80 b3 67 03");
        previous=p.Received(activation).Count;
        await SetProcHealth(p,400,400);
        Check.Equal(previous,p.Received(activation).Count,"Unloaded permanent parent stops health procs");
        var normal=await context.AddPlayerAsync(new CharacterSeed("ZenRetail") {Level=50,Health=500,MaxHealth=500});
        int goid=unchecked((int)(0x80000000u | (871u<<10)));
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={normal.CharId}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({normal.CharId},400,{goid},30,0)");
        await normal.EnterWorldAsync();
        SetAbilityLoaded(normal,400,871,30,true);
        normal.SendRpc("81 48");await normal.WaitForRpcAsync("81 49");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={normal.CharId} AND slot=400"),"Retail50 normal80ae load rejects parent871 requirement99");
        Check.Equal(0,normal.Received("80 b2 67 03").Count,"Rejected parent never acknowledged loaded");
        await context.Database.ExecuteAsync($"UPDATE char_abilities SET is_loaded=1 WHERE char_id={normal.CharId} AND slot=400");
        SetAbilityLoaded(normal,400,871,30,false);
        await normal.WaitForRpcAsync("80 b3 67 03");
        normal.SendRpc("81 48");await normal.WaitForRpcAsync("81 49");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={normal.CharId} AND slot=400"),"Inaccessible legacy871 can still unload");
        await KillForResurrection(p);
        Check.Equal(0,p.World.Problems.Count+normal.World.Problems.Count,"Zen Master source health proc protocol");
    }

    private static async Task HealthProcLifecycle(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("HealthProc") {Level=20,Health=100,MaxHealth=1000,InnerStrength=1000,MaxInnerStrength=1000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}");
        await p.EnterWorldAsync();
        Cast(p,196);
        var proc=await p.WaitForRpcAsync(ModPattern(196,196));
        Check.Equal(10,BitConverter.ToInt32(proc.Bytes,13),"Fixture's native15-bit minimum sampled once");
        await SetProcHealth(p,90,100);
        await SetProcHealth(p,110,120); // script does not filter external healing
        p.SendChat("?dismissbuff 196");await p.WaitForRpcAsync(ModPattern(196,180,true));
        var off=await p.WaitForRpcAsync(ModPattern(196,196,true));
        Check.Equal(10,BitConverter.ToInt32(off.Bytes,13),"Original proc value retained on off record");
        await SetProcHealth(p,80,80);
        Cast(p,861);await p.WaitForRpcAsync(ModPattern(861,861),TimeSpan.FromSeconds(8));
        await SetProcHealth(p,70,120); // NeutralHeal minimum50, once per transaction
        await SetProcHealth(p,160,210);
        await KillForResurrection(p);
        Check.That(p.Received(ModPattern(861,861,true)).Count>0,"Death removed Balance before the death dialog");
        Check.Equal(0,p.World.Problems.Count,"Health callback lifecycle protocol");
    }

    private static async Task HealthReactiveArea(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("CallbackOwner") {Level=50,Organization=1,Health=1000,MaxHealth=1000,InnerStrength=2000,MaxInnerStrength=2000});
        var friend=await context.AddPlayerAsync(new CharacterSeed("CallbackFriend") {X=17120,Level=50,Organization=1,Health=1000,MaxHealth=1000});
        var enemy=await context.AddPlayerAsync(new CharacterSeed("CallbackEnemy") {X=17220,Level=50,Organization=2,Health=1000,MaxHealth=1000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({p.CharId},{friend.CharId},{enemy.CharId})");
        foreach(var actor in new[]{p,friend,enemy}) await actor.EnterWorldAsync();
        await p.WaitForViewNamedAsync(enemy.Handle);await p.WaitForViewNamedAsync(friend.Handle);
        enemy.SendChat("?pvpflag on");await enemy.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        p.SendChat("?pvpflag on");await p.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        Cast(p,941);await p.WaitForRpcAsync(ModPattern(941,941),TimeSpan.FromSeconds(8));
        Cast(enemy,941);await enemy.WaitForRpcAsync(ModPattern(941,941),TimeSpan.FromSeconds(8));
        p.SendChat("?health 900");
        const string reactiveDamage="81 67 ?? ?? ?? ?? f9 00 00 7c 00 00 10 00 00 00 b8 0e 80";
        var damage=await enemy.WaitForRpcAsync(reactiveDamage);
        int values=BitConverter.ToUInt16(damage.Bytes,19);
        Check.Equal(50,BitConverter.ToInt32(damage.Bytes,values+2),"Reactive neutral damage samples50 with no influence");
        await p.WaitForRpcAsync(reactiveDamage);
        await p.WaitForViewUpdateAsync(2,"02 80 80 80 40 52 03"); //900-50=850: each reactive parent once
        enemy.SendChat("?dismissbuff 941");await enemy.WaitForRpcAsync(ModPattern(941,941,true));
        p.SendChat("?dismissbuff 941");await p.WaitForRpcAsync(ModPattern(941,941,true));
        Cast(p,943);await p.WaitForRpcAsync(ModPattern(943,943),TimeSpan.FromSeconds(8));
        await SetProcHealth(friend,300,300);await SetProcHealth(enemy,200,200);
        int friendBefore=ViewUpdateValues(friend,2,"02 80 80 80 40").Count;
        int enemyBefore=ViewUpdateValues(enemy,2,"02 80 80 80 40").Count;
        await SetProcHealth(p,400,460);
        await friend.WaitUntilAsync(()=>ViewUpdateValues(friend,2,"02 80 80 80 40").Skip(friendBefore).Any(h=>h>=360&&h<=380),
            "Code Conversion heals a nearby friendly non-team player",TimeSpan.FromSeconds(5));
        Check.That(ViewUpdateValues(enemy,2,"02 80 80 80 40").Skip(enemyBefore).All(h=>h<250),"Code Conversion excludes the hostile player");
        p.SendChat("?dismissbuff 943");await p.WaitForRpcAsync(ModPattern(943,943,true));
        await SetProcHealth(p,300,300);
        Check.Equal(0,p.World.Problems.Count+friend.World.Problems.Count+enemy.World.Problems.Count,"Reactive and friendly health-area protocol");
    }

    private static async Task HealthDeflectLineup(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(LineUpActor("DeflectOwner") with {Organization=1,InnerStrength=2000,MaxInnerStrength=2000});
        var friend=await context.AddPlayerAsync(new CharacterSeed("DeflectFriend") {X=17120,Level=50,Organization=1});
        var enemy=await context.AddPlayerAsync(new CharacterSeed("DeflectEnemy") {X=17220,Level=50,Organization=2,InnerStrength=1000,MaxInnerStrength=1000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({friend.CharId},{enemy.CharId})");
        await LoadAbilitiesAsync(context,enemy,2,17,477,478,480,481,483);
        foreach(var actor in new[]{p,friend,enemy}) await actor.EnterWorldAsync();
        p.SendRpc(GroupInvite(Team,friend.Handle,""));await friend.WaitForRpcAsync("80 8f 0c 00");
        friend.SendRpc(InviteAnswer(Team,0,p.Handle));await friend.WaitForRpcAsync("80 8e");await p.WaitForRpcAsync($"70 03 {Le(friend.CharId)} 09 00");
        Cast(p,380);await p.WaitForRpcAsync("80 bd 05 7c 01 00 00 00 00 00 01",TimeSpan.FromSeconds(8));
        var ballistic=await friend.WaitForRpcAsync(ModPattern(692,1010));Check.Equal(19,BitConverter.ToInt32(ballistic.Bytes,13),"Deflect uses caster20");
        await friend.WaitForRpcAsync(ModPattern(692,1011));
        var bulletFx = await friend.WaitForRpcAsync(ModPattern(380,380));
        Check.Equal(0x28000824, BitConverter.ToInt32(bulletFx.Bytes,13), "Deflect Bullets delayed FX");
        Check.Equal(0.2f, BitConverter.ToSingle(bulletFx.Bytes,18), "Deflect Bullets FX delay");
        await friend.WaitForRpcAsync(ModPattern(380,380,true));
        Check.That(friend.Received(ModPattern(692,564)).Count>0,"Concentration component accompanies the pulse");
        await friend.WaitUntilAsync(()=>friend.Received(ModPattern(692,1010)).Count>=2,"Deflect parent repeats its child",TimeSpan.FromSeconds(8));
        Cast(p,380);await p.WaitForRpcAsync("80 bd 05 7c 01 00 00 00 00 00 00");
        Cast(p,378);await friend.WaitForRpcAsync(ModPattern(693,183),TimeSpan.FromSeconds(8));
        var virusFx = await friend.WaitForRpcAsync(ModPattern(378,378));
        Check.Equal(0x28000820, BitConverter.ToInt32(virusFx.Bytes,13), "Deflect Virus delayed FX");
        Check.Equal(0.2f, BitConverter.ToSingle(virusFx.Bytes,18), "Deflect Virus FX delay");
        await friend.WaitForRpcAsync(ModPattern(378,378,true));
        friend.SendChat("?pvpflag on");await friend.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        var victim=await enemy.WaitForViewNamedAsync(friend.Handle);
        Cast(enemy,481,victim.ViewId);await friend.WaitForRpcAsync(ModPattern(481,481),TimeSpan.FromSeconds(8));
        await friend.WaitForRpcAsync(ModPattern(481,481,true),TimeSpan.FromSeconds(8)); // fixture roll0 < term sweep4
        Cast(p,378);await p.WaitForRpcAsync("80 bd 05 7a 01 00 00 00 00 00 00");
        await PrepareLineUp(p);
        int concealOff=p.Received(ModPattern(1137,881,true)).Count;
        Cast(p,506);await Task.Delay(1000);
        Check.Equal(concealOff,p.Received(ModPattern(1137,881,true)).Count,"Line Up preserves concealment during its cast");
        await p.WaitForRpcAsync(ModPattern(506,524),TimeSpan.FromSeconds(8));
        Check.That(p.Received(ModPattern(506,171)).Count>0,"Both Line Up modifiers arrived");
        await SetProcHealth(p,1000,1000); // healing/full health cannot cancel the damage-sensitive modifier
        Check.Equal(0,p.Received(ModPattern(506,171,true)).Count,"Healing preserves Line Up damage component");
        p.SendChat("?health 400");await p.WaitForRpcAsync(ModPattern(506,171,true));await p.WaitForRpcAsync(ModPattern(506,524,true));
        Check.Equal(0,p.World.Problems.Count+friend.World.Problems.Count+enemy.World.Problems.Count,"Deflect parents and concealed Line Up protocol");
    }

    private static async Task DeflectBulletsChildRadius(ScenarioContext context)
    {
        const ushort parent = 380, child = 692, level = 22;
        const int x = 17000, y = 495, z = 6400;
        const int edgeZ = z - 330, outsideZ = z + 331;
        var roots = new ushort[] { 2, 17, CombatStyles.HackerStyle, parent };
        var profile = roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
            .Distinct().Select(id => (id, level)).ToArray();
        Check.That(profile.Sum(row => AbilityMemory.Cost(row.id)) <= AbilityMemory.ForLevel(level),
            "Deflect Bullets fits its first eligible profile");
        Check.Equal(6d, AbilityCatalog.Find(parent).Number("Ability/RadiusOfEffect"), "Parent radius metadata");
        Check.Equal(3d, AbilityCatalog.Find(child).Number("Ability/RadiusOfEffect"), "Invoked child radius");
        SectorProperties.Load(Path.Combine(context.RepositoryRoot, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot, "data", "navmesh"));
        foreach (var (standX, standZ) in new[] { (x, z), (x, edgeZ), (x, outsideZ), (x + 150, z) })
            Check.That(NavMesh.Locate(1, standX, y, standZ, out var stand) && !stand.indoors && stand.floorY + 95 == y,
                $"Verified outdoor NAV ({standX},{y},{standZ})");

        var caster = await context.AddPlayerAsync(new CharacterSeed("DeflectRadiusOwner")
        {
            Level = level, X = x, Y = y, Z = z, Rotation = 0, Organization = 1, OwnedLoadedAbilities = profile
        });
        var edge = await context.AddPlayerAsync(new CharacterSeed("DeflectRadiusEdge")
        {
            Level = level, X = x, Y = y, Z = edgeZ, Organization = 1
        });
        var outside = await context.AddPlayerAsync(new CharacterSeed("DeflectRadiusOutside")
        {
            Level = level, X = x, Y = y, Z = outsideZ, Organization = 1
        });
        var outsider = await context.AddPlayerAsync(new CharacterSeed("DeflectRadiusOutsider")
        {
            Level = level, X = x + 150, Y = y, Z = z, Organization = 1
        });
        var players = new[] { caster, edge, outside, outsider };
        foreach (var player in players) await player.EnterWorldAsync();
        SelectStyle(caster, CombatStyles.HackerStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        var radius = await ObserveAsync(caster, ModPattern(74, 127));
        Check.Equal(10, BitConverter.ToInt32(radius.Bytes, 13),
            "Required rank-22 Resist Damage makes the three-metre radius 3.30 m");
        foreach (var (player, expectedX, expectedZ) in new[] { (edge, x, edgeZ), (outside, x, outsideZ), (outsider, x + 150, z) })
        {
            var seen = await caster.WaitForViewNamedAsync(player.Handle);
            Check.That(EngageFoesFixture.Position(seen).SequenceEqual(new double[] { expectedX, y, expectedZ }),
                $"{player.Handle} stands at its measured radius control");
        }
        foreach (var teammate in new[] { edge, outside })
        {
            caster.SendRpc(GroupInvite(Team, teammate.Handle, ""));
            await teammate.WaitForRpcAsync("80 8f 0c 00");
            teammate.SendRpc(InviteAnswer(Team, 0, caster.Handle));
            await teammate.WaitForRpcAsync("80 8e");
            await caster.WaitForRpcAsync($"70 03 {Le(teammate.CharId)} 09 00");
        }

        Cast(caster, child);
        await caster.WaitForTextAsync("Use the parent ability, not its internal effect.");
        Check.Equal(0, caster.Received(ModPattern(child, 1010)).Count, "The child has no direct activation path");

        // The legacy headless sender drives the ordinary server executor. PR320 independently captures the stock parent/child flow.
        Cast(caster, parent);
        await caster.WaitForRpcAsync("80 bd 05 7c 01 00 00 00 00 00 01");
        var accepted = new[] { caster, edge };
        var modifiers = new (ushort Stat, int Value)[] { (1010, 21), (1011, 10), (564, 10) };
        var active = new Dictionary<(HeadlessPlayer Player, ushort Stat), RpcRecord>();
        var ownFx = BytePattern.Parse(WireFormat.ToHex(ServerPackets.BuildPlayerAbilityEffect(0x28000824, 0, true)[..^1]) + " ??");
        foreach (var player in accepted)
        {
            foreach (var (stat, value) in modifiers)
            {
                var add = await ObserveAsync(player, ModPattern(child, stat));
                Check.Equal(value, BitConverter.ToInt32(add.Bytes, 13), $"{player.Handle} stat {stat}");
                Check.Equal(5f, BitConverter.ToSingle(add.Bytes, 18), $"{player.Handle} five-second child");
                active[(player, stat)] = add;
            }
            var fx = await ObserveAsync(player, ModPattern(parent, parent));
            Check.Equal(0x28000824u, BitConverter.ToUInt32(fx.Bytes, 13), $"{player.Handle} child FX");
            Check.Equal(0.2f, BitConverter.ToSingle(fx.Bytes, 18), $"{player.Handle} linked FX delay");
            var fxOff = await ObserveAsync(player, ModPattern(parent, parent, off: true));
            Check.Equal(BitConverter.ToUInt16(fx.Bytes, 4), BitConverter.ToUInt16(fxOff.Bytes, 4), "Linked FX removes its own record");
            await player.WaitUntilAsync(() => player.World.ViewSnapshot().Any(ownFx.Occurs),
                $"{player.Handle} receives the actual linked effect on the own view", HeadlessPlayer.DefaultTimeout);
        }
        Cast(caster, parent);
        await caster.WaitForRpcAsync("80 bd 05 7c 01 00 00 00 00 00 00");
        foreach (var player in accepted)
        foreach (var (stat, _) in modifiers)
        {
            var removal = await ObserveAsync(player, ModPattern(child, stat, off: true));
            Check.Equal(BitConverter.ToUInt16(active[(player, stat)].Bytes, 4), BitConverter.ToUInt16(removal.Bytes, 4),
                $"{player.Handle} expires the active {stat} record");
            Check.Equal(1, player.Received(ModPattern(child, stat)).Count, $"{player.Handle} gets one pulse after the test starts");
        }
        foreach (var player in new[] { outside, outsider })
        {
            foreach (var (stat, _) in modifiers)
                Check.Equal(0, player.Received(ModPattern(child, stat)).Count, $"{player.Handle} is excluded");
            Check.Equal(0, player.Received(ModPattern(parent, parent)).Count, $"{player.Handle} receives no linked FX");
        }
        Check.Equal(0, players.Sum(player => player.World.Problems.Count), "Deflect Bullets child framing");

        static async Task<RpcRecord> ObserveAsync(HeadlessPlayer player, string pattern)
        {
            await player.WaitUntilAsync(() => player.Received(pattern).Count != 0, pattern, HeadlessPlayer.DefaultTimeout);
            return player.Received(pattern).Single();
        }
    }

    private static async Task HealthLineupTimings(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(LineUpActor("LineupTimer"));
        await p.EnterWorldAsync();
        SelectStyle(p, CombatStyles.Rifles);
        await p.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 93 00 00 00");
        // This scenario exercises normal server admission using the existing headless ability sender.
        byte[] casterState = AbilityRefusals.Encode(new AbilityRefusals.Record(506, AbilityRefusals.CasterStateRequired,
            states: Stealth.ConcealmentFlag));
        Cast(p,506);
        await p.WaitForRpcAsync(WireFormat.ToHex(casterState));
        string cost = $"[ABILITY-COST] char={p.CharId} ability=506 paid=30";
        Check.That(!context.Server.DebugLog().Contains(cost), "An exposed player does not pay for Line Up");
        await EnterLineUpConcealment(p);
        Cast(p,506);
        var bar = await p.WaitForRpcAsync("80 ac fa 01");
        Check.Equal(4f, BitConverter.ToSingle(bar.Bytes, bar.Bytes.Length - 4), "Line Up's normal cast lasts four seconds");
        var accuracy=await p.WaitForRpcAsync(ModPattern(506,524),TimeSpan.FromSeconds(9));
        Check.Equal(20f,BitConverter.ToSingle(accuracy.Bytes,18),"Accuracy lasts20s");
        Check.Equal(25, BitConverter.ToInt32(accuracy.Bytes,13), "Accuracy scales to +25 at level 20");
        var damage=p.Received(ModPattern(506,171)).Last();Check.Equal(12f,BitConverter.ToSingle(damage.Bytes,18),"Damage and its health callback last12s");
        Check.Equal(10, BitConverter.ToInt32(damage.Bytes,13), "Ballistic damage scales to +10 at level 20");
        await p.WaitForRpcAsync(WireFormat.ToHex(AbilityCooldowns.BuildTimer(506, AbilityCooldowns.OwnTimer, 15, true)));
        Check.That(context.Server.DebugLog().Contains(cost), "The admitted use pays 30 IS");
        Check.Equal(0, p.Received(ModPattern(506,710)).Count, "The inline reapplication identifier is never published as a stat");
        await p.WaitForRpcAsync(ModPattern(506,171,true),TimeSpan.FromSeconds(15));
        Check.Equal(0,p.Received(ModPattern(506,524,true)).Count,"Accuracy survives the12s component expiry");
        p.SendChat("?health 400");await p.WaitForRpcAsync(ModPattern(1137,881,true));
        await Task.Delay(600);Check.Equal(0,p.Received(ModPattern(506,524,true)).Count,"Damage after12s does not cancel the remaining accuracy");
        await p.WaitForRpcAsync(ModPattern(506,524,true),TimeSpan.FromSeconds(10));
        Check.Equal(0,p.World.Problems.Count,"Line Up split duration protocol");
    }

    private static CharacterSeed LineUpActor(string handle)
    {
        const ushort level = 20, lineUp = AbilityHealthCallbacks.LineUpAbility;
        var roots = new ushort[] { 2, 17, CombatStyles.Rifles, lineUp };
        var profile = roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
            .Distinct().Select(id => (id, level)).ToArray();
        return new CharacterSeed(handle)
        {
            Level = level, Health = 500, MaxHealth = 1000, InnerStrength = 1000, MaxInnerStrength = 1000,
            Items = new[] { ((int)CombatFixtureWeapons.HandSlot, CombatFixtureWeapons.Rifle, 1) },
            OwnedLoadedAbilities = profile
        };
    }

    private static async Task PrepareLineUp(HeadlessPlayer actor)
    {
        SelectStyle(actor, CombatStyles.Rifles);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 93 00 00 00");
        await EnterLineUpConcealment(actor);
    }

    private static async Task EnterLineUpConcealment(HeadlessPlayer actor)
    {
        Cast(actor, Stealth.ConcealmentGrantAbility);
        await actor.WaitForRpcAsync(ModPattern(1137, 881), TimeSpan.FromSeconds(8));
    }
}
