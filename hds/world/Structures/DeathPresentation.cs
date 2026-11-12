using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>Source-backed movement requests; corpse stance is server presentation policy, native QA pending.
    /// Built-in constants: client.dll62692ea0/62692ee3; reader626718c0. See docs/DEATH-PRESENTATION.md.</summary>
    public static class DeathPresentation
    {
        public const byte Prone = 5, Standing = 0, Idle = 0, Resurrect = 63;
        public const ushort NoAbility = 0x1000;

        /// <summary>Built-in action, distinct from action25's named-emote u16. The trailing u16 is the ability context.</summary>
        public static byte[] Build(bool own, byte counter, byte stance, byte action, ushort ability,
            float x, float y, float z, uint simMs = 0)
        {
            var bytes = new List<byte>();
            if (own) bytes.AddRange(new byte[] { 2, 0 });
            bytes.AddRange(new byte[] { 1, (byte)(own ? 0x28 : 0x2a), counter, 0x40, stance, action });
            bytes.AddRange(BitConverter.GetBytes(ability));
            if (!own) bytes.Add(0); // ordinary movement action byte, separate from the scripted action
            bytes.AddRange(BitConverter.GetBytes(x));
            bytes.AddRange(BitConverter.GetBytes(y));
            bytes.AddRange(BitConverter.GetBytes(z));
            if (own) bytes.AddRange(BitConverter.GetBytes(simMs));
            return bytes.ToArray();
        }

        public static bool CanIntroduceNpc(Mob mob) => !mob.getIsDead() || mob.ScriptedCorpse;

        /// <summary>NPC_BASE uses the sourced NPC stance/animation layout, distinct from player prone stance5.</summary>
        public static byte[] NpcCorpse(Mob mob) => ServerPackets.BuildNpcDeathUpdate(mob.NextAnimationSeq(),
            (byte)mob.getRotation(), (float)mob.getXPos(), (float)mob.getYPos(), (float)mob.getZPos());

        public static bool IsCorpse(WorldClient player) => player.playerData.isDead || player.playerData.duelDeath;

        public static byte[] OtherCorpse(WorldClient player)
        {
            Position(player, out float x, out float y, out float z);
            return Build(false, player.playerData.emoteCounter, Prone, Idle, NoAbility, x, y, z);
        }

        private static void Position(WorldClient player, out float x, out float y, out float z)
        {
            double px = 0, py = 0, pz = 0;
            NumericalUtils.LtVector3dToDoubles(player.playerInstance.Position.getValue(), ref px, ref py, ref pz);
            x = (float)px; y = (float)py; z = (float)pz;
        }

        public static void Corpse(WorldClient player) => Play(player, Prone, Idle, NoAbility);
        public static void Stand(WorldClient player) => Play(player, Standing, Idle, NoAbility);
        public static void Resuscitate(WorldClient player, ushort ability) => Play(player, Standing, Resurrect, ability);

        /// <summary>Combat teardown can replace movement. Reapply only within this death/world lifecycle.</summary>
        public static void AfterInterlockTeardown(WorldClient player)
        {
            if (!IsCorpse(player)) return;
            int life = player.playerData.lifeGeneration, world = player.playerData.worldGeneration;
            DelayedActions.After(DuelHandler.ReassertSeconds, () =>
            {
                lock (player.playerData.lifeSync)
                {
                    if (!player.Alive || !player.playerData.getOnWorld() || !IsCorpse(player) ||
                        player.playerData.lifeGeneration != life || player.playerData.worldGeneration != world) return;
                    Corpse(player);
                    player.FlushQueue();
                }
            });
        }

        private static void Play(WorldClient player, byte stance, byte action, ushort ability)
        {
            Position(player, out float x, out float y, out float z);
            byte counter = ++player.playerData.emoteCounter;
            if (!player.IsBot)
                player.messageQueue.addObjectMessage(Build(true, counter, stance, action, ability, x, y, z,
                    CombatHandler.CombatStartTime()), false);
            PvpFlag.ToWatchers(player, Build(false, counter, stance, action, ability, x, y, z));
        }
    }
}
