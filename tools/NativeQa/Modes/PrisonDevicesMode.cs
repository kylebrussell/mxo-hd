using hds;
namespace NativeQa.Modes;
public sealed class PrisonDevicesMode:NativeQaMode
{
    public override string Name=>"prison-devices";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,77},17000,2300,
        Array.Empty<string>(),new[]{"DELETE FROM inventory WHERE charId=110 AND slot IN (1,2,106); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,42722,1,1,0),(110,42723,2,1,0)"},
        new[]{"UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",healthC=5000,healthM=5000,innerStrC=50,innerStrM=2000,y=495,rotation=0 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        extraQaMetadata:new(){["PrisonDevicesQa"]=true,["AcquisitionImplemented"]=false,["PrisonDeviceEquipmentSlot"]=106},
        readyMessages:new[]{"Equipment test only; acquisition is unimplemented. Equip Prison Mentality Device from bag1 into program-launcher slot106, then Actions>Tools/Activate Item. No free Abilities949/957 are granted. Use ?health 5000 then ?health 4900 for one deterministic health-change event (no RNG override). Expect toughness1→3 and30s expiry restoring6 IS, subject to ordinary regeneration/max. Unequip/re-equip cannot evade360s family timer. After timer or fresh profile, Mastery bag2 starts2→5 and restores10 IS on unequip; removing either device deactivates both Prison parents. Use ordinary UI; record actual80b9slot106/flag1 and106moves. See docs/PRISON-DEVICES.md."});
}
