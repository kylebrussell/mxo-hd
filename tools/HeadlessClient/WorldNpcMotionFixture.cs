using System.Globalization;
using hds;

namespace HeadlessClient;

public static class WorldNpcMotionFixture
{
    public sealed record ChaseCase(string Name,byte Level,string Rsi,double[] Origin,NavPoint Start);
    private sealed record Source(string Name,byte Level,string Rsi,double[] At,uint Weapon);
    public static ChaseCase Chase(string root)
    {
        SectorProperties.Load(Path.Combine(root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(root,"data/navmesh"));
        double[] anchor={24032,495,-3315};
        var sources=File.ReadLines(Path.Combine(root,"data/npc_spawns/OpenWorld.csv")).Select(l=>l.Split(';'))
            .Where(f=>f.Length>21&&f[0]=="1"&&byte.TryParse(f[3],out var level)&&level<=6&&
                MobBehaviour.Classify(f[2],f[21])==MobAggroKind.WithinLevelLimits)
            .Select(f=>new Source(f[2],byte.Parse(f[3]),f[6],new[]{7,8,9}.Select(i=>double.Parse(f[i],CultureInfo.InvariantCulture)).ToArray(),uint.TryParse(f[20],out var weapon)?weapon:0))
            .Where(n=>Math.Abs(n.At[1]-anchor[1])<=150&&Math.Abs(n.At[0]-anchor[0])<6000&&Math.Abs(n.At[2]-anchor[2])<6000).ToArray();
        foreach(double radius in new[]{900d,1100,1400,1800,2200,2800,800})for(int step=0;step<16;step++)
        {
            double angle=-Math.PI/2+step*Math.PI/8;
            if(!NavMesh.Project(1,anchor[0]+radius*Math.Cos(angle),anchor[1],anchor[2]+radius*Math.Sin(angle),out var at,
                maxDistance:25,reachableFrom:anchor,requireDirect:true)||at.location.indoors)continue;
            double Distance(Source s)=>Math.Sqrt(Math.Pow(at.x-s.At[0],2)+Math.Pow(at.z-s.At[2],2));
            var nearest=sources.OrderBy(Distance).FirstOrDefault();
            if(nearest==null||Distance(nearest)<600||Distance(nearest)>MobAggro.RangeUnits||RangedWeapons.IsGun(nearest.Weapon)||
                !NavMesh.Locate(1,nearest.At[0],nearest.At[1],nearest.At[2],out var npc)||npc.indoors||
                !NavMesh.HasSightPath(1,nearest.At[0],nearest.At[1],nearest.At[2],at.x,at.y,at.z))continue;
            return new(nearest.Name,nearest.Level,nearest.Rsi,nearest.At,at);
        }
        throw new InvalidDataException("No isolated walkable melee chase fixture near the Blackwoods");
    }
}
