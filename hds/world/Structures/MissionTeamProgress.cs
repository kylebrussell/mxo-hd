using System;
using System.Linq;
using hds.world.Structures;

namespace hds
{
    internal static class MissionTeamProgress
    {
        // serverrules.MissionTeamCheck and OwnTeamExclusive identify the mission/team owning the actual object.
        // NPC ids and instance counters alone are not globally unique across players.
        internal static WorldClient Holder(WorldClient killer,Mob mob)
        {
            if(killer?.playerData==null||mob==null||mob.missionInstance==0||WorldServer.Clients==null)return null;
            WorldClient[] clients;lock(WorldServer.Clients)clients=WorldServer.Clients.Values.ToArray();
            foreach(var owner in clients)
            {
                var data=owner?.playerData;
                if(data==null||!owner.Alive||!data.getOnWorld()||data.waitForRPCShutDown||data.isDead||data.worldChangePending||
                    data.getDistrictId()!=mob.getDistrict()||killer.playerData.getDistrictId()!=mob.getDistrict()||
                    data.missionProgress==null||data.activeMission!=data.missionProgress.mission||data.missionProgress.restoreRefusal!=null||
                    data.missionInstanceCounter!=mob.missionInstance||
                    !SocialGroups.Instance.ShareTeam(data.getCharID(),killer.playerData.getCharID()))continue;
                lock(data.missionMobs)if(data.missionMobs.Contains(mob))return owner;
            }
            return null;
        }
    }
}
