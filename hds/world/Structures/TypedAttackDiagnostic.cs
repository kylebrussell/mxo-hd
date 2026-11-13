using hds.shared;
using System;
namespace hds
{
    /// <summary>Read-only paired seeded contests. Does not alter live combat RNG, health, inventory or ability ownership.</summary>
    public static class TypedAttackDiagnostic
    {
        public static bool Allowed(string dev, string database, uint character, bool world, bool dead, string name) =>
            QaLoadout.Allowed(dev, database, character, world, dead) && !string.Equals(name,"TheNeo",StringComparison.OrdinalIgnoreCase);
        public static string Run(WorldClient caller, string[] args)
        {
            if (caller == null || !ReferenceEquals(caller,Store.currentClient) || !caller.Alive ||
                caller.playerData.worldChangePending || caller.playerData.DistrictIsLA() || caller.playerData.duelDeath ||
                !Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
                    caller.playerData.getCharID(),caller.playerData.getOnWorld(),caller.playerData.isDead,TimedEffects.PlayerName(caller)))
                return "Special attack diagnostic requires MXO_DEV_COMMANDS=1, strict mxotest_ and a live character other than 2/TheNeo.";
            if (args.Length != 2 || !ushort.TryParse(args[1],out ushort id) || (id != 105 && id != 134 && id != 197 && id != 33))
                return "Usage: ?combatdepth specialattack <105|134|197|33> (33 diagnostic only; positive cast is not queued).";
            var move = new AbilityItem(); move.setAbilityID(id);
            int total = AbilityStats.SpecialAttack(caller,move), hits = 0;
            var rng = new Random(731);
            for (int i=0;i<1000;i++)
                if (CombatInterlock.RollHit(rng,CombatTactic.Speed,50,CombatTactic.Speed,50,out _,out _,CombatTacticType.Normal,total,0)) hits++;
            return "Special attack " + id + " total " + total + "; seeded landed " + hits + "/1000; read-only diagnostic, no live roll/HP changed.";
        }
    }
}
