using System;
using System.Linq;
using System.Text.Json.Serialization;
using G=hds.MissionOpenFloorResolver;
namespace hds
{
    // Persisted alongside the raw site/source DTO. Never used to rewrite those identities.
    public sealed class MissionElevatorWitness
    {
        public int Version {get;} public string SiteFingerprint {get;} public ushort Region {get;} public ushort LobbyRegion {get;} public uint FloorKey {get;} public uint LobbyKey {get;}
        private readonly double[] floor,lobby;private readonly string[] sources;
        public double[] Floor =>(double[])floor?.Clone(); public double[] Lobby =>(double[])lobby?.Clone(); public string FloorQuaternion {get;} public string LobbyQuaternion {get;}
        public string Evidence {get;} public string[] Sources =>(string[])sources?.Clone();
        [JsonConstructor] public MissionElevatorWitness(int version,string siteFingerprint,ushort region,double[] floor,double[] lobby,string floorQuaternion,string lobbyQuaternion,string evidence,string[] sources,uint floorKey=0,uint lobbyKey=0,ushort lobbyRegion=0)
        {Version=version;LobbyRegion=lobbyRegion;FloorKey=floorKey;LobbyKey=lobbyKey;SiteFingerprint=siteFingerprint;Region=region;this.floor=(double[])floor?.Clone();this.lobby=(double[])lobby?.Clone();FloorQuaternion=floorQuaternion;LobbyQuaternion=lobbyQuaternion;Evidence=evidence;this.sources=(string[])sources?.Clone();}
    }
    internal static class MissionElevatorPoses
    {
        internal static bool TryCreate(MissionSite site,MissionOpenFloorChoice[] choices,out MissionElevatorWitness witness,out string refusal,MissionElevatorAnchors.Dataset dataset=null)
        {
            witness=null;
            if(!TrySelectionContext(site,out var context,out refusal,dataset))return false;
            if(context==null)return true;
            if(choices==null||choices.Length==0){refusal="ElevatorMissingIndependentSourceRegion";return false;}
            foreach(var choice in choices)
                if(!CompatibleSource(site,choice?.ReadSpot(),context,out refusal,dataset))return false;
            witness=new MissionElevatorWitness(context.Version,context.SiteFingerprint,context.Region,context.Floor,context.Lobby,
                context.FloorQuaternion,context.LobbyQuaternion,context.Evidence,choices.Select(c=>c.RawSpotJson).ToArray(),context.FloorKey,context.LobbyKey,context.LobbyRegion);
            return true;
        }
        internal static bool CompatibleSource(MissionSite site,StaticWorldObject raw,MissionElevatorWitness context,out string refusal,MissionElevatorAnchors.Dataset dataset=null)
        {
            refusal="ElevatorSourceRegionDisagreement";
            if(raw==null||context==null)return false;
            var state=new MissionObjectState{spot=raw,goid=MissionSites.Goid(raw)};
            if(MissionBoundObjectPoses.Supported(state))
            {
                if(!MissionBoundObjectPoses.TryCurrentRegion(state,out _,out int region,out refusal))return false;
                if(region!=context.Region){refusal="ElevatorBoundSourceRegionDisagreement";return false;}
            }
            else
            {
                var load=MissionElevatorAnchors.Current(site.elevatorDoor,dataset);
                if(load.Kind!=MissionElevatorAnchors.LoadKind.Available){refusal=load.Refusal??"UnavailableElevatorSourceRegion";return false;}
                if(!load.Index.SourceRegion(raw,context.Region))return false;
            }
            refusal=null;return true;
        }
        internal static bool TrySelectionContext(MissionSite site,out MissionElevatorWitness witness,out string refusal,MissionElevatorAnchors.Dataset dataset=null)
        {
            witness=null;refusal=null;if(site?.elevatorDoor==null||site.lobbyDoor==null)return true;
            var raw=site.elevatorDoor;
            var load=MissionElevatorAnchors.Current(raw,dataset);
            if(load.Kind==MissionElevatorAnchors.LoadKind.Unavailable){refusal=load.Refusal;return false;}
            var index=load.Index;if(load.Kind==MissionElevatorAnchors.LoadKind.Undeclared||!index.HasDoor(raw))return true;
            if(!index.NeedsCorrection(raw,out bool changed,out refusal))return false;
            if(!changed)return true;
            var legacy=MissionOpenFloorEntry.Calculate(site.metrId,new G.Position(raw.pos_x,raw.pos_y,raw.pos_z),site.CentreX,site.CentreZ,"legacy entry used only for original-region association");
            if(legacy.Root==null){refusal=legacy.Refusal;return false;}
            if(legacy.Root.Surface.Sector!=raw.sectorID){refusal="ElevatorEntrySectorDisagreement";return false;}
            if(!index.TryResolve(raw,legacy.Root.Surface.Region,out var floor,out refusal))return false;
            // Known-working coincident locations retain their current yaw and every legacy consumer.
            if(raw.pos_x==floor.X&&raw.pos_y==floor.Y+145&&raw.pos_z==floor.Z)return true;
            var entry=MissionOpenFloorEntry.Calculate(site.metrId,floor.Presentation,site.CentreX,site.CentreZ,floor.Evidence);
            if(entry.Root==null||entry.Root.Surface.Sector!=legacy.Root.Surface.Sector||entry.Root.Surface.Region!=legacy.Root.Surface.Region)
            {refusal="ElevatorResolvedEntryRegionDisagreement";return false;}
            // Independently locate and resolve the selected lobby record. No inferred floor delta.
            var lobbyRaw=site.lobbyDoor;
            var lobbyLoad=MissionElevatorAnchors.Current(lobbyRaw,dataset);
            if(lobbyLoad.Kind==MissionElevatorAnchors.LoadKind.Unavailable){refusal=lobbyLoad.Refusal;return false;}
            var lobbyIndex=lobbyLoad.Index;
            if(lobbyLoad.Kind!=MissionElevatorAnchors.LoadKind.Available||!lobbyIndex.TryPosition(lobbyRaw,out var lobbyNative,out refusal))
            {refusal=refusal??"ElevatorLobbyPositionUnavailable";return false;}
            var lobbyPoint=new G.Position(lobbyNative.X,lobbyNative.Y+145,lobbyNative.Z);
            var lobbyEntry=MissionOpenFloorEntry.Calculate(site.metrId,lobbyPoint,site.CentreX,site.CentreZ,"independent original lobby position consensus");
            if(lobbyEntry.Root==null){refusal=lobbyEntry.Refusal;return false;}
            if(lobbyEntry.Root.Surface.Sector!=lobbyRaw.sectorID){refusal="ElevatorLobbySectorDisagreement";return false;}
            if(!lobbyIndex.TryResolve(lobbyRaw,lobbyEntry.Root.Surface.Region,out var lobby,out refusal))return false;
            var checkedLobby=MissionOpenFloorEntry.Calculate(site.metrId,lobby.Presentation,site.CentreX,site.CentreZ,lobby.Evidence);
            if(checkedLobby.Root==null||!checkedLobby.Root.Surface.Equals(lobbyEntry.Root.Surface)||!checkedLobby.Published.Equals(lobbyEntry.Published))
            {refusal="ElevatorResolvedLobbyRegionDisagreement";return false;}
            witness=new MissionElevatorWitness(1,MissionOpenFloorSite.CatalogFingerprint(site),floor.Region,
                new[]{floor.X,floor.Y+145,floor.Z},new[]{lobby.X,lobby.Y+145,lobby.Z},floor.Quaternion,lobby.Quaternion,
                floor.Evidence+";"+lobby.Evidence,Array.Empty<string>(),raw.mxoStaticId,lobbyRaw.mxoStaticId,lobby.Region);return true;
        }
        internal enum OverlayKind { Legacy,Resolved,Refused }
        internal sealed class Overlay
        {
            internal readonly OverlayKind Kind;internal readonly G.Position Position;internal readonly string Quaternion,Refusal;
            internal Overlay(OverlayKind kind,G.Position position=default,string quaternion=null,string refusal=null){Kind=kind;Position=position;Quaternion=quaternion;Refusal=refusal;}
        }
        // Caller holds the captured owner's missionSync through snapshot, publication and OnElevator.
        internal static Overlay ResolveOverlay(MissionProgress progress,StaticWorldObject raw,MissionElevatorAnchors.Dataset dataset=null)
        {
            if(progress==null||raw==null)return new Overlay(OverlayKind.Legacy);
            var npc=MissionNpcInteriorAdmission.Overlay(progress,raw);if(npc!=null)return npc;
            Overlay resolved=null;
            foreach(var site in progress.sites)
            {
                if(raw.metrId!=site.metrId)continue;
                bool floor=raw.mxoStaticId==site.elevatorDoor?.mxoStaticId,lobby=raw.mxoStaticId==site.lobbyDoor?.mxoStaticId;
                if(!floor&&!lobby)continue;
                var selected=floor?site.elevatorDoor:site.lobbyDoor;var witness=progress.ElevatorFor(site);
                bool adoptedCurrent=ReferenceEquals(site,progress.site)&&(progress.openFloorResolvedPhase==progress.phaseIndex||progress.objects.Values.Any(o=>o.OpenFloor!=null));
                bool witnessedHistory=progress.sites.Any(s=>s.metrId==raw.metrId&&(s.elevatorDoor?.mxoStaticId==raw.mxoStaticId||s.lobbyDoor?.mxoStaticId==raw.mxoStaticId)&&progress.ElevatorFor(s)!=null);
                if(witness==null&&!adoptedCurrent&&!witnessedHistory)continue; // No bound-only V1 migration.
                var load=MissionElevatorAnchors.Current(selected,dataset);
                if(load.Kind==MissionElevatorAnchors.LoadKind.Unavailable)return new Overlay(OverlayKind.Refused,refusal:load.Refusal);
                var index=load.Index;bool known=load.Kind==MissionElevatorAnchors.LoadKind.Available&&index.HasDoor(selected);
                bool changed=false;string refusal=null;
                if(known&&!index.NeedsCorrection(selected,out changed,out refusal))
                    return new Overlay(OverlayKind.Refused,refusal:refusal);
                if(witness==null&&!changed)continue; // Unchanged/unrelated legacy behavior is preserved.
                if(witness==null||progress.restoreRefusal!=null||!known)
                    return new Overlay(OverlayKind.Refused,refusal:"MissingOrQuarantinedMissionElevatorWitness");
                if(witness.Version!=1||witness.SiteFingerprint!=MissionOpenFloorSite.CatalogFingerprint(site)||
                    witness.FloorKey!=site.elevatorDoor.mxoStaticId||witness.LobbyKey!=site.lobbyDoor.mxoStaticId||
                    !index.TryResolve(raw,floor?witness.Region:witness.LobbyRegion,out var pose,out refusal))
                    return new Overlay(OverlayKind.Refused,refusal:refusal??"InvalidMissionElevatorWitnessIdentity");
                var expected=floor?witness.Floor:witness.Lobby;string q=floor?witness.FloorQuaternion:witness.LobbyQuaternion;
                if(expected?.Length!=3||expected.Any(v=>!double.IsFinite(v))||expected[0]!=pose.X||expected[1]!=pose.Y+145||expected[2]!=pose.Z||!string.Equals(q,pose.Quaternion,StringComparison.OrdinalIgnoreCase))
                    return new Overlay(OverlayKind.Refused,refusal:"MissionElevatorWitnessPoseDisagreement");
                var candidate=new Overlay(OverlayKind.Resolved,Position(expected),q);
                if(resolved!=null&&(!resolved.Position.Equals(candidate.Position)||!string.Equals(resolved.Quaternion,candidate.Quaternion,StringComparison.OrdinalIgnoreCase)))
                    return new Overlay(OverlayKind.Refused,refusal:"ConflictingMissionElevatorHistory");
                resolved=candidate;
            }
            return resolved??new Overlay(OverlayKind.Legacy);
        }
        internal static Overlay WithDoorOwner(WorldClient client,StaticWorldObject raw,Action<Overlay> publish,MissionElevatorAnchors.Dataset dataset=null)
        {
            return WithCapturedDoorOwner(client,client?.playerData,raw,publish,dataset);
        }
        internal static Overlay WithCapturedDoorOwner(WorldClient client,ClientData owner,StaticWorldObject raw,Action<Overlay> publish,MissionElevatorAnchors.Dataset dataset=null)
        {
            if(owner==null)return new Overlay(OverlayKind.Refused,refusal:"MissingMissionDoorOwner");
            lock(owner.missionSync)
            {
                if(!ReferenceEquals(client.playerData,owner))return new Overlay(OverlayKind.Refused,refusal:"ChangedMissionDoorOwner");
                var progress=owner.missionProgress;var snapshot=ResolveOverlay(progress,raw,dataset);
                if(!ReferenceEquals(client.playerData,owner)||!ReferenceEquals(owner.missionProgress,progress))return new Overlay(OverlayKind.Refused,refusal:"ChangedMissionDoorProgress");
                if(snapshot.Kind!=OverlayKind.Refused&&MissionNpcInteriorAdmission.OwnsDoor(progress,raw))
                {
                    var stamp=new MissionRideStamp(client,progress);
                    if(!stamp.Current)return new Overlay(OverlayKind.Refused,refusal:"StaleNpcDoorLifetime");
                    if(MissionOutput.Buffered(client.messageQueue))
                    {
                        if(!stamp.BindOutput())return new Overlay(OverlayKind.Refused,refusal:"StaleNpcDoorOutput");
                        publish(snapshot);stamp.RequireCurrent();return snapshot;
                    }
                    using(var output=MissionOutput.Begin(client.messageQueue))
                    {
                        if(!stamp.BindOutput())return new Overlay(OverlayKind.Refused,refusal:"StaleNpcDoorOutput");
                        publish(snapshot);stamp.RequireCurrent();output.Commit();client.FlushQueue();return snapshot;
                    }
                }
                if(snapshot.Kind!=OverlayKind.Refused)publish(snapshot);
                return snapshot;
            }
        }
        internal static bool InShaft(MissionProgress progress,MissionSite site,StaticWorldObject used)
        {
            if(MissionNpcInteriorAdmission.ForSite(progress,site)!=null)return used!=null&&(site.shaft.Any(door=>door.mxoStaticId==used.mxoStaticId)||InShaft(progress,site,used.pos_x,used.pos_z));
            if(progress.ElevatorFor(site)==null)return MissionSites.InShaft(site,used);
            return used!=null&&(site.shaft.Any(door=>door.mxoStaticId==used.mxoStaticId)||InShaft(progress,site,used.pos_x,used.pos_z));
        }
        internal static bool InShaft(MissionProgress progress,MissionSite site,double x,double z)
        {
            var npc=MissionNpcInteriorAdmission.ForSite(progress,site);
            if(npc!=null)return new[]{npc.Floor.Position,npc.Lobby.Position}.Any(p=>Math.Pow(x-p[0],2)+Math.Pow(z-p[2],2)<=900*900);
            var witness=progress.ElevatorFor(site);if(witness==null)return MissionSites.InShaft(site,x,z);
            // Preserve the existing900-unit threshold, measured from the witnessed doors.
            return new[]{witness.Floor,witness.Lobby}.Any(p=>Math.Pow(x-p[0],2)+Math.Pow(z-p[2],2)<=900*900);
        }
        internal static bool RequiresWitness(MissionSite site,out bool required,out string refusal,MissionElevatorAnchors.Dataset dataset=null)
        {
            required=false;refusal=null;if(site?.elevatorDoor==null)return true;
            var load=MissionElevatorAnchors.Current(site.elevatorDoor,dataset);
            if(load.Kind==MissionElevatorAnchors.LoadKind.Unavailable){refusal=load.Refusal;return false;}
            return load.Kind!=MissionElevatorAnchors.LoadKind.Available||!load.Index.HasDoor(site.elevatorDoor)||load.Index.NeedsCorrection(site.elevatorDoor,out required,out refusal);
        }
        internal static G.Position Floor(MissionSite site,MissionElevatorWitness witness)=>witness==null?new G.Position(site.elevatorDoor.pos_x,site.elevatorDoor.pos_y,site.elevatorDoor.pos_z):Position(witness.Floor);
        internal static G.Position Position(double[] p)=>new G.Position(p[0],p[1],p[2]);
        internal static bool Same(MissionElevatorWitness a,MissionElevatorWitness b)=>System.Text.Json.JsonSerializer.Serialize(a,MissionProgress.SaveJson)==System.Text.Json.JsonSerializer.Serialize(b,MissionProgress.SaveJson);
        internal static bool Validate(MissionSite site,MissionOpenFloorChoice[] choices,MissionElevatorWitness saved,out string refusal,MissionElevatorAnchors.Dataset dataset=null)
        {
            if(!TryCreate(site,choices,out var expected,out refusal,dataset))return false;
            if(!Same(saved,expected)){refusal="ChangedMissionElevatorWitness";return false;}return true;
        }
    }
}
