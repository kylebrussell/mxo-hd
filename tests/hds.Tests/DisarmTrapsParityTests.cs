using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

public class DisarmTrapsParityTests
{
    private const ushort DisarmTraps = UtilityAbilities.DisarmTraps; // 119

    private static WorldClient Client(uint charId = 110, bool loaded = true)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)10);
        client.playerInstance.Health.setValue((ushort)3000);
        client.playerInstance.MaxHealth.setValue((ushort)3000);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        if (loaded)
            AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (DisarmTraps, 10) });
        return client;
    }

    [Fact]
    public void DisarmRefusalEnforcesLoadedAndOutsideCombatBeforeToolCheck()
    {
        var unloaded = Client(loaded: false);
        Assert.Equal(AbilityRefusals.NotLoaded, UtilityAbilities.DisarmRefusal(unloaded, out bool missingUnloaded));
        Assert.False(missingUnloaded);

        var fighting = Client(loaded: true);
        var combat = CombatManager.GetHandler(fighting);
        combat.isCombatRunning = true;
        try
        {
            Assert.Equal(AbilityRefusals.MustBeOutsideCombat, UtilityAbilities.DisarmRefusal(fighting, out bool missingCombat));
            Assert.False(missingCombat);
        }
        finally
        {
            combat.isCombatRunning = false;
        }
    }

    [Fact]
    public void DisarmTrapsQaControlsOnlyContestRollInDisposableFixture()
    {
        var prevConfig = Store.config;
        string? oldMode = Environment.GetEnvironmentVariable(DisarmTrapsQa.Mode);
        string? oldDev = Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS");
        try
        {
            var cfg = (ServerConfig)RuntimeHelpers.GetUninitializedObject(typeof(ServerConfig));
            typeof(ServerConfig).GetField("_dbParams", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(cfg, new DbParams { DatabaseName = "mxotest_disarm_traps" });
            Store.config = cfg;
            Environment.SetEnvironmentVariable(DisarmTrapsQa.Mode, "1");
            Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS", "1");

            var csr = Client(110);
            var neo = Client(2);

            Assert.Null(DisarmTrapsQa.RollFor(csr));
            Assert.Contains("set to 50", DisarmTrapsQa.Dev(csr, new[] { "50" }));
            Assert.Equal(50, DisarmTrapsQa.RollFor(csr));
            Assert.Null(DisarmTrapsQa.RollFor(neo));
            Assert.Contains("Usage:", DisarmTrapsQa.Dev(csr, new[] { "100" }));
            Assert.Contains("cleared", DisarmTrapsQa.Dev(csr, new[] { "off" }));
            Assert.Null(DisarmTrapsQa.RollFor(csr));
        }
        finally
        {
            DisarmTrapsQa.ResetForTests();
            Store.config = prevConfig;
            Environment.SetEnvironmentVariable(DisarmTrapsQa.Mode, oldMode);
            Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS", oldDev);
        }
    }

    [Fact]
    public void DisarmTrapsRank10ContestMatchesShippedScriptAndModePlan()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-disarm-traps").Plan;
        Assert.Contains(DisarmTraps, plan.Abilities);
        Assert.Equal(new[] { DisarmTraps }, plan.HotbarAbilities);
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(10));

        // Rank 10, toolBonus 0, difficulty 0 -> delta = roll - 40.
        Assert.Equal(UtilityAbilities.DisarmResult.Critical, UtilityAbilities.DisarmTrap(true, 10, 0, 0, detected: false, roll: 99));
        Assert.Equal(UtilityAbilities.DisarmResult.Critical, UtilityAbilities.DisarmTrap(true, 10, 0, 0, detected: true, roll: 10));
        Assert.Equal(UtilityAbilities.DisarmResult.Failed, UtilityAbilities.DisarmTrap(true, 10, 0, 0, detected: true, roll: 30));
        Assert.Equal(UtilityAbilities.DisarmResult.Disarmed, UtilityAbilities.DisarmTrap(true, 10, 0, 0, detected: true, roll: 50));
        Assert.Equal(UtilityAbilities.DisarmResult.NotTrapped, UtilityAbilities.DisarmTrap(false, 10, 0, 0, detected: true, roll: 50));
        Assert.Equal((ushort)10, AbilityCosts.Cost(DisarmTraps));
        Assert.Equal(2f, AbilityCatalog.Find(DisarmTraps)!.CastSeconds);
    }
}
