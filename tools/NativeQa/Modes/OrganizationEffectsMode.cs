using System.Buffers.Binary;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

/// <summary>
/// Organization E toggles 1169/1173/1177 and their internal health-change effects 1180 System Control, 1182 Retribution and
/// 1181 Champion of Zion. The parents' stored prerequisite chain ends in level-99 Organization B abilities, so this disposable
/// fixture loads only the three toggles. Fixed callback rolls make the stored 5% chance deterministic.
/// </summary>
public sealed class OrganizationEffectsMode : NativeQaMode
{
    private const ushort SystemControl = AbilityHealthCallbacks.SystemControl, Retribution = AbilityHealthCallbacks.Retribution,
        Zion = AbilityHealthCallbacks.ChampionOfZion, Family = 1178;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, DummyZ = 6700;
    private static readonly ushort[] Parents = { SystemControl, Retribution, Zion };
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17).Concat(Parents).Distinct().Order().ToArray();
    private static readonly (string Handle, int Id, int X, int Z, int Org, string Decision)[] Stands =
    {
        ("QAEnemy", 6114, X - 300, Z, 2, "included"),          // 3.00 m west, hostile
        ("QAOther", 6113, X + 501, Z, 2, "radius"),            // 5.01 m east, hostile outside radius 5
        ("QAFriend", 6111, X + 150, Z, 1, "pvp-NotOpponent"),  // 1.50 m east, friendly
        ("QAFar", 6112, X - 600, Z, 1, "radius")               // 6.00 m west, friendly outside radius 5
    };
    // Stock self-cast request template (PR382 7bc903fb3dd5:5886-5887, target cleared); ability ID substituted.
    private const string SelfCastTemplate = "80b905000000000000000000000000000000000000000000000000585cc60001000000086b0000000000";

    public override string Name => "ability-parity-organization-effects";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0," +
            "alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
    }.Concat(Stands.Select(s =>
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={s.X},y={Y},z={s.Z},alignment={s.Org}," +
        $"healthC={(s.Org == 1 ? 4000 : 5000)},healthM=5000,innerStrC=500,innerStrM=1000 WHERE charId={s.Id}")).ToArray(),
        DevCommands: true, FixedCallbackRolls: true, loadAbilityAncestors: false, testMobFixture: "wounding-throw.csv",
        hotbarAbilities: Parents,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1180,1181,1182",
            ["NativeCloseableRows"] = 3,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}. CSR starts at verified outdoor HOME ({X},{Y},{Z}) facing the 30,000 HP Melee Dummy 3 m north. QAEnemy (hostile) is 3.00 m west, QAOther (hostile) 5.01 m east, QAFriend 1.5 m east and QAFar (friendly) 6.00 m west. Page 2: 1=System Control (1169), 2=Retribution (1173), 3=Champion of Zion (1177). Disposable fixture: the parents' stored prerequisite chain ends in level-99 Organization B abilities, so only the three toggles are loaded. Callback rolls are fixed to 0 so the stored 5% chance always passes. Stay put with selection clear; no initial tp.",
            "Step 1 (System Control): press slot 1. Expect 25 IS, toggle on, 25 IS every 5 s, no cast. Type ?health 4000. Expect [HEALTH-EFFECT] 1169 roll=0 chance=5, '1180 controls QAEnemy' and '1180 controls NPC Melee Dummy', a 5 s Stun on both with 10 s control immunity, and stun presentation on the dummy. [AREA-QA] should exclude QAOther (radius) and QAFriend/QAFar (pvp-NotOpponent / radius). Nothing for QAFriend, QAFar or CSR.",
            "Step 2 (Retribution): wait 11 s for the immunity to end, then press slot 2 (25 IS). Type ?health 3800. Expect '1182 hits QAEnemy for 150' and '1182 hits NPC Melee Dummy for 150' with matching HP drops (dummy 30000 -> 29850), the 1180 stun again, and no damage to QAOther or the friends.",
            "Step 3 (Champion of Zion): press slot 3 (25 IS). Type ?health 3000. Expect '1181 heals CSR_Tester a -> a+250' and '1181 heals QAFriend b -> b+250' (QAFriend starts at 4000/5000; ordinary regeneration can raise the starting values), each with '1181 restores IS of ... c -> c+25', the 1181 FX on both, no heal for QAFar or QAEnemy, and 1182 again. QAEnemy is still inside its 1180 immunity window, so no second stun lands on it.",
            "Step 4 (toggle-off reuse): press slots 1, 2 and 3 to switch each off. Expect each toggle off with its own 240 s timer and the shared family timer 1178 for 240 s; pressing any slot again is refused while the timer runs. Type ?health 2800: no [HEALTH-EFFECT] line follows."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Organization effects require NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        foreach (var (x, z) in Stands.Select(s => (s.X, s.Z)).Append((X, Z)).Append((X, DummyZ)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Organization stand ({x},{Y},{z}) has outdoor NAV");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "HOME has clear sight to the Melee Dummy");
        foreach (var s in Stands)
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, s.X, Y, s.Z), $"HOME has clear sight to {s.Handle}");
        foreach (ushort effect in new ushort[] { AbilityHealthCallbacks.SystemControlEffect, AbilityHealthCallbacks.RetributionEffect,
                     AbilityHealthCallbacks.ChampionOfZionEffect })
            Check.Equal(5d, AbilityCatalog.Find(effect).Number("Ability/RadiusOfEffect"), $"Stored {effect} radius");
        Check.Equal(150, AbilityHealthCallbacks.Sample(AbilityHealthCallbacks.RetributionEffect, 0), "Stored Retribution damage");
        Check.Equal(250, AbilityHealthCallbacks.Sample(AbilityHealthCallbacks.ChampionOfZionEffect, 0), "Stored Champion of Zion heal");

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        foreach (var s in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, s.Handle, s.Handle, stockCharacterLoad: true);
            context.Helpers.Add(s.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet();
        Check.That(loaded.SetEquals(Loadout), "Exactly the three Organization toggles over the base loadout are loaded");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        CombatFixtureLoadout.AssertDummy(actor, await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName));
        foreach (var s in Stands) await actor.WaitForViewNamedAsync(s.Handle);

        foreach (ushort parent in Parents)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(SelfRequest(parent));
            await ToggleAsync(cursor, parent, true);
        }

        int log = server.DebugLog().Length;
        actor.SendChat("?health 4000");
        string[] expected =
        {
            "[HEALTH-EFFECT] 1180 controls QAEnemy", "[HEALTH-EFFECT] 1180 controls NPC Melee Dummy",
            "[HEALTH-EFFECT] 1182 hits QAEnemy for 150", "[HEALTH-EFFECT] 1182 hits NPC Melee Dummy for 150",
            "[HEALTH-EFFECT] 1181 heals CSR_Tester ", "[HEALTH-EFFECT] 1181 heals QAFriend ",
            "[HEALTH-EFFECT] 1181 restores IS of CSR_Tester ", "[HEALTH-EFFECT] 1181 restores IS of QAFriend "
        };
        foreach (string line in expected)
            await actor.WaitUntilAsync(() => server.DebugLog()[log..].Contains(line, StringComparison.Ordinal), line, HeadlessPlayer.DefaultTimeout);
        // Ordinary regeneration runs between fixture steps, so check the applied deltas rather than absolute values.
        foreach (var (prefix, delta) in new[] { ("1181 heals CSR_Tester ", 250), ("1181 heals QAFriend ", 250),
                     ("1181 restores IS of CSR_Tester ", 25), ("1181 restores IS of QAFriend ", 25) })
        {
            string line = server.DebugLog()[log..].Split('\n').First(l => l.Contains(prefix, StringComparison.Ordinal));
            var values = line[(line.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length)..].Split(" -> ");
            Check.Equal(delta, int.Parse(values[1].Trim()) - int.Parse(values[0].Trim()), $"Champion of Zion delta for {prefix.Trim()}");
        }
        string trace = server.DebugLog()[log..];
        var effects = trace.Split('\n').Where(l => l.Contains("[HEALTH-EFFECT] 118", StringComparison.Ordinal)).ToArray();
        foreach (string handle in new[] { "QAOther", "QAFar" })
            Check.That(effects.All(l => !l.Contains(handle, StringComparison.Ordinal)), $"{handle} receives no Organization effect");
        Check.That(!trace.Contains("1180 controls QAFriend", StringComparison.Ordinal) && !trace.Contains("1182 hits QAFriend", StringComparison.Ordinal) &&
            !trace.Contains("1181 heals QAEnemy", StringComparison.Ordinal), "Friend and hostile selectors stay separate");
        foreach (var s in Stands.Where(s => s.Org == 2))
            Check.That(trace.Split('\n').Any(l => l.Contains($"candidate={s.Handle} ", StringComparison.Ordinal) && l.TrimEnd().EndsWith("reason=" + s.Decision)),
                $"Expected area decision for {s.Handle}");

        foreach (ushort parent in Parents)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(SelfRequest(parent));
            await ToggleAsync(cursor, parent, false);
            await EvidenceRetestWire.TimerAsync(actor, cursor, parent, AbilityCooldowns.OwnTimer, 240);
            await EvidenceRetestWire.TimerAsync(actor, cursor, Family, AbilityCooldowns.GroupTimer, 240);
            await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + 0.15));
        }
        int afterStop = server.DebugLog().Length;
        int views = actor.World.ViewSnapshot().Count;
        actor.SendChat("?health 3800");
        var ownerHealth = BytePattern.Parse($"02 00 02 80 80 80 40 {EvidenceRetestWire.Le(3800)}");
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views).Any(ownerHealth.Occurs),
            "The owner health change is delivered after toggle-off", HeadlessPlayer.DefaultTimeout);
        Check.That(!server.DebugLog()[afterStop..].Contains("[HEALTH-EFFECT] 11", StringComparison.Ordinal),
            "Toggle-off removes all three health callbacks");
        Check.Equal(0, actor.World.Problems.Count, "Organization toggle framing");
        Console.WriteLine("ORGANIZATION EFFECTS PASS 1180 1181 1182");
        return Array.Empty<byte>();

        Task ToggleAsync(int cursor, ushort id, bool on) => StockAbilityProfile.ObserveAsync(actor, cursor, frame =>
            frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(id, on)), $"Organization toggle {id} on={on}");
    }

    private static byte[] SelfRequest(ushort ability)
    {
        byte[] request = Convert.FromHexString(SelfCastTemplate);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), ability);
        return request;
    }
}
