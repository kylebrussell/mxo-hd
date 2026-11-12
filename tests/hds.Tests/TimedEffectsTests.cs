using hds;
using hds.shared;

namespace hds.Tests;

// The timed-effects tick is shared like DelayedActions' queue: running it in one class would fire the other's.
[Collection("DelayedActions")]
public class TimedEffectsTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    private static string Ascii(string s) => Hex(System.Text.Encoding.ASCII.GetBytes(s));

    private static TimedEffects.Active Mod(UInt16 target, int value, float seconds, TempMods.Proc proc = TempMods.Proc.Stat,
        UInt16 kind = TempMods.KindStat) =>
        new TimedEffects.Active { Kind = kind, Target = target, Value = value, Seconds = seconds, Proc = proc };

    // ---- the holder: apply, expire, refresh, stack ----

    [Fact]
    public void EachTempmodExpiresOnItsOwnTime()
    {
        // Vector: Powerless 8.94 s, ViralDeflection -60 for 30 s
        var holder = new TimedEffects.Holder();
        holder.Apply(355, new List<TimedEffects.Active>
        {
            Mod(TempMods.PowerlessAbility, 0, 8.94f, TempMods.Proc.State, TempMods.KindState),
            Mod(183, -60, 30)
        }, 100);
        Assert.True(holder.Has(TempMods.PowerlessAbility));
        Assert.Equal(-60, holder.Total(183));

        var ticks = new List<TimedEffects.Active>();
        var expired = new List<TimedEffects.Active>();
        holder.Advance(108.9, ticks, expired);
        Assert.Empty(expired);
        holder.Advance(109, ticks, expired);
        Assert.Equal(TempMods.PowerlessAbility, Assert.Single(expired).Target);
        Assert.False(holder.Has(TempMods.PowerlessAbility));
        Assert.Equal(-60, holder.Total(183));

        expired.Clear();
        holder.Advance(130, ticks, expired);
        Assert.Equal(183, Assert.Single(expired).Target);
        Assert.True(holder.IsEmpty);
        Assert.Empty(ticks);
    }

    [Fact]
    public void SameAbilityAgainReplacesItsTempmods()
    {
        // Crippling Shot again 2 s later (old_world Vector 25521): the old three off, three new on with the full time
        var holder = new TimedEffects.Holder();
        holder.Apply(450, new List<TimedEffects.Active> { Mod(877, -113, 8), Mod(878, -113, 8), Mod(115, -81, 8) }, 100);
        List<TimedEffects.Active> replaced = holder.Apply(450,
            new List<TimedEffects.Active> { Mod(877, -112, 8), Mod(878, -112, 8), Mod(115, -75, 8) }, 102);
        Assert.Equal(3, replaced.Count);
        Assert.Equal(-112, holder.Total(877));
        Assert.Equal(-75, holder.Total(115));

        var expired = new List<TimedEffects.Active>();
        holder.Advance(108.5, new List<TimedEffects.Active>(), expired);
        Assert.Empty(expired); // the refresh restarted the time
        holder.Advance(110, new List<TimedEffects.Active>(), expired);
        Assert.Equal(3, expired.Count);
    }

    [Fact]
    public void DifferentAbilitiesStack()
    {
        // Neuro Dart and Deadly Throw both slow (115 -57 each); UI Lag and Vector both lower ViralDeflection
        var holder = new TimedEffects.Holder();
        holder.Apply(141, new List<TimedEffects.Active> { Mod(115, -57, 15) }, 0);
        holder.Apply(279, new List<TimedEffects.Active> { Mod(115, -57, 15) }, 1);
        holder.Apply(97, new List<TimedEffects.Active> { Mod(183, -17, 27) }, 2);
        holder.Apply(355, new List<TimedEffects.Active> { Mod(183, -60, 30) }, 3);
        Assert.Equal(-114, holder.Total(115));
        Assert.Equal(-77, holder.Total(183));
        holder.RemoveParent(97);
        Assert.Equal(-60, holder.Total(183));
    }

    // ---- damage over time ----

    [Fact]
    public void DotTicksMatchRetail()
    {
        // tempmods.py ViralDOT: int(Value / Duration x 2.5 + 0.5); retail's ticks were these plus the shown resistance
        Assert.Equal(72, TempMods.DotTick(866, 30, 2.5));  // Code Infection 4: 91 done, -19
        Assert.Equal(38, TempMods.DotTick(450, 30, 2.5));  // Infect Area 1: 48, -10
        Assert.Equal(52, TempMods.DotTick(624, 30, 2.5));  // Infect Area 2: 66, -14
        Assert.Equal(60, TempMods.DotTick(716, 30, 2.5));  // Code Infection 3: 76, -16
        Assert.Equal(71, TempMods.DotTick(424, 15, 2.5));  // Plague Zone 2.0: 69, +2
        Assert.Equal(73, TempMods.DotTick(880, 30, 2.5));  // Code Infection 4 (crashover2k_008): 84, -11
        Assert.Equal(10, TempMods.DotTick(4, 0, 2.5));     // Duration 0 counts as 1
        Assert.Equal(20, TempMods.HotAmount(480, 500, 30));
        Assert.Equal(0, TempMods.HotAmount(500, 500, 30));
    }

    [Fact]
    public void DotTicksEveryIntervalAndNotAtTheEnd()
    {
        // Plague Zone 2.0: 15 s, ticks at +2.5 .. +12.5 (five, as retail), none at +15
        var holder = new TimedEffects.Holder();
        TimedEffects.Active dot = Mod(TempMods.DOTPlaceHolderAbility, 424, 15, TempMods.Proc.Dot, TempMods.KindLinked);
        holder.Apply(466, new List<TimedEffects.Active> { dot }, 1000);
        var ticks = new List<TimedEffects.Active>();
        var expired = new List<TimedEffects.Active>();
        holder.Advance(1002.4, ticks, expired);
        Assert.Empty(ticks);
        holder.Advance(1002.5, ticks, expired);
        Assert.Single(ticks);
        holder.Advance(1016, ticks, expired);
        Assert.Equal(5, ticks.Count);
        Assert.Single(expired);
        Assert.All(ticks, t => Assert.Equal(71, t.TickDamage));

        // 30 s viruses: eleven ticks
        holder.Apply(474, new List<TimedEffects.Active> { Mod(TempMods.DOTPlaceHolderAbility, 866, 30, TempMods.Proc.Dot, TempMods.KindLinked) }, 0);
        ticks.Clear();
        holder.Advance(31, ticks, expired);
        Assert.Equal(11, ticks.Count);
    }

    // ---- control immunity ----

    [Fact]
    public void StunGivesImmunityAndASecondStunFails()
    {
        // tempmods.py ApplyStunWithTimer: a stun puts ControlImmunityTimer (734 -> 681) on for its own time, and no stun
        // lands while it is on ("Stun stacking detected")
        var subject = new RecordingSubject();
        double now = 5000;
        List<TimedEffects.Active> first = TimedEffects.Apply(subject, TempMods.Retail[243], now);
        Assert.Single(first);
        Assert.NotNull(subject.LastImmunity);
        Assert.Equal(4f, subject.LastImmunity!.Seconds);
        Assert.Equal(TempMods.ISDrainTargetAbility, subject.LastImmunity.Target);
        Assert.Equal(TempMods.KindPlain, subject.LastImmunity.Kind);
        Assert.True(subject.Effects.Has(TempMods.StunAbility));

        List<TimedEffects.Active> second = TimedEffects.Apply(subject, TempMods.Retail[333], now + 1);
        Assert.Empty(second);

        // Vector during the immunity: its Powerless is refused, its ViralDeflection lands
        List<TimedEffects.Active> vector = TimedEffects.Apply(subject, TempMods.Retail[355], now + 2);
        Assert.Equal(183, Assert.Single(vector).Target);

        // Freeze Area 2.0's own 12 s; Vector's whole seconds of 8.94
        Assert.Equal(12f, TimedEffects.ImmunityFor(TempMods.Retail[479], Mod(TempMods.MezAbility, 0, 10.02f, TempMods.Proc.State)).Seconds);
        Assert.Equal(8f, TimedEffects.ImmunityFor(TempMods.Retail[355], Mod(TempMods.PowerlessAbility, 0, 8.94f, TempMods.Proc.State)).Seconds);
    }

    [Fact]
    public void ServerTickTicksAndExpires()
    {
        var subject = new RecordingSubject();
        double now = 20000;
        TimedEffects.Apply(subject, TempMods.Retail[466], now, rng: new Random(3));
        TimedEffects.RunDue(now + 2.6);
        Assert.Single(subject.Ticks);
        Assert.InRange(subject.Ticks[0], 71, 74); // 424-444 over 15 s
        TimedEffects.RunDue(now + 16);
        Assert.Equal(5, subject.Ticks.Count);
        Assert.Single(subject.EndedMods);
        Assert.True(subject.Effects.IsEmpty);
    }

    [Fact]
    public void TickThatCannotLandEndsTheDot()
    {
        // The caster's fight with the NPC is over: the damage over time ends with that tick
        var subject = new RecordingSubject { LandTicks = false };
        double now = 30000;
        TimedEffects.Apply(subject, TempMods.Retail[472], now);
        TimedEffects.RunDue(now + 3);
        Assert.Single(subject.Ticks);
        Assert.Single(subject.EndedMods);
        Assert.True(subject.Effects.IsEmpty);
        TimedEffects.RunDue(now + 6);
        Assert.Single(subject.Ticks);
    }

    private sealed class RecordingSubject : TimedEffects.Subject
    {
        public TimedEffects.Active? LastImmunity;
        public readonly List<int> Ticks = new List<int>();
        public readonly List<TimedEffects.Active> EndedMods = new List<TimedEffects.Active>();
        public bool LandTicks = true;

        internal override WorldClient Context => null!;

        internal override void Applied(TempMods.Effect effect, List<TimedEffects.Active> added, TimedEffects.Active immunity,
            List<TimedEffects.Active> replaced)
        {
            LastImmunity = immunity ?? LastImmunity;
        }

        internal override bool Ticked(TimedEffects.Active mod, int damage)
        {
            Ticks.Add(damage);
            return LandTicks;
        }

        internal override void Ended(List<TimedEffects.Active> ended)
        {
            EndedMods.AddRange(ended.FindAll(m => m.Proc == TempMods.Proc.Dot));
        }
    }

    // ---- packets against retail ----

    [Fact]
    public void TempmodRecordsMatchRetail()
    {
        // Code Infection 4 on Morpheuzzz (morpheuzzz_001 2394): 80 bc 1d 00 18 00 00 da 01 00 00 85 04 62 03 00 00 00 00 00 f0 41
        var dot = new TimedEffects.Active
        {
            Parent = 474, Kind = TempMods.KindLinked, Target = TempMods.DOTPlaceHolderAbility, Value = 866, Seconds = 30, RecordId = 0x18
        };
        Assert.Equal("80bc1d00180000da010000850462030000000000f041", Hex(PlayerEffects.Record(dot, false)));

        // UI Lag 1.0 (2490): 80 bc 15 00 19 00 00 61 00 00 00 08 04 f6 ff ff ff 00 00 00 d8 41 ... 56 01 f2 ff ff ff
        var lag = new TimedEffects.Active { Parent = 97, Kind = TempMods.KindStat, Target = 1032, Value = -10, Seconds = 27, RecordId = 0x19 };
        Assert.Equal("80bc15001900006100000008" + "04f6ffffff000000d841", Hex(PlayerEffects.Record(lag, false)));
        TempMods.Mod viral = TempMods.Retail[97].Mods[3];
        Assert.Equal((342, -14, 27f), (viral.Target, viral.MinValue, viral.Seconds));

        // Pistol Aerial's stun and the stun's own record (crashover2k proxy_003 8687):
        // 80 bc 11 00 03 05 00 f3 00 00 00 66 00 00 00 00 00 00 00 00 80 40 / 80 bc 1d 00 04 05 00 66 00 00 00 00 00 ..
        var stun = new TimedEffects.Active
        {
            Parent = 243, Kind = TempMods.KindState, Target = TempMods.StunAbility, Seconds = 4, RecordId = 0x503, LinkedRecordId = 0x504
        };
        Assert.Equal("80bc11000305" + "00f3000000" + "6600" + "00000000" + "00" + "00008040", Hex(PlayerEffects.Record(stun, false)));
        Assert.Equal("80bc1d000405" + "0066000000" + "0000" + "00000000" + "00" + "00000000", Hex(PlayerEffects.LinkedRecord(stun, false)));
        // ... and ControlImmunityTimer: 80 bc 01 00 06 05 00 de 02 00 00 a9 02 00 00 00 00 00 00 00 80 40
        TimedEffects.Active immunity = TimedEffects.ImmunityFor(TempMods.Retail[243], stun);
        immunity.Parent = TempMods.ControlImmunityTimerAbility;
        immunity.RecordId = 0x506;
        Assert.Equal("80bc01000605" + "00de020000" + "a902" + "00000000" + "00" + "00008040", Hex(PlayerEffects.Record(immunity, false)));

        // MezzingTerm's addTempMod(MezAbility, MovementSpeedAbility, 0, 2) (tempmods.py 862) as retail sent it (Rajko
        // MaraCThenTimeDOut 63411), and DeflectVirus1's 0.2 s PlayFX with FX_VIRUSCAST_SPLIT_DEFLECT_VIRUS_DEFLECTVIRUS1
        // (virii.py 31; crashover2k proxy_024 13034)
        var mezSpeed = new TimedEffects.Active { Parent = TempMods.MezAbility, Kind = TempMods.KindPlain, Target = TempMods.MovementSpeedAbility, Seconds = 2, RecordId = 0xdc };
        Assert.Equal("80bc0100dc0000550000007300000000000000000040", Hex(PlayerEffects.Record(mezSpeed, false)));
        var deflect = new TimedEffects.Active { Parent = 378, Kind = TempMods.KindFx, Target = 378, Value = 0x28000820, Seconds = 0.2f, RecordId = 0xd8 };
        Assert.Equal("80bc0900d800007a0100007a0120080028" + "00cdcc4c3e", Hex(PlayerEffects.Record(deflect, false)));

        // Pistol Whip's blind and Blind's own CombatTactics -30 (proxy_020 143154): 80 bc 1d 00 2c 02 00 5a 00 00 00 a3 00 e2 ff ff ff
        var blind = new TimedEffects.Active { Parent = 501, Kind = TempMods.KindState, Target = TempMods.BlindAbility, Seconds = 10.08f, LinkedRecordId = 0x22c };
        Assert.Equal("80bc1d002c02" + "005a000000" + "a300" + "e2ffffff" + "00" + "00000000", Hex(PlayerEffects.LinkedRecord(blind, false)));
    }

    [Fact]
    public void LogLinesMatchRetail()
    {
        // "Your Plague Zone 2.0 infects Zombie Lurch!" (actions_combat 16472)
        Assert.Equal("8167" + "1700" + "2700" + "190000c6" + "01" + "d2010000" + "00000000" + "3600" + "00" + "08" +
                     "0e00" + Ascii("AfterWhoruNeo") + "00" + "0d00" + Ascii("Zombie Lurch") + "00" + "0200" + "a8010000" + "00000000",
            Hex(AbilityMessages.AboutAbility(AbilityMessages.InfectsCaster, true, 466, "AfterWhoruNeo", "Zombie Lurch", 424, 0)));
        // "Your Plague Zone 2.0 does 69 damage to Zombie Lurch." (16649)
        Assert.Equal("8167" + "1700" + "2700" + "fa00007c" + "01" + "00100000" + "00480780" + "3600" + "00" + "07" +
                     "0e00" + Ascii("AfterWhoruNeo") + "00" + "0d00" + Ascii("Zombie Lurch") + "00" + "0200" + "45000000" + "02000000",
            Hex(AbilityMessages.Damage(true, 466, "AfterWhoruNeo", "Zombie Lurch", 69, 2)));
        // "KaosDevil's Code Infection 4 does 91 damage to you." (morpheuzzz_001 2544)
        Assert.Equal("8167" + "1700" + "2300" + "f900007c" + "00" + "00100000" + "00680780" + "3000" + "00" + "07" +
                     "0a00" + Ascii("KaosDevil") + "00" + "0b00" + Ascii("Morpheuzzz") + "00" + "0200" + "5b000000" + "edffffff",
            Hex(AbilityMessages.Damage(false, 474, "KaosDevil", "Morpheuzzz", 91, -19)));
        // "KaosDevil's UI Lag 1.0 infects you!" [-14, 0] (2490)
        Assert.Equal("8167" + "1700" + "2300" + "1a0000c6" + "00" + "61000000" + "00000000" + "3000" + "00" + "08" +
                     "0a00" + Ascii("KaosDevil") + "00" + "0b00" + Ascii("Morpheuzzz") + "00" + "0200" + "f2ffffff" + "00000000",
            Hex(AbilityMessages.AboutAbility(AbilityMessages.InfectsTarget, false, 97, "KaosDevil", "Morpheuzzz", -14, 0)));
        // "You are no longer Powerless." (2406)
        Assert.Equal("8167" + "1700" + "1a00" + "3c00007a" + "01" + "00000000" + "00000000" + "2700" + "00" + "08" +
                     "0100" + "00" + "0b00" + Ascii("Morpheuzzz") + "00" + "0100" + "4800007a",
            Hex(AbilityMessages.StateEnd(TempMods.PowerlessAbility, "Morpheuzzz")));
        Assert.Equal(0x80000400u, AbilityMessages.AbilityGameObject(1)); // abilityIDs.csv GOID of ability 1
    }

    [Fact]
    public void SelfTickUpdateMatchesRetail()
    {
        // Own view on a tick (morpheuzzz_001 2699): 02 00 02 80 80 80 c0 77 09 30 30 04 00 28 07
        Assert.Equal("020002808080c0" + "7709" + "30" + "30040028" + "07",
            Hex(PlayerEffects.BuildSelfTickUpdate(0x0977, TempMods.VirusCrashFx, 7)));
    }

    // ---- a player getting tempmods ----

    private static WorldClient NewClient()
    {
        WorldClient client = (WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    private static List<string> Rpcs(WorldClient client)
    {
        List<string> list = new List<string>();
        foreach (SequencedMessage m in client.messageQueue.RPCMessagesQueue)
        {
            list.Add(Hex(m.content).Substring(2));
        }
        client.messageQueue.RPCMessagesQueue.Clear();
        client.messageQueue.ObjectMessagesQueue.Clear();
        return list;
    }

    [Fact]
    public void PlayerGetsRecordsOnAndOff()
    {
        // Vector on Morpheuzzz (morpheuzzz_001 1829): Powerless 7.44 s (Determination had cut it), ViralDeflection -60 for 30 s
        var vector = new TempMods.Effect
        {
            Ability = 355, Virus = true, Mods = new[]
            {
                new TempMods.Mod { Kind = TempMods.KindState, Target = TempMods.PowerlessAbility, Seconds = 7.44f, Proc = TempMods.Proc.State },
                new TempMods.Mod { Kind = TempMods.KindStat, Target = 183, MinValue = -60, MaxValue = -60, Seconds = 30, Proc = TempMods.Proc.Stat },
            }
        };
        WorldClient client = NewClient();
        client.playerData.nextBuffRecordId = 0x14;
        PlayerEffects effects = TimedEffects.ForPlayer(client);
        double now = TimeUtils.GetSimTimeSeconds();
        TimedEffects.Apply(effects, vector, now, null, "KaosDevil");
        List<string> on = Rpcs(client);
        // Retail's four records, byte for byte, in retail's order
        Assert.Equal("80bc01001500" + "00de020000" + "a902" + "00000000" + "00" + "0000e040", on[0]); // ControlImmunityTimer 7 s
        Assert.Equal("80bc11001400" + "0063010000" + "6000" + "00000000" + "00" + "7b14ee40", on[1]); // Powerless 7.44 s
        Assert.Equal("80bc1d001600" + "0060000000" + "0000" + "00000000" + "00" + "00000000", on[2]); // Powerless's own record
        Assert.Equal("80bc15001700" + "0063010000" + "b700" + "c4ffffff" + "00" + "0000f041", on[3]); // ViralDeflection -60, 30 s
        Assert.Contains(on, r => r.StartsWith("8167") && r.Substring(12, 8) == "c82000c6"); // "immune ... for 7 seconds"
        Assert.DoesNotContain(on, r => r.StartsWith("8167") && r.Substring(12, 8) == "4d00007a"); // a virus: no "rendered you powerless"
        Assert.Contains(on, r => r.StartsWith("8167") && r.Substring(12, 8) == "1a0000c6"); // "... infects you!"

        // 7.44 s later (retail 09:45:40): Powerless, its record and the immunity off, "You are no longer Powerless."
        TimedEffects.RunDue(now + 7.5);
        List<string> off = Rpcs(client);
        Assert.Contains("80bc11001400" + "0063010000" + "6000" + "00000000" + "01" + "7b14ee40", off);
        Assert.Contains("80bc1d001600" + "0060000000" + "0000" + "00000000" + "01" + "00000000", off);
        Assert.Contains("80bc01001500" + "00de020000" + "a902" + "00000000" + "01" + "0000e040", off);
        Assert.Contains(off, r => r.StartsWith("8167") && r.EndsWith("4800007a"));
        Assert.True(effects.Effects.HasParent(355));

        // Death takes the rest off
        AbilityBuffs.RemoveAll(client);
        List<string> death = Rpcs(client);
        Assert.Equal("80bc15001700" + "0063010000" + "b700" + "c4ffffff" + "01" + "0000f041", Assert.Single(death));
        Assert.True(effects.Effects.IsEmpty);
    }

    [Fact]
    public void PlayerTakesDamageOverTime()
    {
        WorldClient client = NewClient();
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Health.setValue((UInt16)2500);
        client.playerInstance.CharacterName.setValue("Morpheuzzz");
        PlayerEffects effects = TimedEffects.ForPlayer(client);
        double now = TimeUtils.GetSimTimeSeconds();
        var infection = new TempMods.Effect
        {
            Ability = 474, Virus = true,
            Mods = new[] { new TempMods.Mod { Kind = TempMods.KindLinked, Target = TempMods.DOTPlaceHolderAbility, MinValue = 866, MaxValue = 866, Seconds = 30, Proc = TempMods.Proc.Dot } }
        };
        TimedEffects.Apply(effects, infection, now, null, "KaosDevil");
        client.messageQueue.RPCMessagesQueue.Clear();

        TimedEffects.RunDue(now + 2.5);
        Assert.Equal(2500 - 72, NumericalUtils.ByteArrayToUint16(client.playerInstance.Health.getValue(), 1));
        List<string> rpcs = new List<string>();
        foreach (SequencedMessage m in client.messageQueue.RPCMessagesQueue)
        {
            rpcs.Add(Hex(m.content).Substring(2));
        }
        // "KaosDevil's Code Infection 4 does 72 damage to you." (no resistances here: [72, 0])
        Assert.Equal(Hex(AbilityMessages.Damage(false, 474, "KaosDevil", "Morpheuzzz", 72, 0)), Assert.Single(rpcs));
        TimedEffects.Clear(effects);
        client.messageQueue.RPCMessagesQueue.Clear();
    }
}
