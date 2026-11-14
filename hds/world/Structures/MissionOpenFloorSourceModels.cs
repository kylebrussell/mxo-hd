using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using G=hds.MissionOpenFloorResolver;
namespace hds
{
    internal static class MissionOpenFloorSourceModels
    {
        internal sealed class Entry
        {
            internal G.SourceModelPresence Presence {get;} internal string Evidence {get;}
            internal Entry(G.SourceModelPresence presence,string evidence){Presence=presence;Evidence=evidence;}
        }
        private static readonly Lazy<IReadOnlyDictionary<ushort,Entry>> current=new Lazy<IReadOnlyDictionary<ushort,Entry>>(()=>
        {using(var stream=File.OpenRead(DataLoader.DataPath("mission_open_floor","source_models_1.json")))return Read(stream,1);});
        internal static bool TryGet(ushort world,ushort goid,out G.SourceModelPresence presence,out string evidence)
        {
            presence=G.SourceModelPresence.Unproven;evidence=null;if(world!=1)return false;
            try{if(!current.Value.TryGetValue(goid,out var entry))return false;presence=entry.Presence;evidence=entry.Evidence;return true;}
            catch(Exception ex) when(ex is IOException||ex is JsonException||ex is InvalidOperationException||ex is ArgumentException||ex is KeyNotFoundException||ex is OverflowException){return false;}
        }
        internal static IReadOnlyDictionary<ushort,Entry> Read(Stream stream,ushort world)
        {
            using(var document=JsonDocument.Parse(stream))
            {
                var root=document.RootElement;if(root.GetProperty("format").GetInt32()!=1||root.GetProperty("world").GetUInt16()!=world||world!=1)throw new InvalidDataException("SourceModelIdentity");
                var rows=new Dictionary<ushort,Entry>();
                foreach(var row in root.GetProperty("entries").EnumerateArray())
                {
                    ushort goid=row.GetProperty("goid").GetUInt16();string status=row.GetProperty("status").GetString(),hash=row.GetProperty("recordSha256").GetString();
                    int[] components=row.GetProperty("componentTypes").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
                    if(goid==0||hash==null||hash.Length!=64||!hash.All(Uri.IsHexDigit)||components.Any(x=>x<0||x>ushort.MaxValue)||
                        (status!="Model6Absent"&&status!="AuthoredStaticBounds")||components.Contains(6)!=(status=="AuthoredStaticBounds"))throw new InvalidDataException("InvalidSourceModelEvidence");
                    var presence=status=="Model6Absent"?G.SourceModelPresence.Model6Absent:G.SourceModelPresence.AuthoredStaticBounds;
                    if(!rows.TryAdd(goid,new Entry(presence,"GOB:"+goid+":"+status+":"+hash)))throw new InvalidDataException("DuplicateSourceModel");
                }
                if(rows.Count==0)throw new InvalidDataException("EmptySourceModelCatalog");
                return new ReadOnlyDictionary<ushort,Entry>(rows);
            }
        }
    }
}
