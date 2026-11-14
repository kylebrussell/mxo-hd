using System;
namespace hds
{
    // Single shared immutable registry. Conservative actual-asset/clip envelope, not retail placement or GPU parity.
    internal static class MissionOpenFloorProfiles
    {
        internal const string ModelSourceSha256 = "c38150d02111f6cfa51e1f2ffd4b18b0b8e0ea9aaf9783caa18287ffbfcd0402";
        internal static bool TryModel(ushort goid,out OpenFloorModelBounds model)
        {
            model=goid switch
            {
                3 => new OpenFloorModelBounds(3,new OpenFloorPoint(-42.56936264038086,-25.00377655029297,-41.43278884887695),new OpenFloorPoint(42.56936264038086,25.135780334472656,41.43278884887695),-25.00377655029297,"actual asset/clip conservative profile:"+ModelSourceSha256),
                548 => new OpenFloorModelBounds(548,new OpenFloorPoint(-98.42459106445312,-40.0,-125.44692993164062),new OpenFloorPoint(98.39927673339844,96.44692993164062,125.38443756103516),-40.0,"actual asset/clip conservative profile:"+ModelSourceSha256),
                552 => new OpenFloorModelBounds(552,new OpenFloorPoint(-96.58653259277344,-50.0,-40.13495635986328),new OpenFloorPoint(39.400634765625,50.0,98.58653259277344),-50.0,"actual asset/clip conservative profile:"+ModelSourceSha256),
                _ => null
            };
            return model!=null;
        }
        internal static bool TryUsable(ushort goid,out MissionOpenFloorUsableRange.Profile profile)
        {
            profile=goid switch
            {
                3 => MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new OpenFloorPoint(43,25,40),"625d6b20 omitted/nonpositive branch; startup configurable UsableExecuteMaxDist400; current client setting unobserved"),
                548 => new MissionOpenFloorUsableRange.Profile(548,3,100,new OpenFloorPoint(50,40,70),"stored OpenUse100; native size floor HalfZ+65 yields135"),
                552 => new MissionOpenFloorUsableRange.Profile(552,3,100,new OpenFloorPoint(40,50,35),"stored OpenUse100; native size floor HalfZ+65 yields100"),
                _ => null
            };
            return profile!=null;
        }
    }
}
