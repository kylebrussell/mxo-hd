using System;

namespace hds
{
    // Checkpoint rollback restores mission state and withholds teleport output. Restore the associated
    // server-side transport state too, so a failed departure can be retried from the original floor.
    internal sealed class MissionTransportState : IDisposable
    {
        private readonly WorldClient client;
        private readonly ClientData data;
        private readonly PlayerCharacter player;
        private readonly byte[] position;
        private readonly StaticWorldObject panel;
        private readonly byte counter;
        private readonly int world,life;
        private readonly uint district;
        private bool committed;
        internal MissionTransportState(WorldClient client)
        {
            this.client=client;data=client.playerData;player=client.playerInstance;
            position=(byte[])player.Position.getValue().Clone();panel=data.elevatorPanel;counter=data.teleportCounter;
            world=data.worldGeneration;life=data.lifeGeneration;district=data.getDistrictId();
        }
        internal void Commit() => committed=true;
        public void Dispose()
        {
            if(committed || !ReferenceEquals(client.playerData,data) || !ReferenceEquals(client.playerInstance,player) ||
                data.worldGeneration!=world || data.lifeGeneration!=life || data.getDistrictId()!=district)return;
            player.Position.setValue(position);data.elevatorPanel=panel;data.teleportCounter=counter;
        }
        internal static bool Run(WorldClient client, Func<bool> action, MissionRideStamp stamp = null)
        {
            lock(client.playerData.missionSync)
            {
                // Nested batches belong to another SQL owner. Do not move or mint evidence before
                // that owner commits; transport is retried as a separate ordinary operation.
                if(MissionOutput.Buffered(client.messageQueue))return false;
                stamp=stamp??new MissionRideStamp(client,client.playerData.missionProgress);
                if(!stamp.Current)return false;
                using(var state=new MissionTransportState(client))
                {
                    bool committed=MissionPersistence.Mutate(client,()=>
                    {
                        if(!stamp.Current)return false;
                        if(!stamp.BindOutput())throw new MissionCheckpointException("Stale mission transport output");
                        bool moved=action();
                        stamp.RequireCurrent(); // final owner check immediately before Mutate saves
                        return moved;
                    });
                    if(committed)state.Commit();
                    return committed;
                }
            }
        }
    }

    internal sealed class MissionRideStamp
    {
        private readonly WorldClient client;
        private readonly ClientData data;
        private readonly MissionProgress progress;
        private readonly MissionSite site;
        private readonly int phase, world, life;
        private readonly uint character, district;
        private readonly bool onWorld;
        internal readonly long Transport;
        internal MissionRideStamp(WorldClient client, MissionProgress progress)
        {
            this.client=client;data=client.playerData;this.progress=progress;site=progress?.site;
            phase=progress?.phaseIndex??-1;world=data.worldGeneration;life=data.lifeGeneration;
            character=data.getCharID();district=data.getDistrictId();onWorld=data.getOnWorld();
            Transport=client.TransportFence.Capture();
        }
        internal bool BindOutput() => MissionOutput.BindCurrent(client.messageQueue,client.TransportFence,Transport,world);
        internal void RequireCurrent()
        {
            if(!Current)throw new MissionCheckpointException("Stale mission transport owner");
        }
        internal bool Current => client.Alive && ReferenceEquals(client.playerData,data) &&
            ReferenceEquals(data.missionProgress,progress) && (progress==null || progress.phaseIndex==phase && ReferenceEquals(progress.site,site)) &&
            data.worldGeneration==world && data.lifeGeneration==life && data.getCharID()==character &&
            data.getDistrictId()==district && data.getOnWorld()==onWorld && !data.isDead && !data.worldChangePending &&
            !data.waitForRPCShutDown && client.TransportFence.Capture()==Transport;
    }

    public partial class MissionProgress
    {
        private StaticWorldObject departedSite;
        private int departedPhase = -1;

        // Authored policy for generated floors with an actual elevator ride. Ground-floor sites retain
        // the historical exit predicate: no transport event can distinguish their inside and outside.
        internal bool UsesGeneratedDeparture => site?.elevatorDoor != null && site.lobbyDoor != null &&
            site.elevatorDoor.pos_y != site.lobbyDoor.pos_y;
        internal bool HasGeneratedDeparture => UsesGeneratedDeparture && departedPhase == phaseIndex &&
            SameDepartureDoor(departedSite, site.elevatorDoor);
        internal static bool SameDepartureDoor(StaticWorldObject a, StaticWorldObject b) => a != null && b != null &&
            a.metrId == b.metrId && a.sectorID == b.sectorID && a.mxoStaticId == b.mxoStaticId &&
            a.pos_x == b.pos_x && a.pos_y == b.pos_y && a.pos_z == b.pos_z;

        // Called only after a successful normal transport commits its destination, never at request time.
        internal void CommitGeneratedFloor(int sourcePhase, MissionSite sourceSite, StaticWorldObject destination)
        {
            if (phaseIndex != sourcePhase || !ReferenceEquals(site, sourceSite) || !UsesGeneratedDeparture) return;
            if (SameDepartureDoor(destination, site.elevatorDoor))
            {
                departedSite = null; departedPhase = -1; // returning before the last goal is not an exit
            }
            else if (SameDepartureDoor(destination, site.lobbyDoor))
            {
                departedSite = site.elevatorDoor; departedPhase = phaseIndex;
            }
        }

        internal void CommitGeneratedPanelFloor(Elevators.PanelFloor from, Elevators.PanelFloor to)
        {
            if (!UsesGeneratedDeparture || from == null || to == null || from.metrId != to.metrId ||
                from.sector != to.sector || from.shaft != to.shaft || from.floorFromBottom == to.floorFromBottom) return;
            var interior = Elevators.PanelForDoor(site.elevatorDoor);
            var lobby = Elevators.PanelForDoor(site.lobbyDoor);
            bool Same(Elevators.PanelFloor a, Elevators.PanelFloor b) => a != null && b != null &&
                a.metrId == b.metrId && a.sector == b.sector && a.shaft == b.shaft && a.mxoStaticId == b.mxoStaticId;
            if (interior == null || lobby == null || interior.shaft != lobby.shaft ||
                interior.y - lobby.y != site.elevatorDoor.pos_y - site.lobbyDoor.pos_y) return;
            if (Same(to, interior)) CommitGeneratedFloor(phaseIndex, site, site.elevatorDoor);
            else if (Same(from, interior) && Same(to, lobby)) CommitGeneratedFloor(phaseIndex, site, site.lobbyDoor);
        }
    }
}
