using System;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>GhostStrike_Subject only. Cast/test timing, throw contest and damage remain current server policy.</summary>
    public static class GhostStrikeCallbacks
    {
        public const ushort Ability=365;
        private static readonly Random random=new Random();
        private sealed class Setting {internal int Roll;internal Func<bool> Current;}
        private static readonly ConditionalWeakTable<WorldClient,Setting> settings=new ConditionalWeakTable<WorldClient,Setting>();
        internal static Func<bool> Lifetime(WorldClient client)
        {
            var current=AbilitySeverArteryStates.Lifetime(client);uint district=client.playerData.getDistrictId();
            return ()=>current()&&client.playerData.getDistrictId()==district&&PvpCombat.HealthOf(client)>0;
        }
        public static string Dev(WorldClient source,string[] words)
        {
            if(source==null||!QaDraw(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
                Environment.GetEnvironmentVariable("MXO_GHOST_STRIKE_QA"),source.playerData.getCharID(),"8").HasValue||
                !AbilitySeverArteryStates.Lifetime(source)()||string.Equals(TimedEffects.PlayerName(source),"TheNeo",StringComparison.OrdinalIgnoreCase))
                return "Ghost Strike QA requires dev1, disposable database, feature marker and current CSR110.";
            if(words.Length==1&&words[0]=="off"){settings.Remove(source);return "Ghost Strike callback normal RNG restored.";}
            if(words.Length!=1||!int.TryParse(words[0],out int roll)||(roll!=8&&roll!=9))return "Usage: ?ghoststrike <8|9> | off; callback only.";
            settings.Remove(source);settings.Add(source,new Setting{Roll=roll,Current=AbilitySeverArteryStates.Lifetime(source)});
            return "Ghost Strike callback roll="+roll+"; base damage and cast/contest RNG unchanged.";
        }
        public sealed class Ticket
        {
            internal WorldClient Source;
            internal Func<bool> SourceCurrent,TargetCurrent;
            internal object Target;
            internal ushort View;
            internal uint Spawn;
            internal double Expires;
            internal int Level,Action,StealthVersion,InitialAction,InitialStealthVersion;
            internal AbilityCastInterrupts.Cast NormalCast;
            internal bool Armed,Consumed;
        }
        public static Ticket Capture(WorldClient source,ushort ability,ushort view)
        {
            if(ability!=Ability)return null;
            var hidden=Stealth.Current(source);
            if(hidden==null||(hidden.Def.Kind!=Stealth.Kind.Invisibility&&hidden.Def.Kind!=Stealth.Kind.Imparted)||
                double.IsInfinity(hidden.Expires)||hidden.Expires<=TimeUtils.GetSimTimeSeconds())return null;
            var mob=AbilityTargets.FindMob(source,view,out var clientView);
            var player=mob==null?PvpCombat.PlayerInView(source,view):null;
            if(mob==null&&player==null)return null;
            return new Ticket {Source=source,SourceCurrent=Lifetime(source),Target=(object)mob??player,
                TargetCurrent=mob!=null?AbilitySeverArteryStates.MobLifetime(mob):Lifetime(player),
                View=view,Spawn=clientView?.spawnId??0,Expires=hidden.Expires,Level=Stealth.AbilityLevel(source,Stealth.InvisibilityGrantAbility),
                InitialAction=StealthShieldAbilities.ActionVersion(source),InitialStealthVersion=hidden.Generation};
        }
        // Arm after this cast's ordinary used-ability break, never exempt the ability from that break.
        public static void Arm(Ticket ticket)
        {
            if(ticket==null)return;
            ticket.Action=StealthShieldAbilities.ActionVersion(ticket.Source);
            ticket.StealthVersion=Stealth.Version(ticket.Source);
            ticket.Armed=ticket.Action==ticket.InitialAction+1&&ticket.StealthVersion==ticket.InitialStealthVersion;
        }
        public static void Bind(Ticket ticket,AbilityCastInterrupts.Cast cast)
        {if(ticket!=null)ticket.NormalCast=cast;}
        internal static bool Current(Ticket ticket)=>ticket!=null&&ticket.Armed&&ticket.SourceCurrent()&&
            ticket.NormalCast!=null&&ticket.NormalCast.Completed&&ticket.NormalCast.Ability==Ability&&
            ReferenceEquals(ticket.NormalCast.Client,ticket.Source)&&
            ticket.Action==StealthShieldAbilities.ActionVersion(ticket.Source)&&ticket.StealthVersion==Stealth.Version(ticket.Source)&&
            Stealth.Current(ticket.Source)==null&&TimeUtils.GetSimTimeSeconds()<ticket.Expires;
        internal static bool BeforeHit(Ticket ticket,WorldClient source,object target,ushort view,uint spawn=0)
        {
            if(ticket==null)return true;
            return ReferenceEquals(ticket.Source,source)&&ReferenceEquals(ticket.Target,target)&&ticket.View==view&&
                ticket.Spawn==spawn&&ticket.SourceCurrent()&&ticket.TargetCurrent();
        }
        internal static bool Wins(int draw)=>draw>=1&&draw<=8;
        internal static bool Accepted(Ticket ticket,Func<int> draw=null)
        {
            if(ticket==null)return false;
            lock(ticket.Source.playerData.craftingSync)
            lock(ticket)
            {
                if(ticket.Consumed||!Current(ticket))return false;
                // Target was validated BEFORE the hit; its death from that hit must not cancel a source callback.
                ticket.Consumed=true;
                int roll=draw!=null?draw():Draw(ticket.Source);
                if(roll<1||roll>10)throw new ArgumentOutOfRangeException(nameof(draw));
                if(!Wins(roll)||!Current(ticket))return false;
                float remaining=(float)(ticket.Expires-TimeUtils.GetSimTimeSeconds());
                if(remaining<.001f)return false;
                var restored=Stealth.Begin(ticket.Source,Stealth.Defs[Stealth.InvisibilityGrantAbility],ticket.Level,durationOverride:remaining);
                if(restored==null)return false;
                // Float duration is presentation/scheduling only; retain the exact original absolute expiry.
                restored.Expires=ticket.Expires;
                ticket.Source.FlushQueue();
                Output.WriteDebugLog("[GHOST-STRIKE] accepted callback roll="+roll+" originalExpiry="+ticket.Expires+" remaining="+remaining);
                return true;
            }
        }
        private static int Draw(WorldClient source)
        {
            var qa=QaDraw(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
                Environment.GetEnvironmentVariable("MXO_GHOST_STRIKE_QA"),source.playerData.getCharID(),
                Environment.GetEnvironmentVariable("MXO_GHOST_STRIKE_QA_ROLL"));
            if(QaDraw(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
                Environment.GetEnvironmentVariable("MXO_GHOST_STRIKE_QA"),source.playerData.getCharID(),"8").HasValue&&
                settings.TryGetValue(source,out var setting)&&setting.Current())return setting.Roll;
            if(qa.HasValue)return qa.Value;
            lock(random)return random.Next(1,11);
        }
        public static int? QaDraw(string dev,string database,string feature,uint character,string raw)=>
            AbilitySeverArteryStates.QaAllowed(dev,database,feature)&&character==110&&int.TryParse(raw,out int value)&&(value==8||value==9)?(int?)value:null;
    }
}
