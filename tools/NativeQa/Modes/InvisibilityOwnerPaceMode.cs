using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class InvisibilityOwnerRank20Mode() : InvisibilityOwnerPaceMode(20);
public sealed class InvisibilityOwnerRank50Mode() : InvisibilityOwnerPaceMode(50);

public abstract class InvisibilityOwnerPaceMode(int rank) : NativeQaMode
{
    private const ushort Parent = 175, Belt = Stealth.InvisibilityGrantAbility, Speed = 12, Spy = CombatStyles.SpyStyle;
    private const int X = 17000, Y = 495, Z = 6400, LaneEnd = 8300, Level = 50;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Parent, Spy, Speed).Order().ToArray();
    private int Bonus => rank / 2;

    public override string Name => $"ability-parity-invisibility-owner-rank{rank}";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level={rank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Parent)}",
        "DELETE FROM inventory WHERE charId=110 AND (slot IN (40,107) OR goid=460)",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"
    }, new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0,alignment=1,innerStrC=700,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x=17400,y={Y},z={Z},alignment=1 WHERE charId=6111"
    }, DevCommands: true, hotbarAbilities: new[] { Spy, Speed },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 460, "Invisibility Belt") },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [MovementSampleQa.Mode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "175", ["NativeCloseableRows"] = 1, ["ParentRank"] = rank },
        readyMessages: new[]
        {
            $"Level 50, rank {rank} Invisibility parent, memory {Loadout.Sum(AbilityMemory.Cost)}/100. Start on verified HOME facing +Z; {Home} only if displaced. Same-side observer 4 m east. No target. Page 2 Spy Style, Hyper-Speed, Invisibility Belt; use Actions > Tools > Invisibility Belt if shortcut fails.",
            $"On login source 175 stat 1051 +{rank / 2}. Select Spy then ordinary Belt (3 s cast, 50 IS, 30 s reuse). Expect own and observer movement scale {(rank / 2) / 100f:0.00}, and Belt pose 1a. Take a 2 s steady forward sample within z6400–{LaneEnd}; use [MOVE-SAMPLE] planar distance over ms and match gait in the other rank fixture.",
            $"Wait for Belt reuse, turn Belt off, return HOME, then activate ordinary Hyper-Speed and cast Belt again. Verify source 12 stat 878 +12, movement scale {(rank / 2 + 12) / 100f:0.00}, then repeat a matched forward sample. Relative to untreated Belt at the same gait the model predicts {(100f + rank / 2 + 12) / (100f + rank / 2):0.000}×. After the second Belt reuse clears, toggle it off and confirm the rank-dependent Belt contribution leaves. Hyper-Speed's ordinary run modifier remains active.",
            "Run both rank-20 and rank-50 modes at identical pose and loadout except parent rank. The linked percentage is an implementation of the shipped 878 → 1051 attribute relation. Save the client scale and measured pace from both ranks."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Belt pace requires NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.Equal(Home, InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false), "Printed HOME");
        InvisibilityBeltWatcherMode.Warp(17400, Y, Z, 192, inside: false);
        for (int z = Z; z <= LaneEnd; z += 100)
        {
            InvisibilityBeltWatcherMode.Warp(X, Y, z, 0, inside: false);
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, z), "Clear Belt sampling lane");
        }
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, Parent, 1051, Bonus, 0, false), "Owner's learned percentage");
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        int at = actor.World.RpcSnapshot().Count;
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpeedUse);
        await StockAbilityProfile.ObserveAsync(actor, at, frame => StockAbilityProfile.Bonus(frame, Speed, 878, 12, 600, false), "Independent invisible speed channel");
        actor.SendRpc(AwakenedStockStealthSpeedMode.BeltUse);
        await EvidenceRetestWire.CastBarAsync(actor, Belt, 3);
        await EvidenceRetestWire.TimerAsync(actor, at, Belt, AbilityCooldowns.OwnTimer, 30);
        await ScaleAsync(Bonus / 100f + .12f);
        actor.SendChat("?dismissbuff 12");
        await StockAbilityProfile.ObserveAsync(actor, at, frame => StockAbilityProfile.Bonus(frame, Speed, 878, 12, 600, true), "Independent speed removed");
        await actor.WaitForViewUpdateAsync(2, WireFormat.ToHex(AbilityBuffs.BuildMovementUpdate(Bonus / 100f, Stealth.Invisible)[2..]));
        return Array.Empty<byte>();

        Task<object> Unused() => throw new NotImplementedException();
        async Task ScaleAsync(float scale) =>
            await actor.WaitForViewUpdateAsync(2, WireFormat.ToHex(PlayerViewAttributes.Build((PlayerViewAttributes.SelfMovementScale, PlayerViewAttributes.F32(scale)))));
    }
}
