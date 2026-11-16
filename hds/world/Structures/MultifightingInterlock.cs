using System;
using hds.shared;
namespace hds
{
    /// <summary>Primary multi-on-one math; OURS threshold/current-link caller policy. No numeric stat is invented.</summary>
    public static class MultifightingInterlock
    {
        public const ushort Ability=140;
        public const int Threshold=1;
        public static int Penalty(int currentOpponents,bool loaded) =>
            CombatRules.MultiOnOnePenalty(currentOpponents,currentOpponents,loaded,Threshold);
        internal static bool Loaded(WorldClient client)
        {
            var state=client.playerData.passives;
            lock(state)return state.Loaded.ContainsKey(Ability);
        }
        // Created before the fight lock, never allowed to adopt a replacement world/player/session.
        internal sealed class Owner
        {
            internal readonly WorldClient Client;
            internal readonly ClientData Data;
            private readonly PlayerCharacter player;
            private readonly CombatHandler handler;
            private readonly uint character,district;
            private readonly int world,life;
            private readonly string key;
            internal Owner(WorldClient client)
            {
                Client=client;Data=client.playerData;player=client.playerInstance;
                handler=CombatManager.GetHandler(client);character=Data.getCharID();district=Data.getDistrictId();
                world=Data.worldGeneration;life=Data.lifeGeneration;key=Data.getUniqueKey(null);
            }
            // Called only outside fight/RNG locks. Direct lookup, never an all-world scan.
            internal bool EndpointCurrent()
            {
                var registry=WorldServer.Clients;if(registry==null||key==null)return false;
                lock(registry)return ReferenceEquals(registry,WorldServer.Clients) &&
                    registry.TryGetValue(key,out var current) && ReferenceEquals(current,Client);
            }
            internal bool Current(PvpFight fight,long transport,bool loaded) =>
                ReferenceEquals(Client.playerData,Data) && ReferenceEquals(Client.playerInstance,player) &&
                Data.getCharID()==character && Data.getDistrictId()==district && Data.worldGeneration==world && Data.lifeGeneration==life &&
                Client.Alive && Data.getOnWorld() && !Data.isDead && !Data.duelDeath && !Data.worldChangePending &&
                PvpCombat.HealthOf(Client)>0 && ReferenceEquals(handler.pvpFight,fight) &&
                Client.TransportFence!=null && Client.TransportFence.IsCurrent(transport) && Loaded(Client)==loaded;
        }
    }
}
