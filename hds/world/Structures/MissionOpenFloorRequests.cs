using System;
using System.Linq;
using G=hds.MissionOpenFloorResolver;
namespace hds
{
    internal static class MissionOpenFloorRequests
    {
        internal static bool TryCreate(StaticWorldObject raw,ushort concrete,MissionSite site,ushort world,out G.Request request,out string refusal,MissionPhase phase=null,ushort objectId=0)
        {
            request=null;refusal=null;
            if(!MissionOpenFloorAdmission.ValidSpot(raw)||!MissionOpenFloorAdmission.ValidSite(site)||raw.metrId!=world||site.metrId!=world||!MissionOpenFloorAdmission.Target(concrete,raw))
            {refusal="InvalidOpenFloorSourceOrSite";return false;}
            if(world!=1){refusal="UnsupportedOpenFloorWorld";return false;}
            if(!MissionOpenFloorAnchors.TryCurrent(raw,out var anchor,out string anchorEvidence)){refusal="MissingOrAmbiguousAuditedAnchor";return false;}
            if(!MissionOpenFloorSourceModels.TryGet(world,MissionSites.Goid(raw),out var presence,out string modelEvidence)){refusal="MissingSourceModelEvidence";return false;}
            if(!MissionOpenFloorProfiles.TryModel(concrete,out var model)||!MissionOpenFloorProfiles.TryUsable(concrete,out var usable)){refusal="UnprovenProxyProfile";return false;}
            if(concrete==3)
            {
                if(!TryComputerActions(phase,objectId,out byte[] actions)){refusal="UnprovenComputerActions";return false;}
                usable=MissionOpenFloorUsableRange.Profile.InitialComputerActions(actions,usable.HalfExtents,"GO3 Usable5 Hack4/Get5/Put8 stored offsets399/387/606 beyond327; zero-init648; native initial configurable fallback400; "+
                    (phase.missionObjectives.Any(o=>o.objectId==objectId)?"actions required by existing phase goals":"no direct goal: sourced all3 geometric owner profile only, no offered actions inferred"));
            }
            var entry=MissionOpenFloorEntry.Calculate(world,new G.Position(site.elevatorDoor.pos_x,site.elevatorDoor.pos_y,site.elevatorDoor.pos_z),site.CentreX,site.CentreZ,"unchanged normal Ride entry at exact selected site");
            if(entry.Root==null){refusal=entry.Refusal;return false;}
            if(!MissionOpenFloorScenes.TryLoad(world,raw.sectorID,out var scene,out refusal))return false;
            ushort[] sectors=new[]{raw.sectorID,entry.Root.Surface.Sector}.Distinct().OrderBy(s=>s).ToArray();
            if(!NavMesh.TryOpenFloorSurfaces(world,sectors,out var surfaces,out refusal))return false;
            request=new G.Request(world,presence,modelEvidence,new G.RawPose(new G.Position(raw.pos_x,raw.pos_y,raw.pos_z),raw.quat,raw.mxoStaticId,MissionSites.Goid(raw)),
                new G.RawPose(new G.Position(anchor.X,anchor.Y,anchor.Z),Convert.ToHexString(anchor.Orientation),raw.mxoStaticId,MissionSites.Goid(raw)),anchorEvidence,
                model,usable,surfaces,entry.Root,new G.Bounds(site.minX,site.maxX,site.minZ,site.maxZ),scene);
            return true;
        }
        internal static bool TryComputerActions(MissionPhase phase,ushort objectId,out byte[] actions)
        {
            actions=Array.Empty<byte>();if(phase==null||objectId==0)return false;
            var objectives=phase.missionObjectives.Where(o=>o.objectId==objectId).ToArray();
            if(objectives.Length==0){actions=new byte[]{4,5,8};return true;} // Geometric owner profile only; no native/server offers are added.
            if(objectives.Any(o=>o.type!=MissionGoalType.Put&&o.type!=MissionGoalType.Search&&o.type!=MissionGoalType.Use))return false;
            actions=objectives.Select(o=>o.type==MissionGoalType.Put?(byte)8:o.type==MissionGoalType.Search&&o.item!=0?(byte)5:(byte)4).Distinct().OrderBy(x=>x).ToArray();return true;
        }
    }
}
