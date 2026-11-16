using System;

namespace hds
{
    internal enum MissionElevatorDirection { None, Up, Down }

    internal static class MissionElevatorTrips
    {
        // Ordinary static-door requests identify a particular floor. The shaft's horizontal
        // proximity does not authorize hijacking a different floor's door into an up-ride.
        internal static MissionElevatorDirection Choose(MissionSite site, StaticWorldObject used,
            double playerY, bool currentSite, bool waitingForExit, MissionElevatorWitness witness,
            MissionNpcInteriorWitness npc, MissionNpcInteriorEntry streetFloor = null,
            MissionNpcInteriorEntry streetLobby = null)
        {
            if(site?.elevatorDoor==null||site.lobbyDoor==null||!double.IsFinite(playerY))return MissionElevatorDirection.None;
            // The coordinates-only caller is the explicitly enabled ?missionfloor developer command.
            if(used==null)return currentSite&&!waitingForExit?MissionElevatorDirection.Up:MissionElevatorDirection.Down;
            if(used.metrId!=site.elevatorDoor.metrId)return MissionElevatorDirection.None;
            uint floorKey=npc!=null?MissionNpcInteriorPlanning.ReadRaw(npc.Floor.RawDoorJson).mxoStaticId:
                witness?.FloorKey??site.elevatorDoor.mxoStaticId;
            uint lobbyKey=npc!=null?MissionNpcInteriorPlanning.ReadRaw(npc.Lobby.RawDoorJson).mxoStaticId:
                witness?.LobbyKey??site.lobbyDoor.mxoStaticId;
            double floorY=npc?.Floor.Position[1]??witness?.Floor[1]??site.elevatorDoor.pos_y;
            double lobbyY=npc?.Lobby.Position[1]??witness?.Lobby[1]??site.lobbyDoor.pos_y;
            // Trusted saved Street entries take precedence, matching Ride and targetY.
            // This pure chooser does not load coverage or manufacture NPC authority.
            if(streetFloor!=null||streetLobby!=null)
            {
                if(streetFloor==null||streetLobby==null)return MissionElevatorDirection.None;
                var floorRaw=MissionNpcInteriorPlanning.ReadRaw(streetFloor.RawDoorJson);
                var lobbyRaw=MissionNpcInteriorPlanning.ReadRaw(streetLobby.RawDoorJson);
                if(floorRaw.metrId!=site.elevatorDoor.metrId||lobbyRaw.metrId!=site.elevatorDoor.metrId)return MissionElevatorDirection.None;
                floorKey=floorRaw.mxoStaticId;lobbyKey=lobbyRaw.mxoStaticId;
                floorY=streetFloor.Position[1];lobbyY=streetLobby.Position[1];
                if(!double.IsFinite(floorY)||!double.IsFinite(lobbyY))return MissionElevatorDirection.None;
            }
            if(floorY==lobbyY)return MissionElevatorDirection.None;
            if(used.mxoStaticId==floorKey&&Math.Abs(playerY-floorY)<200)return MissionElevatorDirection.Down;
            if(currentSite&&!waitingForExit&&used.mxoStaticId==lobbyKey&&Math.Abs(playerY-lobbyY)<200)
                return MissionElevatorDirection.Up;
            return MissionElevatorDirection.None;
        }
    }
}
