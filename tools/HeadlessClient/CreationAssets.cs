using System.Globalization;
using hds;
namespace HeadlessClient;
/// <summary>Offline/fixture adapter for the same existing two CSVs; never initializes world actors or timers.</summary>
public sealed class CreationAssets
{
    private readonly Dictionary<(string,ushort),NewRSIItem> parts=new();
    private readonly HashSet<(string,ushort)> ambiguousParts=new();
    private readonly Dictionary<uint,CharacterCreation.Apparel> clothes=new();
    private CreationAssets(){}
    public static CreationAssets Load(string repositoryRoot)
    {
        var assets=new CreationAssets();
        foreach(string line in File.ReadLines(Path.Combine(repositoryRoot,"data","newrsiIDs.csv")).Skip(1).Where(s=>!string.IsNullOrWhiteSpace(s)))
        {
            string[] v=line.Split(',');if(v.Length!=5)throw new InvalidDataException("Malformed RSI fixture source");
            var part=new NewRSIItem{name=v[0],newRSIID=ushort.Parse(v[1],CultureInfo.InvariantCulture),internalId=ushort.Parse(v[2],CultureInfo.InvariantCulture),type=v[3],gender=ushort.Parse(v[4],CultureInfo.InvariantCulture)};
            var key=(part.type,part.newRSIID);
            if(assets.ambiguousParts.Contains(key))continue;
            if(!assets.parts.TryAdd(key,part)){assets.parts.Remove(key);assets.ambiguousParts.Add(key);}
        }
        foreach(string line in File.ReadLines(Path.Combine(repositoryRoot,"data","mxoClothing.csv")).Skip(1).Where(s=>!string.IsNullOrWhiteSpace(s)))
        {
            string[] v=line.Split(';');if(v.Length!=6)throw new InvalidDataException("Malformed clothing fixture source");
            uint id=uint.Parse(v[0],CultureInfo.InvariantCulture);
            if(!assets.clothes.TryAdd(id,new(id,v[1],v[2],uint.Parse(v[4],CultureInfo.InvariantCulture),uint.Parse(v[5],CultureInfo.InvariantCulture))))throw new InvalidDataException("Ambiguous clothing fixture source");
        }
        return assets;
    }
    public bool TryResolve(CharacterCreation.Request request,out CharacterCreation.InitialState state)=>CharacterCreation.TryResolve(request,
        (type,id)=>parts.TryGetValue((type,id),out var p)?p:null!,id=>clothes.TryGetValue(id,out var item)?item:default,out state);
}
