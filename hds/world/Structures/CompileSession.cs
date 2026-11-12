using System;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>CompileActivate's tool-bound, exclusive UI state. Recipe/economy execution stays in CraftingHandler.</summary>
    public static class CompileSession
    {
        public const ushort Ability = 969, Tool = 1271, Attribute = 968;
        public sealed class State
        {
            public byte Slot;
            public ushort Record;
            public uint District;
            public int World;
        }

        // 621ed3d7 passes flag1 for tool activation; 62080723 passes flag0 on Compile-window close.
        // 621e5129..5138 serializes tool slot and flag at packet+0x25/26 (body+35/36).
        public static (byte Slot, bool Activate)? Parse(byte[] body) => body?.Length == 41 &&
            BitConverter.ToUInt16(body, 0) == Ability && body[35] >= 1 && body[35] <= 96 && body[36] <= 1
            ? (body[35], body[36] == 1) : ((byte, bool)?)null;

        public static bool Matches(State state, byte slot, uint district, int world, bool ready, bool loaded, bool tool) =>
            state != null && state.Slot == slot && state.District == district && state.World == world && ready && loaded && tool;

        private static bool Ready(WorldClient c) => c.playerData.getOnWorld() && !c.playerData.worldChangePending &&
            !c.playerData.DistrictIsLA() && !c.playerData.isDead && !c.playerData.duelDeath &&
            !CombatManager.GetHandler(c).isCombatRunning && ControlActions.CanUseAbilities(c) &&
            !ControlActions.Incapacitated(TimedEffects.ForPlayer(c).Effects);

        private static bool Loaded(WorldClient c) => CombatSelection.IsLoaded(Ability,
            Store.dbManager.MarginDbHandler.LoadAbilities((int)c.playerData.getCharID()));
        private static bool HasTool(WorldClient c, byte slot) => Store.dbManager.WorldDbHandler
            .GetInventoryItems(c.playerData.getCharID()).Any(i => i.Slot == slot && i.Goid == Tool);

        public static bool Authorized(WorldClient c, byte slot)
        {
            lock (c.playerData.craftingSync)
            {
                var state = c.playerData.compileSession;
                if (state == null) return false;
                bool valid = Matches(state, state.Slot, c.playerData.getDistrictId(), c.playerData.worldGeneration,
                    Ready(c), Loaded(c), HasTool(c, state.Slot));
                if (!valid) End(c);
                return valid && state.Slot == slot;
            }
        }

        public static void Check(WorldClient c)
        {
            lock (c.playerData.craftingSync)
                if (c.playerData.compileSession is State state) Authorized(c, state.Slot);
        }

        public static void Process(WorldClient c, byte[] body)
        {
            lock (c.playerData.craftingSync)
            {
                var request = Parse(body);
                if (request == null) { Notice(c, "Activate a Code Compiler Tool to compile items."); return; }
                var r = request.Value;
                if (!r.Activate)
                {
                    if (c.playerData.compileSession?.Slot == r.Slot) End(c);
                    return;
                }
                if (!Ready(c) || !Loaded(c) || !HasTool(c, r.Slot))
                { Notice(c, "Compile Items requires its loaded ability and a Code Compiler Tool outside combat."); return; }
                if (Authorized(c, r.Slot)) return;
                End(c);
                Stealth.BreakForAction(c, "activated compiler");
                var state = new State { Slot = r.Slot, Record = AbilityBuffs.NextRecordId(c.playerData),
                    District = c.playerData.getDistrictId(), World = c.playerData.worldGeneration };
                c.playerData.compileSession = state;
                c.messageQueue.addRpcMessage(AbilityBuffs.BuildExclusive(Ability, true));
                c.messageQueue.addRpcMessage(Record(state, false));
                c.FlushQueue();
            }
        }

        public static byte[] Record(State state, bool off) => AbilityBuffs.BuildModifier(
            AbilityBuffs.KindLinked, state.Record, Ability, Attribute, 0, off, 0);

        public static bool End(WorldClient c, bool notify = true)
        {
            lock (c.playerData.craftingSync)
            {
                var state = c.playerData.compileSession;
                if (state == null) return false;
                c.playerData.compileSession = null;
                if (notify)
                {
                    c.messageQueue.addRpcMessage(Record(state, true));
                    c.messageQueue.addRpcMessage(AbilityBuffs.BuildExclusive(Ability, false));
                    c.FlushQueue();
                }
                return true;
            }
        }

        public static string Dev(WorldClient c, string[] args)
        {
            if (Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") != "1") return "?compilesession is a dev command.";
            if (args.Length == 2 && args[1] == "off") End(c);
            else if (args.Length != 1) return "Usage: ?compilesession [off]";
            Check(c);
            return c.playerData.compileSession is State s ? "Compile session active; tool slot " + s.Slot + "." : "Compile session inactive.";
        }
        private static void Notice(WorldClient c, string text)
        { new ServerPackets().sendSystemChatMessage(c, text, "SYSTEM"); c.FlushQueue(); }
    }
}
