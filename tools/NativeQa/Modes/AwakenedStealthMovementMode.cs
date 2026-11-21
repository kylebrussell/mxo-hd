using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedStealthMovementMode : NativeQaMode
{
    private const int Level = 25, X = 24800, Y = 495, Z = -1498;
    private const ushort Speed = 12, Sprint = 184, Sneak = 1111, Invisibility = 1116;
    // Full stock requests from 11701b9dd84e, as replayed by PR658.
    private const string SpeedUse = "80b90c00000000000000000000000000000000000000000000000000000015000000071c086b0000000000";
    private const string SprintUse = "80b9b800000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Speed, Sprint).Order().ToArray();

    public override string Name => "ability-parity-awakened-stealth-movement";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X,-3247)},innerStrC=1000,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
            "UPDATE characters SET x=25100,y=495,z=-1798,alignment=1,pvpflag=0 WHERE charId=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Speed, Sprint }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "12,184", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "CSR (24800,495,-1498) faces down the open street toward z=-3247. Friendly QAFriend (25100,495,-1798) observes clear of the ice FX. Level 25 and full prerequisites; page 2 slots 1/2 are Hyper-Speed/Hyper-Sprint. No selection, bag use or real-client helper. Keep the same forward key, direction, and run/walk toggle in every sample; stop each pass before the bend and turn back to the stand.",
            "Type ?stealth sneak. After [STEALTH] CSR_Tester 1111 on, hold forward for about two seconds and release. Capture ordinary position frames for elapsed time and displacement. Press slot 1. An accepted press ends the old Sneak on this server and adds source 12 stats 115/+45, 877/+12 and 878/+12 for 600s. If stock refuses locally, capture that, type ?stealth off and then press slot 1.",
            "Re-enter Sneak with ?stealth sneak and run the same forward sample with Speed active. Type ?stealth off, press slot 2 for source 184 stats 115/877/878 +15, then re-enter Sneak and sample again with both active. Confirm a Sprint drain of 25 while Sneak stays on. To clear for the next set, type ?stealth off, toggle slot 2 off and type ?dismissbuff 12. Expect both source families to remove and Sprint own reuse 5s.",
            "Repeat baseline, Speed, and Speed plus Sprint using ?stealth invis instead of ?stealth sneak, clearing the stance before each buff activation. Source 1116 stat 115 is -300. Allow Sprint reuse to clear. End the stance and both buffs. No maximum [VITALS] changes or [PVP] rounds. The native packet sample measures actual client movement; the headless replay verifies the state and records."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var ranks = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => ranks.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => ranks.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Both speed abilities and rank prerequisites fit stock memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await observer.WaitForViewNamedAsync(actor.Handle);
        await SampleAsync("sneak", Sneak, -200);
        await SampleAsync("invis", Invisibility, -300);
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Stealth movement framing");
        Console.WriteLine("AWAKENED STEALTH MOVEMENT PASS: both speed buff record families coexist with Sneak and Invisibility and clean up separately.");
        return Array.Empty<byte>();

        async Task SampleAsync(string stance, ushort stanceSource, int penalty)
        {
            int from = actor.World.RpcSnapshot().Count;
            actor.SendChat($"?stealth {stance}");
            await StealthMovementAsync(from, stanceSource, penalty, false);
            actor.SendRpc(Convert.FromHexString(SpeedUse));
            await StealthMovementAsync(from, stanceSource, penalty, true);
            await RecordsAsync(from, Speed, 45, 12, 600, false);
            int speedStance = actor.World.RpcSnapshot().Count;
            actor.SendChat($"?stealth {stance}");
            await StealthMovementAsync(speedStance, stanceSource, penalty, false);
            await ClearStanceAsync(stanceSource, penalty);

            actor.SendRpc(Convert.FromHexString(SprintUse));
            await RecordsAsync(from, Sprint, 15, 15, 0, false);
            int bothStance = actor.World.RpcSnapshot().Count;
            int logAt = server.DebugLog().Length;
            actor.SendChat($"?stealth {stance}");
            await StealthMovementAsync(bothStance, stanceSource, penalty, false);
            await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("[BUFF-DRAIN] char110 ability184", StringComparison.Ordinal),
                "Sprint drains while the stealth stance is active", TimeSpan.FromSeconds(8));
            Check.That(!actor.World.RpcSnapshot().Skip(bothStance).Any(packet => StealthMovement(packet, stanceSource, penalty, true)),
                $"Sprint drain preserves {stance}");
            await ClearStanceAsync(stanceSource, penalty);
            int closing = actor.World.RpcSnapshot().Count;
            actor.SendRpc(Convert.FromHexString(SprintUse));
            await RecordsAsync(closing, Sprint, 15, 15, 0, true);
            actor.SendRpc("80 bb 0c 00");
            await RecordsAsync(closing, Speed, 45, 12, 600, true);
            await EvidenceRetestWire.TimerAsync(actor, closing, Sprint, AbilityCooldowns.OwnTimer, 5, ended: true);
        }

        async Task ClearStanceAsync(ushort source, int penalty)
        {
            int from = actor.World.RpcSnapshot().Count;
            actor.SendChat("?stealth off");
            await StealthMovementAsync(from, source, penalty, true);
        }

        async Task RecordsAsync(int from, ushort source, int speed, int stances, float life, bool remove)
        {
            foreach (var (stat, value) in new (ushort, int)[] { (115, speed), (877, stances), (878, stances) })
                await StockAbilityProfile.ObserveAsync(actor, from,
                    frame => StockAbilityProfile.Bonus(frame, source, stat, value, life, remove),
                    $"source {source}, stat {stat}, value {value}, remove={remove}");
        }

        Task<RpcRecord> StealthMovementAsync(int from, ushort source, int value, bool remove) =>
            StockAbilityProfile.ObserveAsync(actor, from, frame => StealthMovement(frame, source, value, remove),
                $"Stealth source {source} movement {value}, remove={remove}");
    }

    private static bool StealthMovement(RpcRecord packet, ushort source, int value, bool remove) =>
        ManageBonusRecord.TryDecode(packet.Bytes, out var record) && record!.SourceId == source &&
        record.TargetId == 115 && record.SignedValue == value && record.AddRemoveType == (remove ? 1 : 0);
}
