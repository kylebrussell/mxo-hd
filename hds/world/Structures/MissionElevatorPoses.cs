using System;
using System.Linq;
using System.Text.Json.Serialization;
using G=hds.MissionOpenFloorResolver;
namespace hds
{
    // Persisted alongside the raw site/source DTO. Never used to rewrite those identities.
    public sealed class MissionElevatorWitness
    {
        public int Version {get;} public string SiteFingerprint {get;} public ushort Region {get;} public uint FloorKey {get;} public uint LobbyKey {get;}
        private readonly double[] floor,lobby;private readonly string[] sources;
        public double[] Floor =>(double[])floor?.Clone(); public double[] Lobby =>(double[])lobby?.Clone(); public string FloorQuaternion {get;} public string LobbyQuaternion {get;}
        public string Evidence {get;} public string[] Sources =>(string[])sources?.Clone();
        [JsonConstructor] public MissionElevatorWitness(int version,string siteFingerprint,ushort region,double[] floor,double[] lobby,string floorQuaternion,string lobbyQuaternion,string evidence,string[] sources,uint floorKey=0,uint lobbyKey=0)
        {Version=version;FloorKey=floorKey;LobbyKey=lobbyKey;SiteFingerprint=siteFingerprint;Region=region;this.floor=(double[])floor?.Clone();this.lobby=(double[])lobby?.Clone();FloorQuaternion=floorQuaternion;LobbyQuaternion=lobbyQuaternion;Evidence=evidence;this.sources=(string[])sources?.Clone();}
    }
    internal static class MissionElevatorPoses
    {
        internal static bool TryCreate(MissionSite site,MissionOpenFloorChoice[] choices,out MissionElevatorWitness witness,out string refusal)
        {
            witness=null;refusal=null;if(site?.elevatorDoor==null||site.lobbyDoor==null)return true;
            var raw=site.elevatorDoor;
            if(!MissionElevatorAnchors.TryCurrent(raw,out var index)||!index.HasDoor(raw))return true;
            if(!index.NeedsCorrection(raw,out bool changed,out refusal))return false;
            if(!changed)return true;
            var legacy=MissionOpenFloorEntry.Calculate(site.metrId,new G.Position(raw.pos_x,raw.pos_y,raw.pos_z),site.CentreX,site.CentreZ,"legacy entry used only for original-region association");
            if(legacy.Root==null){refusal=legacy.Refusal;return false;}
            if(legacy.Root.Surface.Sector!=raw.sectorID){refusal="ElevatorEntrySectorDisagreement";return false;}
            if(!index.TryResolve(raw,legacy.Root.Surface.Region,out var floor,out refusal))return false;
            // Known-working coincident locations retain their current yaw and every legacy consumer.
            if(raw.pos_x==floor.X&&raw.pos_y==floor.Y+145&&raw.pos_z==floor.Z)return true;
            if(choices==null||choices.Length==0){refusal="ElevatorMissingIndependentSourceRegion";return false;}
            foreach(var choice in choices)
            {
                var source=choice?.ReadSpot();
                if(source==null||!index.SourceRegion(source,legacy.Root.Surface.Region))
                {refusal="ElevatorSourceRegionDisagreement";return false;}
            }
            var entry=MissionOpenFloorEntry.Calculate(site.metrId,floor.Presentation,site.CentreX,site.CentreZ,floor.Evidence);
            if(entry.Root==null||entry.Root.Surface.Sector!=legacy.Root.Surface.Sector||entry.Root.Surface.Region!=legacy.Root.Surface.Region)
            {refusal="ElevatorResolvedEntryRegionDisagreement";return false;}
            // Independently locate and resolve the selected lobby record. No inferred floor delta.
            var lobbyRaw=site.lobbyDoor;
            if(!MissionElevatorAnchors.TryCurrent(lobbyRaw,out var lobbyIndex)||!lobbyIndex.TryPosition(lobbyRaw,out var lobbyNative,out refusal))
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
                floor.Evidence+";"+lobby.Evidence,choices.Select(c=>c.RawSpotJson).ToArray(),raw.mxoStaticId,lobbyRaw.mxoStaticId);return true;
        }
        internal static bool TryOverlay(MissionProgress progress,StaticWorldObject raw,out G.Position position,out string quaternion)
        {
            position=default;quaternion=null;if(progress==null||progress.restoreRefusal!=null||raw==null)return false;
            foreach(var site in progress.sites.AsEnumerable().Reverse())
            {
                var witness=progress.ElevatorFor(site);if(witness==null||raw.metrId!=site.metrId||
                    !MissionElevatorAnchors.TryCurrent(raw,out var index)||!index.HasDoor(raw)||!index.NeedsCorrection(raw,out _,out _))continue;
                if(raw.mxoStaticId==site.elevatorDoor.mxoStaticId){position=Position(witness.Floor);quaternion=witness.FloorQuaternion;return true;}
                if(raw.mxoStaticId==site.lobbyDoor.mxoStaticId){position=Position(witness.Lobby);quaternion=witness.LobbyQuaternion;return true;}
            }
            return false;
        }
        internal static bool InShaft(MissionProgress progress,MissionSite site,StaticWorldObject used)
        {
            if(progress.ElevatorFor(site)==null)return MissionSites.InShaft(site,used);
            return used!=null&&(site.shaft.Any(door=>door.mxoStaticId==used.mxoStaticId)||InShaft(progress,site,used.pos_x,used.pos_z));
        }
        internal static bool InShaft(MissionProgress progress,MissionSite site,double x,double z)
        {
            var witness=progress.ElevatorFor(site);if(witness==null)return MissionSites.InShaft(site,x,z);
            // Preserve the existing900-unit threshold, measured from the witnessed doors.
            return new[]{witness.Floor,witness.Lobby}.Any(p=>Math.Pow(x-p[0],2)+Math.Pow(z-p[2],2)<=900*900);
        }
        internal static G.Position Floor(MissionSite site,MissionElevatorWitness witness)=>witness==null?new G.Position(site.elevatorDoor.pos_x,site.elevatorDoor.pos_y,site.elevatorDoor.pos_z):Position(witness.Floor);
        internal static G.Position Position(double[] p)=>new G.Position(p[0],p[1],p[2]);
        internal static bool Same(MissionElevatorWitness a,MissionElevatorWitness b)=>System.Text.Json.JsonSerializer.Serialize(a,MissionProgress.SaveJson)==System.Text.Json.JsonSerializer.Serialize(b,MissionProgress.SaveJson);
        internal static bool Validate(MissionSite site,MissionOpenFloorChoice[] choices,MissionElevatorWitness saved,out string refusal)
        {
            if(!TryCreate(site,choices,out var expected,out refusal))return false;
            if(!Same(saved,expected)){refusal="ChangedMissionElevatorWitness";return false;}return true;
        }
    }
}
