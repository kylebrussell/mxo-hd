using System;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>Mutually exclusive compiler/writer UI state. Recipe/economy execution stays in CraftingHandler.</summary>
    public static class CraftingSession
    {
        public const ushort Ability = 969, Tool = 1271, Attribute = 968;
        public const ushort WriterAbility = 970, WriterTool = 531;
        // Shipped movement table629ce160: StandType is index3; Action_Idle is index0.
        public const byte TypingStance = 3;
        public static bool Handles(ushort ability) => ability == Ability || ability == WriterAbility;
        public static ushort ToolFor(ushort ability) => ability == WriterAbility ? WriterTool : Tool;
        public sealed class State
        {
            public byte Slot;
            public ushort Ability = CraftingSession.Ability;
            public ushort Record;
            public uint District;
            public int World;
        }

        // 621ed3d7 passes flag1 for tool activation; 62080723 passes flag0 on Compile-window close.
        // 621e5129..5138 serializes tool slot and flag at packet+0x25/26 (body+35/36).
        public static (byte Slot, bool Activate)? Parse(byte[] body) => body?.Length == 41 &&
            Handles(BitConverter.ToUInt16(body, 0)) && body[35] >= 1 && body[35] <= 96 && body[36] <= 1
            ? (body[35], body[36] == 1) : ((byte, bool)?)null;

        public static bool Matches(State state, byte slot, uint district, int world, bool ready, bool loaded, bool tool) =>
            state != null && state.Slot == slot && state.District == district && state.World == world && ready && loaded && tool;

        private static bool Ready(WorldClient c) => c.playerData.getOnWorld() && !c.playerData.worldChangePending &&
            !c.playerData.DistrictIsLA() && !c.playerData.isDead && !c.playerData.duelDeath &&
            !CombatManager.GetHandler(c).isCombatRunning && ControlActions.CanUseAbilities(c) &&
            !ControlActions.Incapacitated(TimedEffects.ForPlayer(c).Effects);

        private static bool Loaded(WorldClient c, ushort ability) => CombatSelection.IsLoaded(ability,
            Store.dbManager.MarginDbHandler.LoadAbilities((int)c.playerData.getCharID()));
        private static bool HasTool(WorldClient c, byte slot, ushort ability) => Store.dbManager.WorldDbHandler
            .GetInventoryItems(c.playerData.getCharID()).Any(i => i.Slot == slot && i.Goid == ToolFor(ability));

        public static bool Authorized(WorldClient c, byte slot, ushort ability = Ability)
        {
            lock (c.playerData.craftingSync)
            {
                var state = c.playerData.craftingSession;
                if (state == null) return false;
                bool valid = Matches(state, state.Slot, c.playerData.getDistrictId(), c.playerData.worldGeneration,
                    Ready(c) && c.playerData.stance == TypingStance, Loaded(c, state.Ability), HasTool(c, state.Slot, state.Ability));
                if (!valid) End(c);
                return valid && state.Slot == slot && state.Ability == ability;
            }
        }

        public static void Check(WorldClient c)
        {
            lock (c.playerData.craftingSync)
                if (c.playerData.craftingSession is State state) Authorized(c, state.Slot, state.Ability);
        }

        public static void Process(WorldClient c, byte[] body)
        {
            lock (c.playerData.craftingSync)
            {
                var request = Parse(body);
                ushort ability = body?.Length >= 2 ? BitConverter.ToUInt16(body, 0) : Ability;
                string tool = ability == WriterAbility ? "Code Analyser Tool" : "Code Compiler Tool";
                string name = ability == WriterAbility ? "Write Code" : "Compile Items";
                if (request == null) { Notice(c, "Activate a " + tool + " to " + (ability == WriterAbility ? "write code." : "compile items.")); return; }
                var r = request.Value;
                if (!r.Activate)
                {
                    if (c.playerData.craftingSession?.Slot == r.Slot && c.playerData.craftingSession.Ability == ability) End(c);
                    return;
                }
                if (!Ready(c) || !Loaded(c, ability) || !HasTool(c, r.Slot, ability))
                { Notice(c, name + " requires its loaded ability and a " + tool + " outside combat."); return; }
                if (Authorized(c, r.Slot, ability)) return;
                End(c);
                Stealth.BreakForAction(c, "activated crafting tool");
                var state = new State { Slot = r.Slot, Record = AbilityBuffs.NextRecordId(c.playerData),
                    Ability = ability, District = c.playerData.getDistrictId(), World = c.playerData.worldGeneration };
                c.playerData.craftingSession = state;
                c.messageQueue.addRpcMessage(AbilityBuffs.BuildExclusive(ability, true));
                c.messageQueue.addRpcMessage(Record(state, false));
                EmoteHandler.SetStance(c, TypingStance);
                c.FlushQueue();
            }
        }

        public static byte[] Record(State state, bool off) => AbilityBuffs.BuildModifier(
            AbilityBuffs.KindLinked, state.Record, state.Ability, Attribute, 0, off, 0);

        public static bool End(WorldClient c, bool notify = true, ushort ability = 0)
        {
            lock (c.playerData.craftingSync)
            {
                var state = c.playerData.craftingSession;
                if (state == null || ability != 0 && state.Ability != ability) return false;
                c.playerData.craftingSession = null;
                if (c.playerData.stance == TypingStance)
                {
                    if (notify) EmoteHandler.SetStance(c, AnimationPackets.StanceStanding);
                    else c.playerData.stance = AnimationPackets.StanceStanding;
                }
                if (notify)
                {
                    c.messageQueue.addRpcMessage(Record(state, true));
                    c.messageQueue.addRpcMessage(AbilityBuffs.BuildExclusive(state.Ability, false));
                    c.FlushQueue();
                }
                return true;
            }
        }

        public static string Dev(WorldClient c, string[] args)
        {
            string command = args[0];
            ushort ability = command == "?writersession" ? WriterAbility : Ability;
            if (Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") != "1") return command + " is a dev command.";
            if (args.Length == 2 && args[1] == "off") End(c, ability: ability);
            else if (args.Length != 1) return "Usage: " + command + " [off]";
            Check(c);
            string name = ability == WriterAbility ? "Writer" : "Compile";
            return c.playerData.craftingSession is State s && s.Ability == ability ? name + " session active; tool slot " + s.Slot + "." : name + " session inactive.";
        }
        private static void Notice(WorldClient c, string text)
        { new ServerPackets().sendSystemChatMessage(c, text, "SYSTEM"); c.FlushQueue(); }
    }
}
