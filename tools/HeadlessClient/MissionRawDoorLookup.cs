namespace HeadlessClient;
// Mechanically extracted qualified241 genuine-legacy lookup; measured entries never call it.
public static class MissionRawDoorLookup
{
    public static uint DoorAt(IEnumerable<string> rows,double[] pose)
    {
        var ids=rows.Where(l=>!string.IsNullOrWhiteSpace(l)&&char.IsDigit(l[0])).Select(l=>l.Split(','))
            .Where(f=>f.Length>=11&&f[0]=="1"&&new ushort[]{417,419}.Contains(BitConverter.ToUInt16(Convert.FromHexString(f[4]),0))&&
                Enumerable.Range(0,3).All(i=>double.Parse(f[6+i],System.Globalization.CultureInfo.InvariantCulture)==pose[i]))
            .Select(f=>BitConverter.ToUInt32(Convert.FromHexString(f[2]),0)).Distinct().Take(2).ToArray();
        if(ids.Length!=1)throw new InvalidDataException("Exact current-site door missing or ambiguous");return ids[0];
    }
}
