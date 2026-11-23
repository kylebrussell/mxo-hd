using hds;
using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static IReadOnlyDictionary<string, string> CoderEnrage588Environment =>
        new Dictionary<string, string>(PvpOpponentsFixture.Environment)
        {
            ["MXO_DEV_COMMANDS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        };

    private static async Task CoderEnrageInterlockRetarget(ScenarioContext context)
    {
        const ushort enrage = 588, level = 25;
        const int y = 495, z = 6400, casterX = 16800, subjectX = 17000, originalX = 17150, replacementX = 17250;
        SectorProperties.Load(Path.Combine(context.RepositoryRoot, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot, "data", "navmesh"));
        foreach (int x in new[] { casterX, subjectX, originalX, replacementX })
            Check.That(NavMesh.Locate(1, x, y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - y) < 1 && NavMesh.HasSightPath(1, x, y, z, subjectX, y, z),
                $"Visible outdoor NAV stand ({x},{y},{z})");

        ushort[] loaded = { 2, 17, CombatStyles.CoderStyle, enrage };
        var profile = loaded.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
            .Distinct().Order().Select(id => (id, level)).ToArray();
        Check.That(profile.Sum(row => AbilityMemory.Cost(row.id)) <= AbilityMemory.ForLevel(level),
            "The complete ranked caster profile fits in memory");

        CharacterSeed Seed(string name, int x, int org) => new(name)
        {
            X = x, Y = y, Z = z, Level = level, Experience = checked((int)PlayerProgression.ExperienceForLevel(level)),
            Health = 5000, MaxHealth = 5000, InnerStrength = 250, MaxInnerStrength = 250, Organization = org,
            OwnedLoadedAbilities = new (ushort, ushort)[] { (2, 1), (17, 1) }
        };

        var caster = await context.AddPlayerAsync(Seed("EnrageRetargetCaster", casterX, 1) with { OwnedLoadedAbilities = profile });
        var subject = await context.AddPlayerAsync(Seed("EnrageRetargetSubject", subjectX, 2) with { InnerStrength = 100 });
        var original = await context.AddPlayerAsync(Seed("EnrageRetargetOld", originalX, 1));
        var replacement = await context.AddPlayerAsync(Seed("EnrageRetargetNew", replacementX, 1));
        foreach (var player in new[] { caster, subject, original, replacement })
            await PvpOpponentsFixture.EnterAsync(player);

        static async Task<uint> HandleAsync(HeadlessPlayer viewer, HeadlessPlayer target)
        {
            var view = await viewer.WaitForViewNamedAsync(target.Handle);
            return DuelPackets.Handle(view.ViewId, view.SpawnId);
        }

        static string Hex(uint handle) => WireFormat.ToHex(BitConverter.GetBytes(handle));
        var oldHandle = Hex(await HandleAsync(subject, original));
        var newHandle = Hex(await HandleAsync(subject, replacement));
        var replacementSubject = Hex(await HandleAsync(replacement, subject));
        await HandleAsync(original, replacement);
        await HandleAsync(replacement, original);
        var castHandle = await HandleAsync(caster, subject);

        subject.SendRpc($"40 {oldHandle} 01 6b");
        await subject.WaitForRpcAsync($"46 {oldHandle} 01");
        ushort neutralIs = RefusalOwnValue(subject, "InnerStrengthAvailable", "02 80 80 80 10");
        int neutralViews = subject.World.ViewSnapshot().Count;
        await SelectTacticAsync(3, "Block");
        await subject.WaitUntilAsync(() => HasBlockAward(neutralViews, neutralIs),
            "Ordinary Block awards ten IS before Enrage", TimeSpan.FromSeconds(8));
        await SelectTacticAsync(0, "Speed");

        caster.SendRpc("3e 6b f0 03 00 00 00 00");
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        // Separate server probe. Stock Close Combat and target selection retain their captured shapes.
        int castLog = context.Server.DebugLog().Length;
        caster.SendRpc(new AbilityUseRecord(enrage, castHandle, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        var cast = await caster.WaitForRpcAsync("80 ac 4c 02");
        Check.Equal(2f, BitConverter.ToSingle(cast.Bytes.AsSpan(^4)), "Enrage 1.0 uses the ordinary two-second player cast");
        await caster.WaitForRpcAsync("80 bd 01 4c 02 04 00 00 e0 41 01");
        Check.That(context.Server.DebugLog()[castLog..].Contains(
            $"[ABILITY-COST] char={caster.CharId} ability={enrage} paid=27 IS=250->223", StringComparison.Ordinal),
            "The stock cost is paid before the affected player's control");
        string modifier = $"[MODIFIER] char={subject.CharId} source={enrage} stat=93";
        var add = await subject.WaitForRpcAsync(
            frame => DeflectCodeFixture.State(frame.Bytes, enrage, TempMods.EnrageAbility, false, out float seconds) && seconds == 12,
            "The already-interlocked player receives the ordinary twelve-second Enraged record");

        replacement.SendRpc($"40 {replacementSubject} 01 6b");
        await replacement.WaitForRpcAsync($"46 {replacementSubject} 01");
        ushort enragedIs = RefusalOwnValue(subject, "InnerStrengthAvailable", "02 80 80 80 10");
        int enragedViews = subject.World.ViewSnapshot().Count;
        int blockRoundLog = context.Server.DebugLog().Length;
        await SelectTacticAsync(3, "Block");
        subject.SendRpc($"81 51 {newHandle}");
        subject.SendChat("?combatdepth shieldstatus");
        await subject.WaitForTextAsync($"target={replacement.Handle}");

        int before = subject.World.RpcSnapshot().Count;
        string refused = $"[PVP] Target change refused for {subject.Handle}: Enraged";
        byte[] clearOld = WireFormat.FromHex($"46 {oldHandle} 00");
        byte[] openNew = WireFormat.FromHex($"46 {newHandle} 01");
        byte[] echo = WireFormat.FromHex($"40 {newHandle} 00 6b");
        subject.SendRpc($"40 {newHandle} 01 6b");
        await subject.WaitUntilAsync(() => context.Server.DebugLog().Contains(refused),
            "Enrage refuses only the actual interlock retarget", TimeSpan.FromSeconds(5));
        await subject.WaitUntilAsync(() => context.Server.DebugLog()[blockRoundLog..].Split('\n').Any(line =>
            line.StartsWith("[PVP] Round ", StringComparison.Ordinal) && !line.Contains("(opening)", StringComparison.Ordinal) &&
            line.Contains($"{subject.Handle} Block (", StringComparison.Ordinal)),
            "The player can select and play a real Block round under Enrage 1.0", TimeSpan.FromSeconds(6));
        subject.SendChat("?combatdepth shieldstatus");
        await subject.WaitForTextAsync($"target={replacement.Handle}");
        Check.That(!HasBlockAward(enragedViews, enragedIs), "Enrage 1.0 suppresses the ordinary ten-point award during the Block round");

        ushort resumedIs = RefusalOwnValue(subject, "InnerStrengthAvailable", "02 80 80 80 10");
        int resumedViews = subject.World.ViewSnapshot().Count;
        byte[] removed = add.Bytes.ToArray();
        removed[17] = 1;
        await subject.WaitForRpcAsync(frame => frame.Bytes.AsSpan().SequenceEqual(removed),
            "Enrage 1.0 expires and removes the same twelve-second player record", TimeSpan.FromSeconds(16));
        await subject.WaitForRpcAsync(frame =>
            frame.Bytes.AsSpan().SequenceEqual(AbilityMessages.StateEnd(TempMods.EnrageAbility, subject.Handle)),
            "The player receives the same Enraged end notification");
        await subject.WaitUntilAsync(() => context.Server.DebugLog().Contains(modifier + " remove"),
            "The original Enrage naturally expires", TimeSpan.FromSeconds(16));
        Check.That(!subject.World.RpcSnapshot().Skip(before).Any(frame => frame.Bytes.AsSpan().SequenceEqual(clearOld) ||
            frame.Bytes.AsSpan().SequenceEqual(openNew) || frame.Bytes.AsSpan().SequenceEqual(echo)),
            "The held target stays active without a false echo or an opponent swap");

        subject.SkipReceived();
        subject.SendRpc($"40 {newHandle} 01 6b");
        await subject.WaitForRpcAsync($"46 {oldHandle} 00");
        await subject.WaitForRpcAsync($"40 {newHandle} 00 6b");
        await subject.WaitForRpcAsync($"46 {newHandle} 01");
        await subject.WaitUntilAsync(() => HasBlockAward(resumedViews, resumedIs),
            "The ten-point Block award resumes in the live fight after expiry", TimeSpan.FromSeconds(8));
        Check.Equal(0, caster.World.Problems.Count + subject.World.Problems.Count +
            original.World.Problems.Count + replacement.World.Problems.Count, "Retarget and join packets parse cleanly");
        Console.WriteLine("ENRAGE 588 PASS: normal Block selection, +10/0/+10 Block award, retarget lock and release, and matched state removal.");

        async Task SelectTacticAsync(byte tactic, string name)
        {
            int cursor = context.Server.DebugLog().Length;
            subject.SendRpc($"42 {tactic:x2} 00 00 00 00 00");
            await subject.WaitUntilAsync(() => context.Server.DebugLog()[cursor..]
                .Contains($"[COMBAT] Tactic changed to {name} (Normal) for {subject.Handle}", StringComparison.Ordinal),
                $"The ordinary {name} input reaches the live fight", HeadlessPlayer.DefaultTimeout);
        }

        bool HasBlockAward(int cursor, ushort previous)
        {
            foreach (ushort current in ViewUpdateValues(subject, 2, "02 80 80 80 10", cursor))
            {
                // Background PvP regeneration stays enabled. The Block round produces
                // its own ten-point update, distinct from the smaller regeneration ticks.
                if (current - previous == 10) return true;
                previous = current;
            }
            return false;
        }
    }

    private static async Task CoderEnrage588(ScenarioContext context)
    {
        const ushort enrage = 588, level = 25;
        ushort[] abilities = new ushort[] { 2, 17, CombatStyles.CoderStyle, enrage };
        var profile = abilities.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
            .Distinct().Order().Select(id => (id, level)).ToArray();
        Check.That(profile.Sum(row => AbilityMemory.Cost(row.id)) <= AbilityMemory.ForLevel(level),
            "Enrage and its ranked prerequisite chain fit memory at level 25");
        CharacterSeed Seed(string name, int x, int organization) => new(name)
        {
            X = x, Y = 495, Z = 6400, Level = level, Experience = checked((int)PlayerProgression.ExperienceForLevel(level)),
            Health = 5000, MaxHealth = 5000, InnerStrength = 250, MaxInnerStrength = 250, Organization = organization
        };
        var actor = await context.AddPlayerAsync(Seed("EnrageCoder", 17000, 1) with { OwnedLoadedAbilities = profile });
        var target = await context.AddPlayerAsync(Seed("EnrageOpponent", 17400, 2) with
        {
            OwnedLoadedAbilities = new (ushort, ushort)[] { (2, 1), (17, 1) }
        });
        var friend = await context.AddPlayerAsync(Seed("EnrageAlly", 16800, 1));
        var far = await context.AddPlayerAsync(Seed("EnrageFarOpponent", 18601, 2));
        foreach (var player in new[] { actor, target, friend, far })
            await PvpOpponentsFixture.EnterAsync(player);
        actor.SendRpc("3e 6b f0 03 00 00 00 00");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        var targetView = await actor.WaitForViewNamedAsync(target.Handle);
        var friendView = await actor.WaitForViewNamedAsync(friend.Handle);
        var farView = await actor.WaitForViewNamedAsync(far.Handle);
        string Added(HeadlessPlayer player) => $"[MODIFIER] char={player.CharId} source={enrage} stat=93 add";
        byte[] Request(uint selection) =>
            new AbilityUseRecord(enrage, selection, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();

        // The production request is exercised with generated inputs here. Native QA
        // separately records 588's actual stock activation envelope.
        int initial = RefusalOwnValue(actor, "InnerStrengthAvailable", "02 80 80 80 10");
        int cursor = context.Server.DebugLog().Length;
        await Refuse(0, AbilityRefusals.InvalidTarget);
        await Refuse(DuelPackets.Handle(friendView.ViewId, friendView.SpawnId), AbilityRefusals.InvalidTarget);
        await Refuse(DuelPackets.Handle(farView.ViewId, farView.SpawnId), AbilityRefusals.TargetOutOfRange);
        await Refuse(DuelPackets.Handle(targetView.ViewId, (ushort)(targetView.SpawnId + 1)), AbilityRefusals.InvalidTarget);
        Check.Equal(initial, RefusalOwnValue(actor, "InnerStrengthAvailable", "02 80 80 80 10"),
            "Empty, friendly, far and wrong-spawn requests preserve IS");
        Check.That(!context.Server.DebugLog()[cursor..].Contains($"[ABILITY-COST] char={actor.CharId} ability={enrage}"),
            "Refused target selections never reach cost");

        actor.SkipReceived();
        int firstView = actor.World.ViewSnapshot().Count;
        actor.SendRpc(Request(DuelPackets.Handle(targetView.ViewId, targetView.SpawnId)));
        var cast = await actor.WaitForRpcAsync("80 ac 4c 02");
        Check.Equal(2f, BitConverter.ToSingle(cast.Bytes.AsSpan(^4)), "Two-second normal cast record");
        await actor.WaitForRpcAsync("80 bd 01 4c 02 04 00 00 e0 41 01");
        string add = Added(target) + " value=0 duration=12s";
        string remove = $"[MODIFIER] char={target.CharId} source={enrage} stat=93 remove";
        await actor.WaitUntilAsync(() => context.Server.DebugLog()[cursor..].Contains(add),
            "One selected living opponent receives the twelve-second Enrage", TimeSpan.FromSeconds(8));
        string after = context.Server.DebugLog()[cursor..];
        Check.That(ViewUpdateValues(actor, 2, "02 80 80 80 10", firstView).Contains((ushort)(initial - 27)) &&
            after.Contains($"[ABILITY-COST] char={actor.CharId} ability={enrage} paid=27 IS={initial}->{initial - 27}"),
            "Exactly 27 IS is paid before any ordinary regeneration");
        Check.That(!after.Contains(Added(friend)) && !after.Contains(Added(far)),
            "The selected Enrage does not spread");
        await actor.WaitUntilAsync(() => context.Server.DebugLog()[cursor..].Contains(remove),
            "Enrage is removed by expiry", TimeSpan.FromSeconds(16));
        Check.Equal((ushort)5000, RefusalOwnValue(target, "Health", "02 80 80 80 40"),
            "The state-only cast preserves the selected opponent's HP");
        Check.Equal(0, actor.World.Problems.Count + target.World.Problems.Count + friend.World.Problems.Count + far.World.Problems.Count,
            "Normal target and modifier handling retains packet framing");
        Console.WriteLine("ENRAGE 588 SERVER PASS: empty, ally, 16.01 m and stale-spawn no-debit refusals, 27 IS, 28 s reuse, and unique 12 s selected-player state.");

        async Task Refuse(uint selection, uint reason)
        {
            actor.SkipReceived();
            actor.SendRpc(Request(selection));
            byte[] expected = AbilityRefusals.Encode(new AbilityRefusals.Record(enrage, reason));
            await actor.WaitForRpcAsync(record => record.Bytes.SequenceEqual(expected), $"Enrage refusal reason {reason}");
        }
    }
}
