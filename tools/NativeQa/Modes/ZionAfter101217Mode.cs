using HeadlessClient;
using hds;
namespace NativeQa.Modes;
public sealed class ZionAfter101217Mode:NativeQaMode
{
    public override string Name=>"zion-critical-after-101217";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17,1006,77},17000,2300,Array.Empty<string>(),Array.Empty<string>(),
        new[]{ZionAfter101217Fixture.SeedPrerequisites(110),"UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="101301",["MXO_PACKET_LOG"]="1",["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"},
        extraQaMetadata:new(){["QaBaseMaster"]="500dc117afedb2d95cf1229558564d67982f79c1",["CoreRuntimeSource"]="34dc9169695f68006ced609e32202a7566716f9d",["RequiresPr274"]=true,["RequiresPr241Generic"]=true,["ReviewedSharedV3FromPr273"]="3a70a57c",["EarlierReceiptsOnly"]=37,["SelectedMissions"]=ZionAfter101217Fixture.Missions,["NextMission"]=102101,["Missing101305NotSeeded"]=true,["ExodusRetailRewardUnproved"]=true,["LayoutSeedOnly"]=101301,["NativeRenderingProven"]=false},
        readyMessages:new[]{"SOURCE CANDIDATE, not qualified native-ready. CSR110: Tyndall Call > Critical Lord of Flies, earn101301/302/303/304/306, next102101.37 prior receipts ONLY; no101305/current progress/items seeded. All25phases/95goals/3optional must be earned. Ordinary public door/car/current saved own-GOID internal doors; both scanner Put uses; native100 V3 furniture; current multi-follower crew/extraction and normal item/follower Quit/relog. Exodus mixed furniture+Upload requires real ordinaryDown+typed011c+exactACK before outside movement, then ordinaryUp if interior work remains. No missionfloor/source reroll/legacy normalization. Headless movement/weaken aids and StillNpcs do not prove native walk/render or unaided survival. Existing story mechanics are adapted; Exodus reward borrowed from101304 is NOT primary-proven retail payout. Stop on unsupported route/capacity/mechanic; never fake goals. docs/MISSION-ZION-CRITICAL-AFTER-101217.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server=context.ActualServer??throw new InvalidOperationException("Actual owned server required");var db=context.OwnedDatabase??throw new InvalidOperationException("Owned disposable schema required");
        if(server.HasExited||server.DatabaseName!=db.Name||server.ConfiguredAddress!=context.Address||!db.Name.StartsWith(ThrowawayDatabase.Prefix,StringComparison.Ordinal))throw new InvalidOperationException("Owned seed server/schema/address mismatch");
        Check.Equal(37L,await db.LongAsync("SELECT COUNT(*) FROM char_missions WHERE charId=110"),"Exactly37 earlier receipts");
        Check.Equal(0L,await db.LongAsync("SELECT COUNT(*) FROM char_missions WHERE charId=110 AND missionId IN (101301,101302,101303,101304,101305,101306,102101)"),"No selected/missing/next receipt seeded");
        Check.Equal(0L,await db.LongAsync("SELECT COUNT(*) FROM char_active_missions WHERE charId=110 AND payload IS NOT NULL"),"No selected active progress seeded");
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{Mode=Name,EarlierReceipts=37,SelectedProgressSeeded=false,Scope="Owned prerequisite seed/auth only; no gameplay or native visibility proof"});
    }
}
