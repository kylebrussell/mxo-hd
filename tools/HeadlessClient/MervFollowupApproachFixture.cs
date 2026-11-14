using hds;
using System.Globalization;
namespace HeadlessClient;
public static class MervFollowupApproachFixture
{
    public static bool RequiresWitness(MissionProgress.SavedObject item)=>
        item.Goid is 3 or 548 or 552 && MissionSites.Goid(item.Spot)!=item.Goid;
    public static uint DoorAt(IEnumerable<string> rows,double[] pose)
    {
        if(pose.Length!=3||pose.Any(v=>!double.IsFinite(v)))throw new InvalidDataException("Invalid saved lobby pose");
        var keys=rows.Where(l=>!string.IsNullOrWhiteSpace(l)&&char.IsDigit(l[0])).Select(l=>l.Split(','))
            .Where(f=>f.Length>=11&&f[0]=="1"&&new ushort[]{417,419}.Contains(BitConverter.ToUInt16(Convert.FromHexString(f[4]),0))&&
                Enumerable.Range(0,3).All(i=>double.Parse(f[6+i],CultureInfo.InvariantCulture)==pose[i]))
            .Select(f=>BitConverter.ToUInt32(Convert.FromHexString(f[2]),0)).Distinct().Take(2).ToArray();
        if(keys.Length!=1)throw new InvalidDataException("Current saved lobby door is missing or ambiguous");return keys[0];
    }
    public static bool Matches(ViewCreation view,ushort goid,IReadOnlyList<double> centre,string quaternion)=>
        view.Goid==goid&&view.Attributes.TryGetValue("Position",out var at)&&at.Length==24&&
        Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(at,i*8)==centre[i])&&
        view.Attributes.TryGetValue("Orientation",out var q)&&q.SequenceEqual(Convert.FromHexString(quaternion));
}
