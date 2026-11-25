using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using NativeQa;
using Xunit;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class SixthSenseParityTests
{
    private static WorldClient Client(uint charId = 110)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        client.playerData.setEntityId(charId);
        client.playerData.setOnWorld(true);
        client.playerData.isDead = false;
        return client;
    }

    [Fact]
    public void SixthSenseQaEnforcesDisposableFixtureGateAndParsesRolls()
    {
        string? prevMode = Environment.GetEnvironmentVariable(SixthSenseQa.Mode);
        string? prevDev = Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS");
        var prevConfig = Store.config;
        try
        {
            SixthSenseQa.ResetForTests();
            var csr = Client(110);
            var other = Client(6114);

            Assert.False(SixthSenseQa.Allowed("0", "1", "mxotest_qa", 110, true, false));
            Assert.False(SixthSenseQa.Allowed("1", "0", "mxotest_qa", 110, true, false));
            Assert.False(SixthSenseQa.Allowed("1", "1", "mxo", 110, true, false));
            Assert.False(SixthSenseQa.Allowed("1", "1", "mxotest_qa", 6114, true, false));
            Assert.True(SixthSenseQa.Allowed("1", "1", "mxotest_qa", 110, true, false));

            Environment.SetEnvironmentVariable(SixthSenseQa.Mode, "1");
            Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS", "1");
            var cfg = (ServerConfig)RuntimeHelpers.GetUninitializedObject(typeof(ServerConfig));
            typeof(ServerConfig).GetField("_dbParams", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(cfg, new DbParams { DatabaseName = "mxotest_sixth_sense" });
            Store.config = cfg;

            Assert.Contains("requires", SixthSenseQa.Dev(other, new[] { "5" }));
            Assert.Equal("Sixth Sense QA roll set to 5.", SixthSenseQa.Dev(csr, new[] { "5" }));
            Assert.Equal(5, SixthSenseQa.RollFor(csr));
            Assert.Null(SixthSenseQa.RollFor(other));
            Assert.Contains("Usage:", SixthSenseQa.Dev(csr, new[] { "100" }));
            Assert.Equal("Sixth Sense QA roll override cleared.", SixthSenseQa.Dev(csr, new[] { "off" }));
            Assert.Null(SixthSenseQa.RollFor(csr));
        }
        finally
        {
            SixthSenseQa.ResetForTests();
            Environment.SetEnvironmentVariable(SixthSenseQa.Mode, prevMode);
            Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS", prevDev);
            Store.config = prevConfig;
        }
    }

    [Fact]
    public void SixthSenseRank10SeparatesResistFromDetectAndFitsLevel16Memory()
    {
        var plan = NativeQaModes.Find("ability-parity-sixth-sense").Plan;
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(16), $"Memory {mem} must fit level 16 ({AbilityMemory.ForLevel(16)})");
        Assert.Equal("1", plan.EnvironmentOverrides[SixthSenseQa.Mode]);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.DisengageMode]);

        // At rank 10 (stat 646 = 40): unstealthed (threshold 50) resists roll 5 (45 <= 50) and is detected at roll 20 (60 > 50);
        // stealthed (threshold 0 per shipped misc.py:100) is detected even at roll 5 (45 > 0).
        Assert.False(SixthSense.Detects(5, 40, 0));
        Assert.True(SixthSense.Detects(20, 40, 0));
        Assert.True(SixthSense.Detects(5, 40, 1));
    }
}
