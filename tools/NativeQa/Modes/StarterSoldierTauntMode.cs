using System.Buffers.Binary;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSoldierTauntMode : NativeQaMode
{
    private const int Level = 15;
    private const int NearX = CombatFixtureLoadout.CasterX, FarX = NearX + 200;
    private const int Y = CombatFixtureLoadout.GroundY, Z = CombatFixtureLoadout.CasterZ;
    private const ushort Taunt = CombatSpecials.Taunt;
    // PR312 stock Taunt request, 33779fd94eb0:5686–5687.
    private const string StockTaunt = "80b98702060004000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";

    public override string Name => "ability-parity-starter-soldier-taunt";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, Taunt }.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
            .Distinct().Order().ToArray(), FarX, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},innerStrC=200,innerStrM=200 WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
        }, readyMessages: new[]
        {
            "At the starting stand, select Melee Dummy 5 m west and cast Taunt (647) from Actions > Soldier. Expect a 10s reuse and no Taunt on Melee Dummy.",
            $"Once reuse ends, move to ?tp {NearX} {Y} {Z} 0 0 0 (3 m from Melee Dummy). Cast Taunt again. Verify Melee Dummy now hates CSR most and retain the two stock presses."
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "647", ["NativeCloseableRows"] = 1 });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet();
        Check.That(Plan.Abilities.All(loaded.Contains), "Stock archive contains Taunt and every prerequisite");
        await caster.EnterWorldAsync();
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        double dummyX = BitConverter.ToDouble(dummy.Attributes["Position"]);
        Check.That(Math.Abs(FarX - dummyX) > 400 && Math.Abs(NearX - dummyX) < 400, "Both stands straddle the stored 4 m radius");

        byte[] request = Convert.FromHexString(StockTaunt);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), DuelPackets.Handle(dummy.ViewId, dummy.SpawnId));
        int cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(request);
        await EvidenceRetestWire.TimerAsync(caster, cursor, Taunt, AbilityCooldowns.OwnTimer, 10);
        await caster.WaitForRpcAsync(packet => packet.Bytes.AsSpan().SequenceEqual(
            AbilityCooldowns.BuildTimer(Taunt, AbilityCooldowns.OwnTimer, 10, false)), "Taunt reuse completes", TimeSpan.FromSeconds(14));
        string taunt = $"[SPECIAL] Taunt: {dummy.Name} now hates {caster.CharId} most";
        Check.That(!server.DebugLog().Contains(taunt), "Selecting an NPC outside the radius does not pull it");

        caster.SendChat($"?tp {NearX} {Y} {Z} 0 0 0");
        await caster.WaitForTextAsync("Teleported to 0/0");
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(request);
        await EvidenceRetestWire.TimerAsync(caster, cursor, Taunt, AbilityCooldowns.OwnTimer, 10);
        await caster.WaitUntilAsync(() => server.DebugLog().Contains(taunt), "In-radius Taunt changes the NPC's aggro", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, caster.World.Problems.Count, "Taunt request framing");
        return Array.Empty<byte>();
    }
}
