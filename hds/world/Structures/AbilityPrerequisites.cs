using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace hds
{
    /// <summary>Pure shipped load graph. No admission, mutation, implicit loading or grant expansion.</summary>
    public static class AbilityPrerequisites
    {
        public sealed class Entry
        {
            public ushort Ability { get; }
            public byte CharacterLevel { get; }
            public IReadOnlyDictionary<ushort,int> Direct { get; }
            internal Entry(ushort ability,byte characterLevel,Dictionary<ushort,int> direct)
            { Ability=ability;CharacterLevel=characterLevel;Direct=new ReadOnlyDictionary<ushort,int>(direct); }
        }
        public sealed class Graph
        {
            public IReadOnlyDictionary<ushort,Entry> All { get; }
            internal Graph(Dictionary<ushort,Entry> entries) { All=new ReadOnlyDictionary<ushort,Entry>(entries); }
            public Entry Find(ushort ability) => All.TryGetValue(AbilityCatalog.Canonical(ability),out var entry)?entry:null;
            /// <summary>Maximum required level per canonical ID; visited nodes make cycles finite. Unknown is not admission.</summary>
            public IReadOnlyDictionary<ushort,int> Closure(ushort ability)
            {
                var result=new SortedDictionary<ushort,int>();var pending=new Queue<ushort>();var visited=new HashSet<ushort>();
                pending.Enqueue(AbilityCatalog.Canonical(ability));
                while(pending.Count!=0)
                {
                    ushort id=pending.Dequeue();if(!visited.Add(id))continue;
                    var entry=Find(id);if(entry==null)continue;
                    foreach(var requirement in entry.Direct)
                    {
                        if(!result.TryGetValue(requirement.Key,out int old)||requirement.Value>old)result[requirement.Key]=requirement.Value;
                        pending.Enqueue(requirement.Key);
                    }
                }
                return new ReadOnlyDictionary<ushort,int>(result);
            }
        }
        private static readonly Lazy<Graph> current=new Lazy<Graph>(()=>Read(DataLoader.DataPath("ability_catalog.json")));
        public static Graph Current => current.Value;
        public static Graph Read(string path) => Parse(File.ReadAllText(path));
        public static Graph Parse(string json)
        {
            using var document=JsonDocument.Parse(json);var root=document.RootElement;
            if(root.GetProperty("schema_version").GetInt32()!=1)throw new InvalidDataException("Ability prerequisite catalog schema");
            var entries=new Dictionary<ushort,Entry>();
            foreach(var row in root.GetProperty("abilities").EnumerateArray())
            {
                ushort id=row.GetProperty("id").GetUInt16();if(id>4095)throw new InvalidDataException("Noncanonical catalog ID");
                var fields=row.GetProperty("fields");var direct=new Dictionary<ushort,int>();
                foreach(var component in row.GetProperty("components").EnumerateArray())
                {
                    if(component.GetProperty("type").GetInt32()!=39)continue;
                    string prefix=component.GetProperty("name").GetString()+"/";
                    if(!fields.TryGetProperty(prefix+"ReqAbilityID",out var value))continue;
                    uint descriptor=value.GetUInt32();
                    //Native ignores code/nonability IDs. Strict catalog family validation is conservative for corrupt data.
                    if((descriptor&0xfc000000)!=0x80000000)continue;
                    ushort required=(ushort)((descriptor>>10)&0xfff);if(required==0)continue;
                    int level=fields.TryGetProperty(prefix+"ReqAbilityLevel",out var stored)?stored.GetInt32():0;
                    if(!direct.TryGetValue(required,out int old)||level>old)direct[required]=level;
                }
                //Native named byte getter starts at zero. No ReqCharLevel descriptor means no positive requirement.
                var character=fields.EnumerateObject().Where(p=>p.Name.EndsWith("/ReqCharLevel",StringComparison.Ordinal)).ToArray();
                if(character.Length>1)throw new InvalidDataException("Ambiguous character prerequisite descriptor");
                byte characterLevel=character.Length==0?(byte)0:character[0].Value.GetByte();
                entries.Add(id,new Entry(id,characterLevel,direct));
            }
            return new Graph(entries);
        }
    }
}
