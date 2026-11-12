using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>589's sourced pet aura; 698's two tactics scores feed the existing approximate proxy hit curve.</summary>
    public static class ProxyTechnician
    {
        public const ushort Ability = 589, Effect = 698;
        public const string QaRollVariable = "MXO_PROXY_TECHNICIAN_QA_ROLL";
        internal sealed class Context { public int World, Life, Generation; }
        private static readonly object contextsGate=new object();
        internal sealed class Snapshot { public int Melee, Ballistic, World, Life, RecipientWorld, RecipientLife; public WorldClient Source; public double Until; }
        private static readonly ConditionalWeakTable<WorldClient,Context> contexts = new ConditionalWeakTable<WorldClient,Context>();
        private static readonly ConditionalWeakTable<CoderPets.Pet,Snapshot> snapshots = new ConditionalWeakTable<CoderPets.Pet,Snapshot>();

        private static bool OwnerReady(WorldClient owner) => owner != null && owner.Alive && owner.playerData.getOnWorld() &&
            !owner.playerData.worldChangePending && !owner.playerData.DistrictIsLA() && !owner.playerData.isDead && !owner.playerData.duelDeath;
        public static bool Loaded(WorldClient owner) => AbilityPassives.Level(owner,Ability) > 0;
        public static string Refusal(WorldClient owner) => Loaded(owner) && OwnerReady(owner) ? null : "Load Proxy Technician on a live in-world character first.";
        public static bool Begin(WorldClient owner,int generation=0)
        {
            lock(contextsGate)
            {
                if (Refusal(owner) != null) return false;
                contexts.Remove(owner);contexts.Add(owner,new Context {World=owner.playerData.worldGeneration,Life=owner.playerData.lifeGeneration,Generation=generation});
                return true;
            }
        }
        private static bool MatchesLocked(WorldClient owner,Context c,int? generation=null) => c!=null && OwnerReady(owner) && Loaded(owner) &&
            c.World==owner.playerData.worldGeneration && c.Life==owner.playerData.lifeGeneration && (!generation.HasValue || c.Generation==generation.Value);
        internal static Context Capture(WorldClient owner,int? generation=null)
        {
            lock(contextsGate) return contexts.TryGetValue(owner,out var c) && MatchesLocked(owner,c,generation)?c:null;
        }
        public static bool Matches(WorldClient owner,int? generation=null) => Capture(owner,generation)!=null;
        public static void End(WorldClient owner) { lock(contextsGate) contexts.Remove(owner); }
        public static void LoadoutChanged(WorldClient owner) { if (!Loaded(owner)) AbilityAuras.Stop(owner,Ability); }

        internal static Snapshot Values(int casterLevel,double now)
        {
            var value=new Snapshot();
            foreach(var m in AbilityCatalog.Find(Effect).Modifiers.Where(m=>m.Number("WhenEnabled",-1)==2))
            {
                if (!AbilityValues.TryValue(m,casterLevel,casterLevel,out int n)) continue;
                if(m.Target==522)value.Melee=n;
                if(m.Target==524)value.Ballistic=n;
                value.Until=Math.Max(value.Until,now+m.Number("Duration"));
            }
            return value;
        }
        public static bool Eligible(WorldClient owner,CoderPets.Pet pet) => OwnerReady(owner) && pet != null &&
            OwnerReady(pet.Owner) && ReferenceEquals(CoderPets.Of(pet.Owner),pet) && !pet.Gone && !pet.Mob.getIsDead() &&
            pet.Mob.getDistrict()==owner.playerData.getDistrictId() && pet.Owner.playerData.getDistrictId()==owner.playerData.getDistrictId() &&
            AbilitySupport.FriendlyRecipient(owner,pet.Owner) &&
            CombatCalculator.CalculateDistanceMetres(owner.playerInstance.Position.getValue(),pet.Mob.getXPos(),pet.Mob.getYPos(),pet.Mob.getZPos())<=AbilityCatalog.Find(Effect).Number("Ability/RadiusOfEffect");
        internal static Snapshot Project(WorldClient owner,CoderPets.Pet pet,double now) => ProjectCaptured(owner,pet,Capture(owner),now);
        internal static Snapshot ProjectCaptured(WorldClient owner,CoderPets.Pet pet,Context captured,double now)
        {
            lock(contextsGate)
            {
                if(captured==null || !contexts.TryGetValue(owner,out var current) || !ReferenceEquals(current,captured) ||
                    !MatchesLocked(owner,captured) || !Eligible(owner,pet))return null;
                var value=Values(owner.playerInstance.Level.getValue()[0],now);
                value.Source=owner;value.World=captured.World;value.Life=captured.Life;
                value.RecipientWorld=pet.Owner.playerData.worldGeneration;value.RecipientLife=pet.Owner.playerData.lifeGeneration;
                // Begin/End cannot intervene between validation and installation. No I/O occurs while holding this gate.
                lock(pet) { snapshots.Remove(pet);snapshots.Add(pet,value); }
                return value;
            }
        }
        public static void Pulse(WorldClient owner,int? generation=null)
        {
            var captured=Capture(owner,generation);
            if(captured==null)return;
            foreach(var pet in CoderPets.SnapshotPets())
            {
                var value=ProjectCaptured(owner,pet,captured,TimeUtils.GetSimTimeSeconds());
                if(value==null)continue;
                uint fx=(uint)AbilityCatalog.Find(Effect).Modifiers.Select(m=>m.Number("ActivationFX")).FirstOrDefault(n=>n>0);
                CoderPets.ShowAuraEffect(pet,fx);
                Output.WriteDebugLog("[PROXYTECH] pulse source="+TimedEffects.PlayerName(owner)+" recipient="+TimedEffects.PlayerName(pet.Owner)+" pet="+pet.Mob.getEntityId()+" melee="+value.Melee+" ballistic="+value.Ballistic);
            }
        }
        private static Snapshot Current(CoderPets.Pet pet,double now)
        {
            if(pet==null || !snapshots.TryGetValue(pet,out var value))return null;
            if(pet.Gone || pet.Mob.getIsDead() || !OwnerReady(pet.Owner) || !ReferenceEquals(CoderPets.Of(pet.Owner),pet) || now>=value.Until ||
                value.RecipientWorld!=pet.Owner.playerData.worldGeneration || value.RecipientLife!=pet.Owner.playerData.lifeGeneration ||
                !OwnerReady(value.Source) || value.World!=value.Source.playerData.worldGeneration || value.Life!=value.Source.playerData.lifeGeneration)
            {
                // Expiry/old-source cleanup must never remove a later caster's replacement.
                lock(pet) if(snapshots.TryGetValue(pet,out var current) && ReferenceEquals(current,value))snapshots.Remove(pet);
                return null;
            }
            return value;
        }
        internal static void Prune(CoderPets.Pet pet,double now) => Current(pet,now);
        public static int Accuracy(CoderPets.Pet pet,double now,bool ranged)
        {
            var value=Current(pet,now);
            return value==null || pet.Kind!=CoderPets.Kind.Proxy ? 0 : ranged?value.Ballistic:value.Melee;
        }
        public static bool QaAllowed(string flag,string database,uint character,bool world,bool dead,string roll,out double value)
        {
            value=0;
            return QaLoadout.Allowed(flag,database,character,world,dead) &&
                double.TryParse(roll,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value) &&
                double.IsFinite(value) && value>=0 && value<100;
        }
        internal static double AttackRoll(CoderPets.Pet pet,out bool forced)
        {
            forced=false;
            if(pet?.Kind==CoderPets.Kind.Proxy && OwnerReady(pet.Owner) &&
                !string.Equals(TimedEffects.PlayerName(pet.Owner),"TheNeo",StringComparison.OrdinalIgnoreCase) &&
                QaAllowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
                    pet.Owner.playerData.getCharID(),pet.Owner.playerData.getOnWorld(),pet.Owner.playerData.isDead,
                    Environment.GetEnvironmentVariable(QaRollVariable),out double value))
            { forced=true;return value; }
            return Random.Shared.NextDouble()*100;
        }
        public static string Describe(ushort id) => id==Ability ? "friendly-simulacra aura698 on init/every5s; IS5/5s; loaded/world/life guards; sourced radius20 and5s child; typed Proxy tactics522/524 consumer (existing OURS hit curve); [source](ABILITY-PROXY-TECHNICIAN.md)" :
            id==Effect ? "caster-level binary32x1.3 tactics522/524 for5s on friendly live same-world simulacra within20m; only Proxy melee/ballistic attack roll consumes them; no new viral/heal/defense consumer" : "";
    }
}
