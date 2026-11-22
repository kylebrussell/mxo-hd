using System.Buffers.Binary;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task CoderStaticBlastTiersServer(ScenarioContext context)
    {
        const ushort level = 29, blast = 33, barrage = 590, higherBlast = 591;
        const string captured33 = "80b92100050003000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
        const string capturedEmptyMind = "80b90800070005000000000000000000000000000000000000000000a8fb7fbf00000000086b0000000000";
        var loadout = new ushort[] { 2, 17, 1008, blast, barrage, higherBlast }
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order()
            .Select(id => (id, level)).ToArray();
        var seed = new CharacterSeed("CoderTierCaster")
        {
            X = 17000, Y = 495, Z = 8100, Level = level, Experience = (int)PlayerProgression.ExperienceForLevel(level),
            Health = 5000, MaxHealth = 5000, InnerStrength = 500, MaxInnerStrength = 1000, Organization = 1,
            OwnedLoadedAbilities = loadout
        };
        var actor = await context.AddPlayerAsync(seed);
        var neutral = await context.AddPlayerAsync(seed with
            { Handle = "CoderTierNeutral", Z = 6600, Organization = 2, OwnedLoadedAbilities = Array.Empty<(ushort, ushort)>() });
        var resisted = await context.AddPlayerAsync(seed with
            { Handle = "CoderTierResisted", X = 17100, Z = 6600, Organization = 2, OwnedLoadedAbilities = new[] { ((ushort)8, level) } });
        var ally = await context.AddPlayerAsync(seed with
            { Handle = "CoderTierAlly", Z = 6950, OwnedLoadedAbilities = Array.Empty<(ushort, ushort)>() });
        var distant = await context.AddPlayerAsync(seed with
            { Handle = "CoderTierDistant", Z = 6400, Organization = 2, OwnedLoadedAbilities = Array.Empty<(ushort, ushort)>() });
        foreach (var player in new[] { neutral, resisted, ally, distant, actor })
            await PvpOpponentsFixture.EnterAsync(player);

        actor.SendRpc("80b9f003000000000000000000000000000000000000000000000000585cc70001000000086b0000000000");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        uint neutralHandle = await HandleAsync(neutral), resistedHandle = await HandleAsync(resisted);
        uint allyHandle = await HandleAsync(ally), farHandle = await HandleAsync(distant);
        int invalid = context.Server.DebugLog().Length;
        foreach (ushort id in new[] { barrage, higherBlast })
        {
            foreach (uint target in new uint[] { 0, allyHandle })
            {
                actor.SkipReceived();
                actor.SendRpc(Request(id, target));
                await actor.WaitForTextAsync("Select a living enemy before using this ability.");
            }
            actor.SkipReceived();
            actor.SendRpc(Request(id, farHandle));
            await MatchAsync(actor, AbilityRefusals.Encode(new AbilityRefusals.Record(id, AbilityRefusals.TargetOutOfRange)),
                "Range reason 34 for the 17 m recipient");
        }
        Check.That(!context.Server.DebugLog()[invalid..].Contains("[ABILITY-COST]"), "Invalid selection and range do not spend IS");

        var rows = new (ushort Id, ushort Family, int Cost, int Raw, int Resisted)[]
        {
            (blast, AbilityCooldowns.VirusDamage1, 15, 26, 21),
            (barrage, AbilityCooldowns.VirusEnhancedDamage, 18, 39, 32),
            (higherBlast, AbilityCooldowns.VirusDamage1, 23, 51, 42)
        };
        await CompareAsync(neutral, neutralHandle, false);
        byte[] emptyMind = Convert.FromHexString(capturedEmptyMind);
        BinaryPrimitives.WriteUInt32LittleEndian(emptyMind.AsSpan(4), 0);
        resisted.SendRpc(emptyMind);
        await resisted.WaitUntilAsync(() => context.Server.DebugLog().Contains($"[MODIFIER] char={resisted.CharId} source=8 stat=157 add value=+43 duration=20s"),
            "Empty Mind publishes +43 Viral Resistance for 20 seconds", HeadlessPlayer.DefaultTimeout);
        await CompareAsync(resisted, resistedHandle, true);

        async Task<uint> HandleAsync(HeadlessPlayer player)
        {
            var view = await actor.WaitForViewNamedAsync(player.Handle);
            return DuelPackets.Handle(view.ViewId, view.SpawnId);
        }

        byte[] Request(ushort ability, uint target)
        {
            // Server-only 590/591 requests adopt the captured 33 envelope. NativeQa never sends them until stock bytes are captured.
            byte[] frame = Convert.FromHexString(captured33);
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), ability);
            BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), target);
            return frame;
        }

        static Task<RpcRecord> MatchAsync(HeadlessPlayer player, byte[] expected, string description) =>
            player.WaitForRpcAsync(frame => frame.Bytes.AsSpan().SequenceEqual(expected), description);

        async Task CompareAsync(HeadlessPlayer target, uint handle, bool underResistance)
        {
            actor.SendRpc("81 51 " + WireFormat.ToHex(BitConverter.GetBytes(handle)));
            foreach (var row in rows)
            {
                if (row.Id == higherBlast)
                    await MatchAsync(actor, AbilityCooldowns.BuildTimer(AbilityCooldowns.VirusDamage1, AbilityCooldowns.GroupTimer, 4, false),
                        "Shared 682 release before 591");
                actor.SkipReceived();
                target.SkipReceived();
                int log = context.Server.DebugLog().Length;
                actor.SendRpc(Request(row.Id, handle));
                var cast = await actor.WaitForRpcAsync($"80 ac {WireFormat.ToHex(BitConverter.GetBytes(row.Id))}");
                Check.Equal(2f, BitConverter.ToSingle(cast.Bytes, cast.Bytes.Length - 4), $"Ability {row.Id} cast time");
                await MatchAsync(actor, AbilityCooldowns.BuildTimer(row.Id, AbilityCooldowns.OwnTimer, 6, true), "Six-second own reuse");
                await MatchAsync(actor, AbilityCooldowns.BuildTimer(row.Family, AbilityCooldowns.GroupTimer, 4, true), "Four-second group reuse");
                int expected = underResistance ? row.Resisted : row.Raw;
                await MatchAsync(target, AbilityMessages.Damage(false, row.Id, actor.Handle, target.Handle, expected, row.Raw - expected),
                    $"Ability {row.Id} deals {expected} to the selected target");
                Check.That(context.Server.DebugLog()[log..].Contains($"[ABILITY-COST] char={actor.CharId} ability={row.Id} paid={row.Cost} IS="),
                    $"Ability {row.Id} charges the shipped {row.Cost} IS");
                if (row.Id == blast)
                {
                    actor.SendRpc(Request(higherBlast, handle));
                    await MatchAsync(actor, AbilityCooldowns.BuildDenied(higherBlast, AbilityCooldowns.DeniedTimerRunning),
                        "591 is gated during 33's shared family timer");
                }
            }
            if (!underResistance)
                await MatchAsync(actor, AbilityCooldowns.BuildTimer(higherBlast, AbilityCooldowns.OwnTimer, 6, false), "Own reuse expires before the second sequence");
        }
    }
}
