using System;
using System.Text;
using hds.shared;

namespace hds
{
    /// <summary>
    /// <c>81 67</c>: an ability's combat-log line (the scripts' SendAbilityOutputToCaster / ToTarget, ability/utility.py).
    /// Layout, from the retail captures (offsets from the 81):
    /// <code>81 67 &lt;17 00&gt; &lt;name2 offset u16&gt; &lt;string id u32&gt; &lt;flag u8&gt; &lt;ability u32&gt; &lt;ability goid u32&gt;
    /// &lt;values offset u16&gt; 00 &lt;08 / 07&gt; &lt;len u16&gt; name1\0 &lt;len u16&gt; name2\0 &lt;count u16&gt; &lt;i32 values&gt;</code>
    /// name1 is the caster (%2s), name2 the target (%3s), %1z the ability: by id with the type byte 08, or for damage
    /// lines by game-object id (0x80000000 | id &lt;&lt; 10) with 0x1000 in the id field and type 07. %4 onwards are the values.
    /// Example, a Plague Zone 2.0 tick (actions_combat 16649): <c>81 67 17 00 27 00 fa 00 00 7c 01 00 10 00 00 00 48 07 80 36 00
    /// 00 07 0e 00 AfterWhoruNeo\0 0d 00 Zombie Lurch\0 02 00 45 00 00 00 02 00 00 00</c> ("Your Plague Zone 2.0 does 69
    /// damage to Zombie Lurch.", 2 resisted).
    /// </summary>
    public static class AbilityMessages
    {
        // String ids (stringtable_client)
        public const UInt32 VirusHit = 0x240000f1;          // ID_CLIENT_ABILITY_VIRUS_HIT "%2s hit %3s with a %1z attack."
        public const UInt32 InfectsCaster = 0xc6000019;     // "Your %1z infects %3s!"
        public const UInt32 InfectsTarget = 0xc600001a;     // "%2s's %1z infects you!"
        public const UInt32 DamageCaster = 0x7c0000fa;      // ID_CLIENT_ABILITY_TAKE_DAMAGE_CASTER "Your %1z does %4I damage to %3s."
        public const UInt32 DamageTarget = 0x7c0000f9;      // ID_CLIENT_ABILITY_TAKE_DAMAGE_TARGET "%2s's %1z does %4I damage to you."
        public const UInt32 StunCaster = 0xc400001a;        // "Your %1z stuns %3s!"
        public const UInt32 StunTarget = 0xc400001b;        // "%2s's %1z stuns you!"
        public const UInt32 ImmunityCaster = 0xc60020c7;    // "%3s will be immune to stun and pacify for %4I seconds."
        public const UInt32 ImmunityTarget = 0xc60020c8;    // "You will be immune to stun and pacify for %4I seconds."
        public const UInt32 PowerlessCaster = 0x7a00004e;   // ID_CLIENT_STATE_ENABLE_POWERLESS_CASTER
        public const UInt32 PowerlessTarget = 0x7a00004d;   // ID_CLIENT_STATE_ENABLE_POWERLESS_TARGET
        public const UInt32 PacifyCaster = 0xc600000c;      // "Your %1z pacifies %3s!"
        public const UInt32 PacifyTarget = 0xc600000d;      // "%2s's %1z pacifies you!"
        public const UInt32 BlindCaster = 0xc600000a;       // "Your %1z blinds %3s!"
        public const UInt32 BlindTarget = 0x7c0001c0;       // "%2s's %1z blinds you!"
        public const UInt32 ConfuseCaster = 0xc6000199;     // "Your %1z confuses %3s!"
        public const UInt32 EnrageCaster = 0xc60013cf;      // "Your %1z enrages %3s."
        public const UInt32 EnrageTarget = 0xc60013d0;      // "%2s's %1z enrages you!"
        public const UInt32 RootTarget = 0x7c000152;        // ID_CLIENT_STATE_ENABLE_ROOT_TARGET "%2s's %1z has locked you in place!"
        public const UInt32 StateEnded = 0x7a00003c;        // ID_CLIENT_STATE_DISABLE_TARGET "You are no longer %4z."

        /// <summary>The ability id field of a line that names the ability by game-object id (damage lines).</summary>
        public const UInt32 ByGameObject = 0x1000;

        public const byte TypeAbility = 0x08, TypeGameObject = 0x07;

        /// <summary>Game-object id of an ability (abilityIDs.csv GOID: 1 -&gt; 0x80000400, 474 -&gt; 0x80076800).</summary>
        public static UInt32 AbilityGameObject(UInt16 abilityId)
        {
            return 0x80000000u | ((UInt32)abilityId << 10);
        }

        public static byte[] Build(UInt32 stringId, bool flag, UInt32 ability, UInt32 abilityObject, byte type,
            string caster, string target, params Int32[] values)
        {
            byte[] n1 = Encoding.ASCII.GetBytes(caster ?? "");
            byte[] n2 = Encoding.ASCII.GetBytes(target ?? "");
            UInt16 name1At = 0x17;
            UInt16 name2At = (UInt16)(name1At + 2 + n1.Length + 1);
            UInt16 valuesAt = (UInt16)(name2At + 2 + n2.Length + 1);

            PacketContent pak = new PacketContent();
            pak.AddHexBytes("8167");
            pak.AddUint16(name1At, 1);
            pak.AddUint16(name2At, 1);
            pak.AddUint32(stringId, 1);
            pak.AddByte(flag ? (byte)0x01 : (byte)0x00);
            pak.AddUint32(ability, 1);
            pak.AddUint32(abilityObject, 1);
            pak.AddUint16(valuesAt, 1);
            pak.AddByte(0x00);
            pak.AddByte(type);
            pak.AddUint16((UInt16)(n1.Length + 1), 1);
            pak.AddByteArray(n1);
            pak.AddByte(0x00);
            pak.AddUint16((UInt16)(n2.Length + 1), 1);
            pak.AddByteArray(n2);
            pak.AddByte(0x00);
            pak.AddUint16((UInt16)values.Length, 1);
            foreach (Int32 v in values)
            {
                pak.AddUint32(unchecked((UInt32)v), 1);
            }
            return pak.ReturnFinalPacket();
        }

        /// <summary>A line naming the ability by id (type 08): infections, states, hits.</summary>
        public static byte[] AboutAbility(UInt32 stringId, bool toCaster, UInt16 ability, string caster, string target,
            params Int32[] values)
        {
            return Build(stringId, toCaster, ability, 0, TypeAbility, caster, target, values);
        }

        /// <summary>A damage line (type 07): "Your %1z does %4I damage to %3s." with [done, raw - done].</summary>
        public static byte[] Damage(bool toCaster, UInt16 ability, string caster, string target, int done, int resisted)
        {
            return Build(toCaster ? DamageCaster : DamageTarget, toCaster, ByGameObject, AbilityGameObject(ability),
                TypeGameObject, caster, target, done, resisted);
        }

        /// <summary>"You are no longer %4z." (a state ending on the player, no caster name, ability 0).</summary>
        public static byte[] StateEnd(UInt16 state, string target)
        {
            return Build(StateEnded, true, 0, 0, TypeAbility, "", target, unchecked((Int32)TempMods.StateNameId(state)));
        }

        /// <summary>The caster's line when a state lands, with its values as retail sent them (null: no line).</summary>
        public static UInt32 CasterStateLine(UInt16 state, out Int32[] values)
        {
            switch (state)
            {
                case TempMods.StunAbility:
                    values = new Int32[] { 0 };          // RajkoHaxor's stun on Zombie Lurch (Rajko SomethingMaraC 54689): [0]
                    return StunCaster;
                case TempMods.PowerlessAbility:
                    values = new Int32[] { 0, 0 };       // entilsar lastday2 14846: [0, 0]
                    return PowerlessCaster;
                case TempMods.BlindAbility:
                    values = new Int32[] { 0, 0 };       // entilsar lastday2 29430: [0, 0]
                    return BlindCaster;
                case TempMods.ConfuseAbility:
                    values = new Int32[] { 0, 0 };       // Rajko AgentsAndIntPorting 24937: [0, 0]
                    return ConfuseCaster;
                case TempMods.MezAbility:
                    values = new Int32[] { 0, 0 };       // the target's line (Rajko MaraCThenTimeDOut 61030) had [0, 0]
                    return PacifyCaster;
                case TempMods.EnrageAbility:
                    values = new Int32[] { 0, 0 };       // the target's line (crashover2k_008 38417) had [0, 0]
                    return EnrageCaster;
                default:
                    values = null;
                    return 0;
            }
        }

        /// <summary>
        /// The target's line when a state lands on a player, and its flag byte (retail's stun line had 01, the others 00).
        /// </summary>
        public static UInt32 TargetStateLine(UInt16 state, out Int32[] values, out bool flag)
        {
            flag = false;
            switch (state)
            {
                case TempMods.StunAbility:
                    values = new Int32[] { 0 };          // crashover2k proxy_003 8687: flag 01, [0]
                    flag = true;
                    return StunTarget;
                case TempMods.PowerlessAbility:
                    values = new Int32[] { 0, 0 };       // entilsar lastday2 143646
                    return PowerlessTarget;
                case TempMods.BlindAbility:
                    values = new Int32[] { 0, 0 };       // proxy_020 143154
                    return BlindTarget;
                case TempMods.MezAbility:
                    values = new Int32[] { 0, 0 };       // Rajko MaraCThenTimeDOut 61030
                    return PacifyTarget;
                case TempMods.EnrageAbility:
                    values = new Int32[] { 0, 0 };       // crashover2k_008 38417
                    return EnrageTarget;
                case TempMods.RootedAbility:
                    values = new Int32[] { 0, 0 };       // Rajko MaraCCyphs 51874
                    return RootTarget;
                default:
                    values = null;
                    return 0;
            }
        }
    }
}
