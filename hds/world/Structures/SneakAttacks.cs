using System;
using System.Collections.Generic;
namespace hds
{
    /// <summary>spy/sneaks.py constants. Adding them to a stealth-started opener is server policy:
    /// the decompiled tests are stubs and do not establish the retail bonus trigger.</summary>
    public static class SneakAttacks
    {
        private static readonly Dictionary<ushort,int> bonuses = new Dictionary<ushort,int>
        { {152,75},{153,150},{287,300},{286,300},{289,600},{288,775},{386,900},{385,725},{387,950},{388,1200} };
        public static int Bonus(ushort ability, bool fromStealth) => fromStealth && bonuses.TryGetValue(AbilityCatalog.Canonical(ability), out int value) ? value : 0;
        public static ushort Add(ushort damage, ushort ability, bool fromStealth) => (ushort)Math.Min(ushort.MaxValue, damage + Bonus(ability, fromStealth));
    }
}
