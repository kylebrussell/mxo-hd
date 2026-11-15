using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
namespace hds
{
    public static class CharacterCreation
    {
        public sealed class Request
        {
            public uint Trait {get;} public IReadOnlyList<uint> Appearance {get;}
            public string FirstName {get;} public string LastName {get;} public string Background {get;}
            internal Request(uint trait,uint[] appearance,string[] text)
            {Trait=trait;Appearance=Array.AsReadOnly((uint[])appearance.Clone());FirstName=text[0];LastName=text[1];Background=text[2];}
        }
        public sealed class InitialState
        {
            public Request Request {get;} public IReadOnlyDictionary<string,int> Rsi {get;}
            public IReadOnlyDictionary<ushort,uint> Clothing {get;}
            internal InitialState(Request request,Dictionary<string,int> rsi,Dictionary<ushort,uint> clothes)
            {Request=request;Rsi=new ReadOnlyDictionary<string,int>(rsi);Clothing=new ReadOnlyDictionary<ushort,uint>(clothes);}
        }
        // Actual native77-byte body; all four offsets/sizes checked BEFORE any persistence operation.
        public static bool TryParse(byte[] body,out Request request)
        {
            request=null;
            if(body==null||body.Length<77||body.Length>4092||body[0]!=0x0c)return false;
            uint trait=BitConverter.ToUInt32(body,65);if(!CharacterPersonality.Valid(trait))return false;
            var appearance=Enumerable.Range(0,16).Select(i=>BitConverter.ToUInt32(body,1+4*i)).ToArray();
            if(appearance.Any(v=>v>ushort.MaxValue))return false; // Existing RSI descriptors are u16; no high-half aliases.
            int end=77;var text=new string[4];int[] maximum={32,32,1024,1024};
            for(int i=0;i<4;i++)
            {
                int offset=BitConverter.ToUInt16(body,69+2*i);
                if(offset!=end||offset+2>body.Length)return false;
                int size=BitConverter.ToUInt16(body,offset);
                if(size<1||size>maximum[i]||offset+2+size>body.Length||body[offset+1+size]!=0)return false;
                for(int p=offset+2;p<offset+1+size;p++)if(body[p]==0)return false;
                text[i]=Encoding.Latin1.GetString(body,offset+2,size-1);end=offset+2+size;
            }
            if(end!=body.Length)return false;
            request=new Request(trait,appearance,text);return true;
        }
        // Literal creation choices from shipped StartingCharacterData.xml (hash pinned in source evidence).
        // Category membership alone must never grant arbitrary known reward clothing during creation.
        private static readonly Dictionary<int,uint[]> StartingClothes=new Dictionary<int,uint[]>{
            [97]=new uint[]{1030},
            [98]=new uint[]{987,989,1003},
            [99]=new uint[]{5907,1246,1203,1206,5918,1243},
            [100]=new uint[]{6727},
            [101]=new uint[]{922,936},
            [102]=new uint[]{6386,1109,1101,6388,1061,1104},
            [104]=new uint[]{1258,1260,1263},
            [1097]=new uint[]{737},
            [1098]=new uint[]{671,704},
            [1099]=new uint[]{842,845,834,835},
            [1100]=new uint[]{726},
            [1101]=new uint[]{5621,5602},
            [1102]=new uint[]{748,772,5667,801,770,5669,5723},
            [1103]=new uint[]{743},
            [1104]=new uint[]{6734,905,908}
        };
        public readonly struct Apparel
        {
            public readonly uint Id,Model,Color;public readonly string Category,Name;
            public Apparel(uint id,string category,string name,uint model,uint color){Id=id;Category=category;Name=name;Model=model;Color=color;}
        }
        internal static bool TryResolve(Request request,DataLoader loader,out InitialState state)=>TryResolve(request,
            (type,id)=>{var records=loader.newRSIItemsDb.Where(p=>p.type==type&&p.newRSIID==id).Take(2).ToArray();return records.Length==1?records[0]:null;},id=>{var item=loader.getItemValues(id);return new Apparel(item.getGoidDecimal(),item.getClothesType(),item.getShortName(),item.getModelId(),item.getColorId());},out state);
        // Pure asset-lookup seam: production supplies the same loaded records; offline tests need no world/timer initialization.
        public static bool TryResolve(Request request,Func<string,ushort,NewRSIItem> part,Func<uint,Apparel> apparel,out InitialState state)
        {
            state=null;if(request==null||part==null||apparel==null)return false;var a=request.Appearance;
            var body=part("BODY",(ushort)a[1]);
            if(body==null||body.type!="BODY"||body.gender>1)return false;
            // Native creation starts HairID0; native pack62686bd3 writes ordinal0 without an asset lookup.
            var hair=a[3]==0?new NewRSIItem{type="HAIR",gender=body.gender,internalId=0}:part("HAIR",(ushort)a[3]);
            var head=part("HEAD",(ushort)a[2]);
            if(body==null||hair==null||head==null||body.type!="BODY"||hair.type!="HAIR"||head.type!="HEAD"||body.gender>1||hair.gender!=body.gender||head.gender!=body.gender)return false;
            if(a[0]>31||a[4]>31||a[5]>7||a[6]>15||a[7]>7)return false; // Existing RSI wire widths, no masked truncation.
            var rsi=new Dictionary<string,int>{["body"]=body.internalId,["sex"]=body.gender,["face"]=head.internalId,["hair"]=hair.internalId,
                ["haircolor"]=(int)a[4],["tattoo"]=(int)a[5],["facialdetail"]=(int)a[6],["facialdetailcolor"]=(int)a[7],["skintone"]=(int)a[0]};
            int[] indices={8,9,10,11,12,13,14,15};ushort[] slots={97,98,99,100,101,102,103,104};
            string[] names={"hat","glasses","shirt","gloves","coat","pants","leggins","shoes"};
            string[] types={"1950892361","2001027401","1750286665","1816609097","2001682761","1397768521","1733050697","2001092937"};
            string[] colorNames={null,"glassescolor","shirtcolor",null,"coatcolor","pantscolor",null,"shoecolor"};
            var clothes=new Dictionary<ushort,uint>();
            for(int i=0;i<indices.Length;i++)
            {
                uint id=a[indices[i]];
                if(id==0){if(i==2||i==5||i==7)return false;rsi[names[i]]=0;if(colorNames[i]!=null)rsi[colorNames[i]]=0;continue;}
                if(!StartingClothes.TryGetValue(body.gender*1000+slots[i],out var choices)||Array.IndexOf(choices,id)<0)return false;
                var item=apparel(id);
                if(item.Id!=id||item.Category!=types[i])return false;
                if(item.Name==null||!item.Name.StartsWith(body.gender==0?"M":"F",StringComparison.OrdinalIgnoreCase))return false;
                rsi[names[i]]=checked((int)item.Model);if(colorNames[i]!=null)rsi[colorNames[i]]=checked((int)item.Color);clothes[slots[i]]=id;
            }
            var widths=new Dictionary<string,int>{["body"]=2,["sex"]=1,["face"]=5,["hair"]=5,["haircolor"]=5,["skintone"]=5,["tattoo"]=3,["facialdetail"]=4,["facialdetailcolor"]=3,
                ["leggins"]=body.gender==0?0:4,["hat"]=6,["glasses"]=5,["shirt"]=5,["gloves"]=body.gender==0?5:6,["coat"]=body.gender==0?6:5,["pants"]=5,["shoes"]=body.gender==0?6:5,
                ["glassescolor"]=4,["shirtcolor"]=6,["coatcolor"]=5,["pantscolor"]=5,["shoecolor"]=4};
            if(rsi.Any(p=>p.Value<0||p.Value>=(1<<widths[p.Key])))return false;
            state=new InitialState(request,rsi,clothes);return true;
        }
    }
}
