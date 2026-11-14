using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace hds
{
    public static partial class NavMesh
    {
        /// <summary>Copied immutable raw world surfaces for the isolated open-floor resolver. Does not use Project,
        /// inset/slack, projection caches or mutate NAV records. Requested sectors bound the snapshot.</summary>
        internal static bool TryOpenFloorSurfaces(ushort district,IEnumerable<ushort> requested,
            out IReadOnlyList<MissionOpenFloorResolver.Surface> surfaces,out string refusal)
        {
            surfaces=Array.AsReadOnly(Array.Empty<MissionOpenFloorResolver.Surface>());refusal=null;
            if(requested==null) {refusal="MissingSectorScope";return false;}
            ushort[] ids=requested.Distinct().OrderBy(x=>x).ToArray();
            if(ids.Length==0) {refusal="MissingSectorScope";return false;}
            try
            {
                string path=Path.Combine(directory??DataLoader.DataPath("navmesh"),district+".bin.gz");
                // Existing shared Load owns body decoding. Independently admit its file identity;
                // do not alter that legacy reader's behavior or expose mutable private polygons.
                using(var file=File.OpenRead(path))using(var gz=new GZipStream(file,CompressionMode.Decompress))using(var r=new BinaryReader(gz))
                {
                    uint magic=r.ReadUInt32();ushort version=r.ReadUInt16(),storedDistrict=r.ReadUInt16();
                    if(magic!=0x564e584d||(version!=2&&version!=3)||storedDistrict!=district)
                    {refusal="WrongNavIdentity";return false;}
                }
                District data=Load(district);var result=new List<MissionOpenFloorResolver.Surface>();
                foreach(ushort id in ids)
                {
                    SectorInfo info=SectorProperties.Get(district,id);
                    if(info==null||!data.sectors.TryGetValue(id,out Sector sector)) {refusal="MissingSectorOrOrigin";return false;}
                    void Add(Polygon polygon,int region,int index,double x,double y,double z,int rotation)
                    {
                        if(polygon.xz.Length<6)return;
                        if(!double.IsFinite(polygon.a)||!double.IsFinite(polygon.b)||!double.IsFinite(polygon.c)||!double.IsFinite(polygon.d)||polygon.b==0||rotation<0||rotation>3)
                            throw new InvalidDataException("Invalid open-floor surface transform");
                        var points=new List<MissionOpenFloorResolver.Point>();
                        for(int i=0;i<polygon.xz.Length;i+=2)
                        {
                            double px=polygon.xz[i],pz=polygon.xz[i+1];
                            points.Add(new MissionOpenFloorResolver.Point(x+(rotation==1?pz:rotation==2?-px:rotation==3?-pz:px),
                                z+(rotation==1?-px:rotation==2?-pz:rotation==3?px:pz)));
                        }
                        double area=MissionOpenFloorResolver.Area(points);if(Math.Abs(area)<=MissionOpenFloorResolver.Epsilon)return;
                        if(area<0)points.Reverse();
                        double fx=(double)(rotation==1?-polygon.c:rotation==2?polygon.a:rotation==3?polygon.c:-polygon.a)/polygon.b;
                        double fz=(double)(rotation==1?polygon.a:rotation==2?polygon.c:rotation==3?-polygon.a:-polygon.c)/polygon.b;
                        result.Add(new MissionOpenFloorResolver.Surface(new MissionOpenFloorResolver.Id(id,checked((ushort)region),checked((ushort)index)),
                            points,fx,fz,y-(double)polygon.d/polygon.b-fx*x-fz*z));
                    }
                    for(int g=0;g<sector.regions.Length;g++)for(int p=0;p<sector.regions[g].Count;p++)
                        Add(sector.regions[g][p],g,p,info.minX,0,info.minZ,0);
                    for(int k=0;k<sector.placements.Length;k++)
                    {
                        Placement pl=sector.placements[k];
                        if(pl.prefab<0||pl.prefab>=data.prefabs.Length||pl.rotation<0||pl.rotation>3)throw new InvalidDataException("Missing open-floor NAV prefab");
                        for(int p=0;p<data.prefabs[pl.prefab].Count;p++)
                            Add(data.prefabs[pl.prefab][p],sector.regions.Length+k,p,info.minX+pl.pivotX,pl.pivotY,info.minZ+pl.pivotZ,pl.rotation);
                    }
                }
                surfaces=Array.AsReadOnly(result.ToArray());return true;
            }
            catch(Exception ex) when(ex is IOException || ex is InvalidDataException || ex is IndexOutOfRangeException || ex is OverflowException)
            {refusal="InvalidOrUnavailableNav";return false;}
        }
    }
}
