using System;
using System.Linq;
namespace hds
{
    internal static class MissionStreetGuidance
    {
        private static MissionWaypoint Door(uint key,double[] position)=>new MissionWaypoint{x=position[0],y=position[1],z=position[2],doorHex=BitConverter.ToString(BitConverter.GetBytes(key)).Replace("-","").ToLowerInvariant()};
        private static MissionWaypoint Public(MissionStreetSelection selection)=>Door(selection.Context.Door.Key,selection.Context.Door.Position);
        private static MissionWaypoint Floor(MissionStreetSelection selection)=>Door(selection.Site.DoorKey,selection.Context.Floor.Position);
        private static bool Inside(MissionStreetSelection selection,bool lobby,NavLocation at)
        {
            var entry=lobby?selection.Context.Lobby:selection.Context.Floor;var raw=MissionNpcInteriorPlanning.ReadRaw(entry.RawDoorJson);
            return MissionPublicDoors.CurrentDataset().TryScene(raw.metrId,raw.sectorID,out var evidence,out _,out _)&&evidence.StreetRealm(entry,at,raw.mxoStaticId);
        }
        // Presentation only. Internal floor/exit predicates keep their measured car waypoints.
        internal static MissionWaypoint Project(MissionProgress progress,NavLocation? at)
        {
            if(progress?.restoreRefusal!=null||progress==null||progress.streetWitnesses.Count==0)return null;
            progress.streetWitnesses.TryGetValue(progress.phaseIndex,out var current);
            if(at.HasValue&&current!=null)
            {
                if(Inside(current,false,at.Value)&&current.Context.Floor.Standing[1]!=current.Context.Lobby.Standing[1])return Floor(current);
                if(Inside(current,true,at.Value))return progress.waitingForExit&&progress.HasGeneratedDeparture?Public(current):Floor(current);
            }
            if(at.HasValue)
                foreach(var old in progress.streetWitnesses.Values.Where(s=>s.Phase<progress.phaseIndex).OrderByDescending(s=>s.Phase))
                {
                    if(Inside(old,true,at.Value))return Public(old);
                    if(Inside(old,false,at.Value))return Floor(old);
                }
            return current==null?null:Public(current);
        }
        internal static MissionWaypoint Project(WorldClient client,MissionProgress progress)
        {
            if(progress==null||progress.streetWitnesses.Count==0)return null;
            if(client?.playerInstance==null)return Project(progress,(NavLocation?)null);
            double x=0,y=0,z=0;NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(),ref x,ref y,ref z);
            return Project(progress,NavMesh.Locate((ushort)client.playerData.getDistrictId(),x,y,z,out var at)?(NavLocation?)at:null);
        }
        internal static string Signature(MissionWaypoint w)=>w==null?null:w.doorHex+"|"+w.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+w.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+w.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
        internal static bool Tick(WorldClient client,MissionProgress progress)
        {
            if(progress.streetWitnesses.Count==0)return false;var stamp=new MissionRideStamp(client,progress);if(!stamp.Current)return false;
            MissionWaypoint projected=Project(client,progress);if(projected==null)return false;string signature=Signature(projected);if(signature==progress.streetShownWaypoint)return false;
            if(!stamp.BindOutput())return false;stamp.RequireCurrent();
            client.messageQueue.addRpcMessage(ServerPackets.BuildMissionWaypoint(projected.x,projected.y,projected.z,0,projected.doorHex));progress.streetShownWaypoint=signature;return true;
        }
    }
}
