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
            MissionNpcInteriorWitness npc)
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
            if(floorY==lobbyY)return MissionElevatorDirection.None;
            if(used.mxoStaticId==floorKey&&Math.Abs(playerY-floorY)<200)return MissionElevatorDirection.Down;
            if(currentSite&&!waitingForExit&&used.mxoStaticId==lobbyKey&&Math.Abs(playerY-lobbyY)<200)
                return MissionElevatorDirection.Up;
            return MissionElevatorDirection.None;
        }
    }
}
