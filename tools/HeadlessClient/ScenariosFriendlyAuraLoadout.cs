using System.Diagnostics;
using hds;
using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    private const string CombatAuraStockUse = "80b92a00000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string DeflectVirusStockUse = "80 b9 7a 01 0d 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 15 00 00 e0 d1 2c 08 6b 00 00 00 00 00";
    private const string CombatAuraTwoStockUse = "80 b9 e5 01 0d 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 20 00 00 00 78 f2 12 00 08 6b 00 00 00 00 00";

    private static (ushort Ability, ushort Level)[] FriendlyAuraProfile(ushort level, params ushort[] roots)
    {
        var known = roots.ToHashSet();
        var pending = new Queue<ushort>(roots);
        while (pending.TryDequeue(out ushort id))
        {
            var discipline = AbilityCatalog.IdOf((uint)AbilityCatalog.Find(id).Number("Ability/DisciplineBelongsTo"));
            foreach (ushort required in AbilityPrerequisites.Current.Closure(id).Keys.Append(discipline))
                if (required != 0 && known.Add(required)) pending.Enqueue(required);
        }
        return known.Order().Select(id => (id, level)).ToArray();
    }

    private static async Task FriendlyAuraLoadout(ScenarioContext context)
    {
        const ushort level = 50;
        var cases = new (ushort Parent, ushort Child, ushort Stat, string Request, bool Timed)[]
        {
            (42, 695, 522, CombatAuraStockUse, false),
            (378, 693, 183, DeflectVirusStockUse, true),
            // There is no retained complete stock 380 request. Use the legacy sender for server coverage only.
            (380, 692, 1010, "80 b9 7c 01 02 00", true),
            (485, 696, 171, CombatAuraTwoStockUse, false)
        };
        var players = new List<(HeadlessPlayer Player, ushort Slot)>();
        for (int index = 0; index < cases.Length; index++)
        {
            ushort parent = cases[index].Parent;
            var profile = FriendlyAuraProfile(level, 2, 17, parent);
            Check.That(profile.Sum(row => AbilityMemory.Cost(row.Ability)) <= AbilityMemory.ForLevel(level),
                $"The owned profile for {parent} fits memory");
            var player = await context.AddPlayerAsync(new CharacterSeed($"AuraArchive{parent}")
            {
                Level = level, Experience = (int)PlayerProgression.ExperienceForLevel(level), Organization = 1,
                X = 17000, Y = 495, Z = 6400 + index * 20, InnerStrength = 1000, MaxInnerStrength = 1000,
                OwnedLoadedAbilities = profile
            });
            ushort slot = (ushort)(400 + Array.FindIndex(profile, row => row.Ability == parent));
            await player.EnterWorldAsync();
            players.Add((player, slot));
        }

        await Task.WhenAll(cases.Zip(players, (row, entry) => ExerciseAsync(entry.Player, entry.Slot, row)));

        async Task ExerciseAsync(HeadlessPlayer player, ushort slot,
            (ushort Parent, ushort Child, ushort Stat, string Request, bool Timed) row)
        {
            var (parent, child, stat, request, timed) = row;
            string exclusiveOn = WireFormat.ToHex(AbilityBuffs.BuildExclusive(parent, true));
            string exclusiveOff = WireFormat.ToHex(AbilityBuffs.BuildExclusive(parent, false));

            if (timed)
            {
                // A reload before landing cannot rescue the original reservation.
                player.SendRpc(request);
                await player.WaitForRpcAsync($"80 ac {Le(parent)}");
                await ChangeLoadedAsync(player, slot, parent, loaded: false);
                await ChangeLoadedAsync(player, slot, parent, loaded: true);
                await player.WaitUntilAsync(() => context.Server.DebugLog().Contains($"{parent} fizzles: the aura was unloaded during the cast"),
                    $"{parent} pending cast fizzles after unload/reload", TimeSpan.FromSeconds(8));
                Check.Equal(0, player.Received(exclusiveOn).Count, $"{parent} pending cast cannot turn on");
                Check.Equal(0, player.Received(ModPattern(child, stat)).Count, $"{child} cannot land from the invalidated cast");
                if (parent == 378)
                {
                    // A paid Deflect Virus reservation keeps its existing 10-second own reuse.
                    await player.WaitForRpcAsync("80 bd 01 7a 01 04 00 00 20 41 01");
                    await player.WaitForRpcAsync("80 bd 01 7a 01 04 00 00 20 41 00", TimeSpan.FromSeconds(12));
                }
            }

            player.SendRpc(request);
            await player.WaitForRpcAsync(exclusiveOn);
            var add = await player.WaitForRpcAsync(ModPattern(child, stat));
            var childLifetime = Stopwatch.StartNew();
            Check.Equal(5f, BitConverter.ToSingle(add.Bytes, 18), $"{child} keeps its normal duration");
            Check.That(ManageBonusRecord.TryDecode(add.Bytes, out var bonus), $"{child} add decodes");

            await ChangeLoadedAsync(player, slot, parent, loaded: false);
            await player.WaitForRpcAsync(exclusiveOff);
            Check.Equal(0, player.Received(ModPattern(child, stat, off: true)).Count,
                $"{child} remains at parent unload");
            int cursor = player.World.ViewSnapshot().Count;
            ushort before = RefusalOwnValue(player, "InnerStrengthAvailable", "02 80 80 80 10");
            player.SendRpc(request);
            await player.WaitForRpcAsync(WireFormat.ToHex(AbilityRefusals.Encode(new(parent, AbilityRefusals.NotLoaded))));

            string removedRecord = $"80 bc ?? ?? {Le(bonus!.RecordId)} 00 {Le((uint)child)} {Le(stat)} ?? ?? ?? ?? 01";
            await player.WaitForRpcAsync(removedRecord, TimeSpan.FromSeconds(8));
            Check.That(childLifetime.Elapsed >= TimeSpan.FromSeconds(4), $"{child} expires at its existing pulse boundary");
            player.SendRpc("81 48");
            await player.WaitForRpcAsync("81 49");
            Check.Equal(1, player.Received(ModPattern(child, stat)).Count, $"{parent} has no pulse after unload");
            Check.Equal(1, player.Received(exclusiveOn).Count, $"An unloaded replay cannot restart {parent}");
            Check.That(ViewUpdateValues(player, 2, "02 80 80 80 10", cursor).All(value => value >= before),
                $"Unloading {parent} ends upkeep and the stale request spends no IS");

            await ChangeLoadedAsync(player, slot, parent, loaded: true);
            player.SendRpc("81 48");
            await player.WaitForRpcAsync("81 49");
            Check.Equal(1, player.Received(exclusiveOn).Count, $"Loading {parent} does not activate it");
            Check.Equal(0, player.World.Problems.Count, $"{parent} transport framing");
        }

        async Task ChangeLoadedAsync(HeadlessPlayer player, ushort slot, ushort parent, bool loaded)
        {
            SetAbilityLoaded(player, slot, parent, level, loaded);
            await player.WaitForRpcAsync($"80 {(loaded ? "b2" : "b3")} {Le(parent)}");
        }
    }
}
