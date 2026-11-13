using System;
using System.Collections.Generic;
using System.Linq;
namespace hds
{
    /// <summary>Only the eight sourced DataMine generic returners. Arrival/relogin fallback remains unchanged.</summary>
    public static class DataMineReturners
    {
        public const ushort GoId = 47267;
        private const ulong Prefix = 0xa700000000000000UL;
        private static readonly uint[] ids = { 0x00800001,0x00800003,0x01400017,0x0170000c,0x01c004ce,0x02400001,0x02900df8,0x02c00002 };
        private static readonly Lazy<IReadOnlyList<StaticWorldObject>> loaded = new Lazy<IReadOnlyList<StaticWorldObject>>(
            () => Validate(DataLoader.getInstance().WorldObjectsDB));
        public static IReadOnlyList<StaticWorldObject> Bindings => loaded.Value;
        public static ulong Entity(StaticWorldObject s) => Prefix | s.mxoStaticId;
        // Refuse a partial/duplicate/unsupported set. Existing generated fallback remains available even on refusal.
        public static IReadOnlyList<StaticWorldObject> Validate(IEnumerable<StaticWorldObject> source)
        {
            var objects=source.Where(s=>s.metrId==7 && MissionSites.Goid(s)==GoId).ToArray();
            if(objects.Length!=8 || !objects.Select(s=>s.mxoStaticId).OrderBy(x=>x).SequenceEqual(ids.OrderBy(x=>x)) ||
                typeof(GameObjectDefinitions).GetField("Object47267")?.FieldType != typeof(AttributeClass3602))return Array.Empty<StaticWorldObject>();
            foreach(var s in objects)
                if(!s.exterior || !double.IsFinite(s.pos_x) || !double.IsFinite(s.pos_y) || !double.IsFinite(s.pos_z) || s.mxoStaticId != ((uint)s.sectorID<<20|s.staticId) ||
                    !NavMesh.Locate(7,s.pos_x,s.pos_y-50,s.pos_z,out var at) || Math.Abs(at.floorY-(s.pos_y-145))>10)
                    return Array.Empty<StaticWorldObject>();
            return Array.AsReadOnly(objects);
        }
        public static bool CanUseView(ClientView view,ClientView current,IReadOnlyList<StaticWorldObject> bindings,
            uint map,bool onWorld,double x,double y,double z)
        {
            if(map!=7 || !onWorld || view==null || !ReferenceEquals(view,current) || !view.viewCreated || view.viewNeedsToBeDeleted || view.GoID!=GoId)return false;
            var source=bindings.FirstOrDefault(s=>ReferenceEquals(s,view.staticObject) && view.entityId==Entity(s));
            if(source==null)return false;
            double dx=x-source.pos_x,dy=y-source.pos_y,dz=z-source.pos_z;
            return double.IsFinite(dx) && double.IsFinite(dy) && double.IsFinite(dz) && dx*dx+dy*dy+dz*dz<=1000*1000;
        }
    }
}
