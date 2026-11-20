using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CoderPetPvpPresentationMode : NativeQaMode
{
    private static readonly (string Name, int X, int Z, byte Organization, bool Flag)[] Stands =
    {
        ("QAFriend", 25300, -1850, 1, true), ("QAEnemy", 24300, -1850, 2, true), ("QAFar", 24800, -900, 3, false)
    };

    public override string Name => "ability-parity-pet-pvp-presentation";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, 1008, 22 }, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level=18,exp={PlayerProgression.ExperienceForLevel(18)},y=495,pvpflag=0 WHERE charId IN (110,6111,6112,6114)",
            "UPDATE characters SET alignment=CASE charId WHEN 6114 THEN 2 WHEN 6112 THEN 3 ELSE 1 END WHERE charId IN (110,6111,6112,6114)",
            "UPDATE characters SET x=31000+100*(charId-6111),z=-1498 WHERE charId IN (6111,6112,6114)"
        }, DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "0",
            ["MXO_CONDITIONAL_POWERLESS_QA"] = "1"
        }, readyMessages: new[]
        {
            "After CSR is fully in world, move helpers with chat: QAFriend ?tp 25300 495 -1850 0 0 0, QAEnemy ?tp 24300 495 -1850 0 0 0, QAFar ?tp 24800 495 -900 0 0 0. Flag QAFriend and QAEnemy with helper ?pvpflag on. Leave QAFar unflagged.",
            "Use ?pvpflag on on CSR. Then have all four actors run ?pet summon 22. Select each pet in turn. Expect own, same-organization and unflagged pets to be friendly. Expect the flagged opposing QAEnemy pet to display as an enemy. Distinguish whose pet you select by stand and its 1.5 m east spawn offset.",
            "While QAEnemy's pet remains present, send helper ?pvpflag off, reselect it and check friendly. Reflag QAEnemy and reselect to check hostility again. Confirm the pet stays on the same view and stays level 7. Mode 2 tests a flag-based label. Combat authorization is unchanged."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await helper.EnterWorldAsync();
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        await caster.EnterWorldAsync();
        await SetFlag(caster, true);
        var views = new Dictionary<string, ViewCreation>();
        foreach (var stand in Stands)
        {
            var helper = context.Helpers[stand.Name];
            helper.SendChat($"?tp {stand.X} 495 {stand.Z} 0 0 0");
            await caster.WaitForViewNamedAsync(stand.Name);
            if (stand.Flag) await SetFlag(helper, true);
            helper.SendChat("?pet summon 22");
            await helper.WaitForRpcAsync("81 76");
            var pet = await SeePet(caster, helper.CharId);
            Verify(pet, helper.CharId, stand.Organization, stand.Name == "QAEnemy");
            views.Add(stand.Name, pet);
        }
        caster.SendChat("?pet summon 22");
        await caster.WaitForRpcAsync("81 76");
        Verify(await SeePet(caster, caster.CharId), caster.CharId, 1, true);
        var enemy = context.Helpers["QAEnemy"];
        Verify(await SeePet(enemy, caster.CharId), caster.CharId, 1, true);
        Verify(await SeePet(context.Helpers["QAFriend"], caster.CharId), caster.CharId, 1, false);

        var selected = views[enemy.Handle];
        foreach (bool flag in new[] { false, true })
        {
            int start = caster.World.ViewSnapshot().Count;
            await SetFlag(enemy, flag);
            string update = $"04 80 a0 02 80 80 80 80 02 00 00 00 {(flag ? "01" : "00")}";
            byte[] prefix = WireFormat.FromHex(WireFormat.ToHex(BitConverter.GetBytes(selected.ViewId)) + " " + update);
            await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(start).Any(bytes => bytes.AsSpan().IndexOf(prefix) >= 0),
                $"existing view {selected.ViewId} updates owner's PvP flag to {flag}", HeadlessPlayer.DefaultTimeout);
        }
        string handle = WireFormat.ToHex(BitConverter.GetBytes(selected.ViewId));
        caster.SendRpc($"81 51 {handle} {selected.SpawnId:x2}");
        int conditionAt = caster.World.ViewSnapshot().Count;
        caster.SendChat("?combatdepth stagger 1");
        await caster.WaitForTextAsync("Stagger fixture applied 883/201 to Nuker Simulacrum for 1 seconds");
        byte[] staggered = WireFormat.FromHex($"{handle} 04 80 a0 02 80 80 80 80 02 08 00 00 01");
        byte[] clear = WireFormat.FromHex($"{handle} 04 80 a0 02 80 80 80 80 02 00 00 00 01");
        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(conditionAt)
            .Any(bytes => bytes.AsSpan().IndexOf(staggered) >= 0), "Pet view keeps the PvP bit alongside Staggered", HeadlessPlayer.DefaultTimeout);
        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(conditionAt)
            .Any(bytes => bytes.AsSpan().IndexOf(clear) >= 0), "Natural Staggered expiry leaves the projected PvP bit on", HeadlessPlayer.DefaultTimeout);
        Check.Equal(1, caster.World.ViewSnapshot().Skip(conditionAt).Count(bytes => bytes.AsSpan().IndexOf(staggered) >= 0),
            "The visibility pass does not repeat the immediate Staggered update");
        Check.Equal(1, caster.World.CreationSnapshot().Count(view => view.ViewId == selected.ViewId), "Owner flag changes preserve the pet view");
        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Pet presentation packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "pet-pvp-presentation.json"), JsonSerializer.Serialize(
            new { Mode = 2, Witnesses = views.Select(witness => new { Name = witness.Key, witness.Value.ViewId,
                Organization = witness.Value.Attributes["OrganizationID"][0], Flag = PetFlag(witness.Value) }),
                SameViewRefresh = selected.ViewId, Staggered = WireFormat.ToHex(staggered), Clear = WireFormat.ToHex(clear) }));
        return Array.Empty<byte>();
    }

    private static async Task SetFlag(HeadlessPlayer actor, bool on)
    {
        actor.SendChat(on ? "?pvpflag on" : "?pvpflag off");
        await actor.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 80 01 00 00 00 {(on ? "01" : "00")}");
    }

    private static async Task<ViewCreation> SeePet(HeadlessPlayer viewer, uint owner)
    {
        ViewCreation? seen = null;
        await viewer.WaitUntilAsync(() => (seen = viewer.World.CreationSnapshot().LastOrDefault(view => view.Goid == 40376 &&
            view.Attributes.TryGetValue("OwnerCharacterID", out var field) && BitConverter.ToUInt32(field) == owner)) != null,
            $"{viewer.Handle} sees simulacrum owned by {owner}", HeadlessPlayer.DefaultTimeout);
        return seen!;
    }

    private static bool PetFlag(ViewCreation pet) => pet.Attributes.TryGetValue("ConditionStateFlags", out var flags) &&
        (BitConverter.ToUInt32(flags) & PvpFlag.PvpBit) != 0;

    private static void Verify(ViewCreation pet, uint owner, byte organization, bool flag)
    {
        Check.Equal(owner, BitConverter.ToUInt32(pet.Attributes["MasterCharacterID"]), "Pet master remains the owner");
        Check.Equal(organization, pet.Attributes["OrganizationID"][0], "Pet view carries the actual owner organization");
        Check.Equal(flag, PetFlag(pet), "Pet view carries the recipient's PvP eligibility flag");
        Check.Equal((byte)7, pet.Attributes["Level"][0], "Pet level remains the real rank-1 Nuker clamp");
        Check.That(!pet.Attributes.ContainsKey("IsEnemy"), "No generic enemy attribute is introduced");
    }
}
