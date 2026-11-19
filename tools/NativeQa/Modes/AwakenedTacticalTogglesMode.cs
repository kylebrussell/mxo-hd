using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AwakenedTacticalTogglesMode : NativeQaMode
{
    public override string Name => "ability-parity-awakened-tactical-toggles";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 9, 15 }, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            "UPDATE characters SET y=495,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}",
            "UPDATE characters SET x=24800,y=495,z=-1198 WHERE charId=6111"
        }, DevCommands: true, loadAbilityAncestors: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Target the 30,000 HP Melee Dummy 3 m west. Start in Self Defense with no ranged weapon equipped. Enter Close Combat; use a non-Block tactic.",
            "Enable Energized Attacks (9) from Actions. Check the on record and repeated in-combat FX 0x2800070c. Attempt Precise Blow (15) and retain the refusal if stock sends the request. Turn 9 off and verify its FX stops.",
            "Enable Precise Blow (15) and check the on record and repeated in-combat FX 0x280006ff. Try Energized Attacks before turning it off and note whether stock sends it. Turn 15 off and verify no continued FX.",
            "Record stock gating at rest separately if tested. Keep exact activation, on/off, and FX records. The NPC's random rounds are not a statistical damage benchmark."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        await observer.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        var watching = await observer.WaitForViewNamedAsync(actor.Handle);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        actor.SendRpc($"40 {handle} 01 6b");
        await actor.WaitForRpcAsync("47");

        var samples = new[]
        {
            (On: (ushort)9, Blocked: (ushort)15, Effect: 0x2800070cu, Refusal: 0xc6001a9bu),
            (On: (ushort)15, Blocked: (ushort)9, Effect: 0x280006ffu, Refusal: 0xc6001a9fu)
        };
        foreach (var sample in samples)
        {
            int actorAt = actor.World.ViewSnapshot().Count;
            int observerAt = observer.World.ViewSnapshot().Count;
            var ownEffect = Effect(sample.Effect, 2, own: true);
            var otherEffect = Effect(sample.Effect, watching.ViewId, own: false);
            EvidenceRetestWire.Cast(actor, sample.On, dummy);
            await actor.WaitForRpcAsync($"80 bd 05 {EvidenceRetestWire.Le(sample.On)} 00 00 00 00 00 01");
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(actorAt).Count(ownEffect.Occurs) >= 2 &&
                observer.World.ViewSnapshot().Skip(observerAt).Count(otherEffect.Occurs) >= 2,
                $"Repeated {sample.On} FX on self and observer", TimeSpan.FromSeconds(10));

            EvidenceRetestWire.Cast(actor, sample.Blocked, dummy);
            await actor.WaitForRpcAsync($"80 ba {EvidenceRetestWire.Le(sample.Blocked)}");
            await actor.WaitForRpcAsync($"81 67 ?? ?? ?? ?? {WireFormat.ToHex(BitConverter.GetBytes(sample.Refusal))}");
            EvidenceRetestWire.Cast(actor, sample.On, dummy);
            await actor.WaitForRpcAsync($"80 bd 05 {EvidenceRetestWire.Le(sample.On)} 00 00 00 00 00 00");
            int offAt = actor.World.ViewSnapshot().Count;
            await Task.Delay(TimeSpan.FromSeconds(1.4));
            Check.That(!actor.World.ViewSnapshot().Skip(offAt).Any(ownEffect.Occurs),
                $"No {sample.On} update after toggle off");
        }

        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Tactical toggle framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "awakened-tactical-toggles.json"),
            JsonSerializer.Serialize(samples.Select(sample => new { sample.On, sample.Blocked, sample.Effect, sample.Refusal })));
        return Array.Empty<byte>();
    }

    private static BytePattern Effect(uint effect, ushort view, bool own)
    {
        byte[] payload = ServerPackets.BuildPlayerAbilityEffect(effect, 0, own);
        string viewPrefix = own ? "" : EvidenceRetestWire.Le(view) + " ";
        return BytePattern.Parse(viewPrefix + WireFormat.ToHex(payload[..^1]) + " ??");
    }
}
