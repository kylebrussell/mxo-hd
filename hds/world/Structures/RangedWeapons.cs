using System;
using System.Collections.Concurrent;

namespace hds
{
    /// <summary>
    /// Guns: which attack animation class a weapon uses in the attack action record <c>01 0c &lt;class&gt;</c>.
    /// Retail evidence (research/packet-dumps): 0x38 with Revolver0 (Redpill Special) and SemiAutoPistolSystem
    /// (the player's own shots, paired with their EquippedItemID); 0x37 with AutomaticRifle1/1a, e50autorifle and
    /// e50shotgun NPCs; 0x39 with e50submachinegun NPCs (NPC shots carrying their EquippedItemID, e.g. "Chopper
    /// Jumper" with AutomaticRifle1 in the luizoe old-sniffer Unknown log, line 32719). 0x3b for dual weapons is a
    /// guess: it is common on NPC shots, but no capture ties it to a weapon.
    /// </summary>
    public static class RangedWeapons
    {
        public const byte ClassRifle = 0x37;
        public const byte ClassPistol = 0x38;
        public const byte ClassSubmachinegun = 0x39;
        public const byte ClassDual = 0x3b;

        private static readonly ConcurrentDictionary<UInt32, byte> classByGoid = new ConcurrentDictionary<UInt32, byte>();

        /// <summary>Attack class from the item's internal name (gameobjects.csv), 0 when it isn't a gun.</summary>
        public static byte AttackClassForName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 0;
            }
            string n = name.ToLowerInvariant();
            if (n.Contains("dualpistol") || n.Contains("dualsubmachinegun"))
            {
                return ClassDual;
            }
            if (n.Contains("submachinegun") || n.Contains("subgun"))
            {
                return ClassSubmachinegun;
            }
            if (n.Contains("rifle") || n.Contains("shotgun"))
            {
                return ClassRifle;
            }
            if (n.Contains("pistol") || n.Contains("revolver"))
            {
                return ClassPistol;
            }
            return 0;
        }

        /// <summary>Attack class of a weapon by goid, 0 when it isn't a gun or isn't known.</summary>
        public static byte AttackClass(UInt32 goid)
        {
            if (goid == 0)
            {
                return 0;
            }
            return classByGoid.GetOrAdd(goid, g =>
            {
                GameObjectItem item = DataLoader.getInstance().GODB.Find(i => (UInt32)i.getGOID() == g);
                return AttackClassForName(item?.getName());
            });
        }

        public static bool IsGun(UInt32 goid)
        {
            return AttackClass(goid) != 0;
        }
    }
}
