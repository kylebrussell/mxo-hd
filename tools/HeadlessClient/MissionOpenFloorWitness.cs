using System.Text.Json;

namespace HeadlessClient;

/// <summary>Independent proof reader for the persisted open-floor witness. It never plans, changes a source spot,
/// or substitutes coordinates when the server has not published a qualified placement.</summary>
public sealed class MissionOpenFloorWitness
{
    public int Policy {get;} public ushort World {get;} public int Mission {get;} public int Phase {get;} public int ObjectId {get;}
    public uint SourceKey {get;} public ushort SourceGoid {get;} public ushort ConcreteGoid {get;}
    public string RawFingerprint {get;} public string InputFingerprint {get;} public string Quaternion {get;}
    public IReadOnlyList<double> Centre {get;} public IReadOnlyList<double> Standing {get;} public IReadOnlyList<double> Route {get;}
    public IReadOnlyList<ushort> Surface {get;} public IReadOnlyList<ushort> RootSurface {get;} public IReadOnlyList<ushort> RouteSurfaces {get;}
    public string RawJson {get;}
    private MissionOpenFloorWitness(JsonElement value)
    {
        if(value.ValueKind!=JsonValueKind.Object || value.EnumerateObject().GroupBy(p=>p.Name,StringComparer.Ordinal).Any(g=>g.Count()!=1))
            throw new FormatException("Missing or duplicate open-floor witness fields");
        Policy=value.GetProperty("Policy").GetInt32();World=value.GetProperty("World").GetUInt16();Mission=value.GetProperty("Mission").GetInt32();
        Phase=value.GetProperty("Phase").GetInt32();ObjectId=value.GetProperty("ObjectId").GetInt32();SourceKey=value.GetProperty("SourceKey").GetUInt32();
        SourceGoid=value.GetProperty("SourceGoid").GetUInt16();ConcreteGoid=value.GetProperty("ConcreteGoid").GetUInt16();
        RawFingerprint=Hex(value,"RawFingerprint",64);InputFingerprint=Hex(value,"InputFingerprint",64);Quaternion=Hex(value,"Quaternion",32);
        Centre=Coordinates(value,"Centre",true);Standing=Coordinates(value,"Standing",true);Route=Coordinates(value,"Route",false);
        Surface=Ids(value,"Surface",true);RootSurface=Ids(value,"RootSurface",true);RouteSurfaces=Ids(value,"RouteSurfaces",false);
        byte[] q=Convert.FromHexString(Quaternion);
        if(Enumerable.Range(0,4).Any(i=>!float.IsFinite(BitConverter.ToSingle(q,i*4))))throw new FormatException("Nonfinite open-floor quaternion");
        if((Policy!=3&&Policy!=4) || World==0 || Mission<=0 || Phase<0 || ObjectId<=0  || SourceGoid==0 || ConcreteGoid==0)
            throw new FormatException("Invalid open-floor witness identity");
        if(Route.Count<6 || Route.Count!=RouteSurfaces.Count+3 || !RouteSurfaces.Take(3).SequenceEqual(RootSurface))
            throw new FormatException("Invalid route segment framing or root surface");
        if(!Route.TakeLast(3).SequenceEqual(Standing))
            throw new FormatException("Standing point is not a published route endpoint");
        RawJson=value.GetRawText();
    }
    public static MissionOpenFloorWitness Read(JsonElement value)
    {
        try{return new MissionOpenFloorWitness(value);}
        catch(Exception e)when(e is KeyNotFoundException or InvalidOperationException or OverflowException or ArgumentException)
        {throw new FormatException("Malformed open-floor witness",e);}
    }
    private static string Hex(JsonElement value,string name,int length)
    {
        string? s=value.GetProperty(name).GetString();
        if(s==null||s.Length!=length||s.Any(c=>!Uri.IsHexDigit(c)))throw new FormatException("Invalid "+name);
        return s;
    }
    private static IReadOnlyList<double> Coordinates(JsonElement value,string name,bool single)
    {
        var a=value.GetProperty(name).EnumerateArray().Select(x=>x.GetDouble()).ToArray();
        if(a.Length==0||a.Length%3!=0||(single&&a.Length!=3)||a.Any(x=>!double.IsFinite(x)))throw new FormatException("Invalid "+name);
        return Array.AsReadOnly(a);
    }
    private static IReadOnlyList<ushort> Ids(JsonElement value,string name,bool single)
    {
        var a=value.GetProperty(name).EnumerateArray().Select(x=>x.GetUInt16()).ToArray();
        if(a.Length==0||a.Length%3!=0||(single&&a.Length!=3))throw new FormatException("Invalid "+name);
        return Array.AsReadOnly(a);
    }
    /// <summary>Finds exact bound-source creation headers in retained03 lists. A legal source Hidden update needs
    /// this source-to-view binding first. This is scoped identity evidence, not a complete03 update decoder.</summary>
    public static IReadOnlyList<int> SourceBindingHeaders(byte[] section,ushort goid,uint sourceKey)
    {
        var found=new List<int>();
        for(int i=0;i+12<section.Length;i++)
            if(section[i]==1&&section[i+1]==0&&(section[i+2]==0||section[i+2]==8)&&
                BitConverter.ToUInt16(section,i+3)==goid&&BitConverter.ToUInt32(section,i+5)==sourceKey&&
                section[i+10]==0xcd&&section[i+11]==0xab)found.Add(i);
        return Array.AsReadOnly(found.ToArray());
    }
    public void Require(int mission,int phase,int objectId,uint sourceKey,ushort sourceGoid,ushort concreteGoid)
    {
        if(World!=1||Mission!=mission||Phase!=phase||ObjectId!=objectId||SourceKey!=sourceKey||SourceGoid!=sourceGoid||ConcreteGoid!=concreteGoid)
            throw new FormatException("Open-floor witness does not identify the current mission object");
    }
}
