using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using hds;
using hds.world.Structures;

namespace hds.Tests;

public class BackgroundTimersAndConsoleSocketTests
{
    [Fact]
    public void SubwaySharedSchedulerRegistersOnceTicksAndUnregistersCleanly()
    {
        var car1 = new Subway(new StaticWorldObject
        {
            mxoStaticId = 9001,
            metrId = 1,
            quat = "0000000000000000000000000000803f"
        });
        var car2 = new Subway(new StaticWorldObject
        {
            mxoStaticId = 9002,
            metrId = 1,
            quat = "0000000000000000000000000000803f"
        });
        car1.secondsLeft = 20;
        car2.secondsLeft = 25;

        try
        {
            car1.StartCountdown();
            car1.StartCountdown(); // idempotent registration
            car2.StartCountdown();

            int c1Before = car1.secondsLeft;
            int c2Before = car2.secondsLeft;
            Subway.TickActiveSubways();
            Assert.True(car1.secondsLeft <= c1Before - 1);
            Assert.True(car2.secondsLeft <= c2Before - 1);

            car1.StopCountdown();
            car1.StopCountdown(); // idempotent removal
            car2.StopCountdown();

            int c1Stopped = car1.secondsLeft;
            int c2Stopped = car2.secondsLeft;
            Subway.TickActiveSubways();
            Assert.Equal(c1Stopped, car1.secondsLeft);
            Assert.Equal(c2Stopped, car2.secondsLeft);
        }
        finally
        {
            car1.StopCountdown();
            car2.StopCountdown();
        }
    }

    [Fact]
    public void DoorLifecycleTickFastPathHandlesEmptyOpenDoorsCleanly()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.messageQueue = new MessageQueue();

        DoorLifecycle.Tick(client, 100.0);

        Assert.Empty(client.messageQueue.ObjectMessagesQueue);
        Assert.Empty(client.messageQueue.RPCMessagesQueue);
    }

    [Fact]
    public void MobLifecycleTickUnspawnsThenRespawnsDeadMob()
    {
        var mob = new Mob();
        mob.setName("TimerTestMob");
        mob.setIsSpawned(true);
        mob.setIsDead(true);

        double t0 = 1000.0;
        MobLifecycle.Died(mob, t0);

        // Past corpse window -> unspawned
        MobLifecycle.Tick(t0 + MobLifecycle.CorpseSeconds + 1.0);
        Assert.False(mob.getIsSpawned());

        // Past respawn window -> respawned and removed from dead list
        double respawnAfter = Math.Max(MobLifecycle.RespawnSeconds, MobLifecycle.CorpseSeconds);
        MobLifecycle.Tick(t0 + respawnAfter + 1.0);
        Assert.True(mob.getIsSpawned());
        Assert.False(mob.getIsDead());
    }

    [Fact]
    public void ConsoleSocketHandlesClientCommandAndStopsCleanlyWithoutThrowing()
    {
        var console = new ConsoleSocket(0);
        console.startServer();
        try
        {
            // Wait briefly for the listener thread to bind the ephemeral port.
            var sw = Stopwatch.StartNew();
            while (console.BoundPort == 0 && sw.ElapsedMilliseconds < 2000)
            {
                Thread.Sleep(10);
            }
            Assert.True(console.BoundPort > 0);

            using (var client = new TcpClient())
            {
                client.Connect(IPAddress.Loopback, console.BoundPort);
                using NetworkStream stream = client.GetStream();
                stream.ReadTimeout = 2000;
                stream.WriteTimeout = 2000;

                byte[] req = Encoding.ASCII.GetBytes("ping");
                stream.Write(req, 0, req.Length);

                byte[] buf = new byte[128];
                int n = stream.Read(buf, 0, buf.Length);
                string reply = Encoding.ASCII.GetString(buf, 0, n);
                Assert.Equal("Unrecognized command: ping", reply);
            }
        }
        finally
        {
            console.stopServer();
            console.stopServer(); // idempotent stop
        }

        // Listener thread must terminate cleanly on stopServer() without crashing.
        var exitSw = Stopwatch.StartNew();
        while (console.IsListenerAlive && exitSw.ElapsedMilliseconds < 2000)
        {
            Thread.Sleep(10);
        }
        Assert.False(console.IsListenerAlive);
    }
}
