using HeadlessClient;
namespace NativeQa.Modes;

public abstract class MobiusCodeFixtureMode:NativeQaMode
{
    protected MobiusCodeFixtureMode(bool positive)
    {
        string branch=positive?"positive":"negative";
        Plan=new(new ushort[]{2,17,967},24800,-1498,Array.Empty<string>(),Array.Empty<string>(),
            new[]{ $"UPDATE characters SET level=50,exp={hds.PlayerProgression.ExperienceForLevel(50)},healthC=2000,healthM=5000,innerStrC=1000,innerStrM=1000,y=495,alignment=1,pvpflag=1 WHERE charId=110; UPDATE characters SET healthC=5000,healthM=5000,y=495,alignment=2,pvpflag=1 WHERE charId IN (6113,6114); UPDATE characters SET x=25000,z=-1498 WHERE charId=6114; UPDATE characters SET x=24800,z=-1198 WHERE charId=6113; DELETE FROM inventory WHERE charId IN (110,6114) AND slot IN (1,105); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,692,1,1,0),(6114,692,1,1,0);" },
            DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,Bootstrap:"mobius-ordinary-gun-helper",
            environmentOverrides:new(){["MXO_MOBIUS_QA"]="1",["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="15"},
            extraQaMetadata:new(){["MobiusPartial"]=true,["MobiusCallbackBranch"]=branch,["MobiusCallbackArm"]="?combatdepth mobius "+branch+" next",["MobiusProcSeconds"]=10,["MobiusCombatRngFixed"]=false},
            readyMessages:new[]{
                "CSR starts in the open street at 24800,495,-1498, flagged. Use QAEnemy for continuous rifle fire before casting. Arm with ?combatdepth mobius "+branch+" next, then use ordinary Mobius Code from Actions.",
                "Use the separate QAOther melee helper against CSR_Tester during the 10-second positive proc. Expect reason 14 and the Mobius refusal log. Repeat the same helper action after expiry and expect an ordinary interlock. See docs/ABILITY-MOBIUS-FREE-ATTACK.md."},
            loadAbilityAncestors:true);
    }
    public override NativeQaModePlan Plan {get;}
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");
        context.Helpers.Add("QAEnemy",enemy);await enemy.EnterWorldAsync();
        await MobiusRifleBootstrap.EquipAsync(enemy);
        var challenger=await HeadlessPlayer.LoginAsync(context.Address,"QAOther","QAOther");
        context.Helpers.Add("QAOther",challenger);await challenger.EnterWorldAsync();
        const string flag = "02 80 80 80 80 80 80 01 00 00 00 01";
        await enemy.WaitForViewUpdateAsync(2, flag);
        await challenger.WaitForViewUpdateAsync(2, flag);
        return Array.Empty<byte>();
    }

    internal static async Task<ForcedEntry> SendForcedEntryAsync(HeadlessPlayer challenger, ViewCreation target)
    {
        string view = WireFormat.ToHex(BitConverter.GetBytes(target.ViewId));
        string handle = $"{view} {target.SpawnId:x2} 00";
        var denial = BytePattern.Parse($"4d ?? ?? ?? ?? {handle}");
        int cursor = challenger.World.RpcSnapshot().Count;
        challenger.SendRpc($"40 {handle} 01 6b");
        byte[]? observed = null;
        await challenger.WaitUntilAsync(() =>
        {
            observed = challenger.World.RpcSnapshot().Skip(cursor).Select(record => record.Bytes)
                .FirstOrDefault(bytes => bytes.Length > 0 && (bytes[0] == 0x47 || denial.IsMatch(bytes)));
            return observed != null;
        }, "ordinary interlock entry or an explicit 4d refusal", HeadlessPlayer.DefaultTimeout);
        byte[] response = observed!;
        return new ForcedEntry(response[0] == 0x47, response[0] == 0x4d ? response[1] : null,
            WireFormat.ToHex(response));
    }

    internal sealed record ForcedEntry(bool Opened, byte? RefusalReason, string Reply);

    internal static async Task<object> VerifyInterlockAsync(ServerInstance server, HeadlessPlayer victim,
        HeadlessPlayer shooter, HeadlessPlayer challenger, bool positive)
    {
        await victim.EnterWorldAsync();
        var aim = await shooter.WaitForViewNamedAsync(victim.Handle);
        var target = await challenger.WaitForViewNamedAsync(victim.Handle);
        string view = WireFormat.ToHex(BitConverter.GetBytes(aim.ViewId));
        shooter.SendRpc($"41 {view} {aim.SpawnId:x2} 00");
        await victim.WaitForRpcAsync("81 68");
        string branch = positive ? "positive" : "negative";
        victim.SendChat($"?combatdepth mobius {branch} next");
        await victim.WaitForTextAsync($"Mobius callback chance armed once: {branch}");
        // Same 43-byte stock self-cast from abilities_mobiuscode as the ordinary live replay.
        victim.SendRpc("80 b9 c7 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 e0 c7 05 64 41 08 6b 00 00 00 00 00");
        await victim.WaitForViewUpdateAsync(2, $"02 80 80 80 80 30 {(positive ? "10" : "12")} 31 46 12 ??");
        ForcedEntry? refused = null;
        if (positive)
        {
            refused = await SendForcedEntryAsync(challenger, target);
            Check.That(!refused.Opened && refused.RefusalReason == 14 &&
                server.DebugLog().Contains("[MOBIUS] Forced interlock request resisted: QAOther -> CSR_Tester", StringComparison.Ordinal),
                "Dedicated challenger gets the explicit Mobius refusal during protection");
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
        var accepted = await SendForcedEntryAsync(challenger, target);
        Check.That(accepted.Opened, "Dedicated challenger opens an ordinary interlock without protection");
        return new { ProtectedEntry = refused, UnprotectedEntry = accepted };
    }
}
public sealed class MobiusCodePositiveMode:MobiusCodeFixtureMode
{
    public MobiusCodePositiveMode():base(true){}
    public override string Name=>"mobius-free-attack-positive";
}
public sealed class MobiusCodeNegativeMode:MobiusCodeFixtureMode
{
    public MobiusCodeNegativeMode():base(false){}
    public override string Name=>"mobius-free-attack-negative";
}

/// <summary>Ordinary rifle equip: opcode63 has byte source/destination, while reply65 uses u16 slots.</summary>
public static class MobiusRifleBootstrap
{
    public static byte[] Request=>new byte[]{0x63,0x01,0x69};
    private static readonly byte[] Success=Convert.FromHexString("650100690000");
    private static readonly byte[] Refusal=Convert.FromHexString("650100010000");
    private static readonly BytePattern RifleInHand=BytePattern.Parse("02 00 02 80 80 80 80 08 b4 02 00 00");
    public static bool HasEquipEvidence(IEnumerable<byte[]> replies,IEnumerable<byte[]> currentViews)=>
        replies.Any(r=>r.SequenceEqual(Success))&&currentViews.Any(RifleInHand.Occurs);
    public static async Task EquipAsync(HeadlessPlayer enemy)
    {
        int rpcCursor=enemy.World.RpcSnapshot().Count,viewCursor=enemy.World.ViewSnapshot().Count;
        enemy.SendRpc(Request);
        await enemy.WaitUntilAsync(()=>
        {
            var replies=enemy.World.RpcSnapshot().Skip(rpcCursor).Select(r=>r.Bytes).ToArray();
            Check.That(!replies.Any(r=>r.SequenceEqual(Refusal)),"Ordinary QAEnemy rifle equip was refused; no ready claim");
            return HasEquipEvidence(replies,enemy.World.ViewSnapshot().Skip(viewCursor));
        },"Successful bag1->weapon105 equip AND current own03 EquippedItemID692",HeadlessPlayer.DefaultTimeout);
    }
}
