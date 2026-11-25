using hds;
using hds.shared;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class JumpRecordTests
{
    const string RetailStart="80c200000000ce42edc00000000000c05740000000003feecf40f7021e00010000f0204601bf97be00";
    const string RetailCancel="80c4000000a0d28ff0c00000000000c0574000000080e8b9d0404f0314000500";
    [Fact] public void LiteralRetailRecordsKeepNavigationSeparateFromTickSample()
    {
        var start=Convert.FromHexString(RetailStart);var cancel=Convert.FromHexString(RetailCancel);
        Assert.True(JumpRecords.TryStart(start,out var s));Assert.True(JumpRecords.TryCancel(cancel,out var c));
        Assert.Equal(0x00be97bfu,s.TimeStamp);Assert.Equal(10300f,BitConverter.Int32BitsToSingle(unchecked((int)s.PeakBits)));
        Assert.Equal((ushort)759,s.Destination.Sector);Assert.Equal((ushort)30,s.Destination.Prefab);Assert.Equal((ushort)1,s.Destination.NavPoly);
        Assert.Equal((ushort)847,c.Destination.Sector);Assert.Equal((ushort)20,c.Destination.Prefab);Assert.Equal((ushort)5,c.Destination.NavPoly);
        Assert.Equal(start,JumpRecords.Encode(s));Assert.Equal(cancel,JumpRecords.Encode(c));
    }
    [Fact] public void RawFloatingBitsAndFullWidthFieldsAreLosslessWithoutNewAdmissionRules()
    {
        var destination=new JumpRecords.Destination(0x7ff8000000001234,0x8000000000000000,0xfff0000000000000,65535,65534,65533);
        var s=new JumpRecords.Start(destination,0x7fc01234,0xfe,uint.MaxValue);
        Assert.True(JumpRecords.TryStart(JumpRecords.Encode(s),out var actual));Assert.Equal(s,actual);
        var c=new JumpRecords.Cancel(destination);Assert.True(JumpRecords.TryCancel(JumpRecords.Encode(c),out var other));Assert.Equal(c,other);
    }
    [Fact] public void ExactFullAndBodyLengthsRejectTruncationTrailingDataAndWrongHeaders()
    {
        var start=Convert.FromHexString(RetailStart);var cancel=Convert.FromHexString(RetailCancel);
        for(int i=0;i<start.Length;i++)Assert.False(JumpRecords.TryStart(start.AsSpan(0,i),out _));
        for(int i=0;i<cancel.Length;i++)Assert.False(JumpRecords.TryCancel(cancel.AsSpan(0,i),out _));
        Assert.False(JumpRecords.TryStart(start.Concat(new byte[]{0}).ToArray(),out _));Assert.False(JumpRecords.TryCancel(cancel.Concat(new byte[]{0}).ToArray(),out _));
        Assert.True(JumpRecords.TryStartBody(start.AsSpan(2),out _));Assert.True(JumpRecords.TryCancelBody(cancel.AsSpan(2),out _));
        Assert.False(JumpRecords.TryStartBody(start,out _));Assert.False(JumpRecords.TryCancelBody(cancel,out _));
        start[1]=0xc4;cancel[0]=0;Assert.False(JumpRecords.TryStart(start,out _));Assert.False(JumpRecords.TryCancel(cancel,out _));
    }
    [Theory]
    [InlineData(0u,"80c300000000")]
    [InlineData(0xffffffffu,"80c3ffffffff")]
    [InlineData(0x80000000u,"80c300000080")]
    public void ClockEchoKeepsWrappedAndHighSamples(uint sample,string literal)
    {
        Assert.Equal(Convert.FromHexString(literal),JumpRecords.EncodeClockEcho(sample));
        var start=new JumpRecords.Start(default,0,1,sample);
        Assert.True(JumpRecords.TryStart(JumpRecords.Encode(start),out var decoded));Assert.Equal(sample,decoded.TimeStamp);
    }
    [Fact]
    public void ImmediateSameFloorAndPolygonReportDoesNotMatchALaterOrAirborneCancel()
    {
        var client = new ClientData();
        client.setDistrictId(1);
        var origin = Coordinates(17100, 495, 6400);
        var takeoff = new JumpRecords.Takeoff(origin, 10, client);
        Assert.True(takeoff.Matches(Coordinates(17101, 495, 6400), 10, client));
        Assert.True(takeoff.Matches(Coordinates(17101, 495, 6400), 10.9f, client));
        Assert.False(takeoff.Matches(Coordinates(17101, 495, 6400), 11.01f, client));
        Assert.False(takeoff.Matches(Coordinates(17101, 505, 6400), 10.1f, client));
        Assert.False(takeoff.Matches(Coordinates(17110, 495, 6400), 10.1f, client));
        Assert.False(takeoff.Matches(Coordinates(17101, 495, 6400, 4), 10.1f, client));
        client.worldGeneration++;
        Assert.False(takeoff.Matches(Coordinates(17101, 495, 6400), 10.1f, client));

        static JumpRecords.Destination Coordinates(double x, double y, double z, ushort polygon = 1) =>
            new(unchecked((ulong)BitConverter.DoubleToInt64Bits(x)), unchecked((ulong)BitConverter.DoubleToInt64Bits(y)),
                unchecked((ulong)BitConverter.DoubleToInt64Bits(z)), 1250, 18, polygon);
    }

    [Fact]
    public void FlightExpiresAcrossClockWrapAndIgnoresALaterLife()
    {
        var data = new ClientData();
        data.setDistrictId(1);
        var flight = new JumpRecords.Flight(10, data, null);
        Assert.True(flight.IsCurrent(uint.MaxValue, data));
        Assert.True(flight.IsCurrent(10, data));
        Assert.False(flight.IsCurrent(11, data));
        data.lifeGeneration++;
        Assert.False(flight.IsCurrent(10, data));
    }

    static JumpRecords.Destination At(double x, double y, double z, ushort polygon = 0) =>
        new(unchecked((ulong)BitConverter.DoubleToInt64Bits(x)), unchecked((ulong)BitConverter.DoubleToInt64Bits(y)),
            unchecked((ulong)BitConverter.DoubleToInt64Bits(z)), 1250, 2, polygon);

    [Fact]
    public void LandingReportMatchesOnlyTheAcceptedDestinationNearTheDeadline()
    {
        var data = new ClientData();
        data.setDistrictId(1);
        const uint end = 100_000;
        var flight = new JumpRecords.Flight(end, data, null, null, (20950, 495, 6400));
        // Retail landing reports: 0 or 1 world unit off the destination, up to 1.054 s before JumpEndTime.
        Assert.True(flight.IsLandingReport(At(20951, 495, 6400), end - 1054));
        Assert.True(flight.IsLandingReport(At(20950, 495, 6399), end));
        Assert.True(flight.IsLandingReport(At(20950, 495, 6400), end - (uint)JumpRecords.LandingReportLeadMilliseconds));
        Assert.False(flight.IsLandingReport(At(20951, 495, 6400), end - (uint)JumpRecords.LandingReportLeadMilliseconds - 1));
        Assert.False(flight.IsLandingReport(At(20951, 495, 6400), end + 1));
        // Retail redirects near the deadline moved the target by 0.77 m or more.
        Assert.False(flight.IsLandingReport(At(20953, 495, 6400), end - 100));
        Assert.False(flight.IsLandingReport(At(20950, 498, 6400), end - 100));
        Assert.False(flight.IsLandingReport(At(20950, 495, 6477), end - 100));
        Assert.False(new JumpRecords.Flight(end, data, null).IsLandingReport(At(20950, 495, 6400), end - 100));
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(5000, false)]
    public void LandingReportNearTheDeadlineGetsNoRedirectReply(int remainingMs, bool landing)
    {
        var oldClient = Store.currentClient; var oldWorld = Store.world;
        try
        {
            var c = JumpClient(100);
            Store.currentClient = c;
            Store.world = (WorldServer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
            c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(20950, 495, 6400));
            uint now = (uint)Math.Round(TimeUtils.GetSimTimeSeconds() * 1000.0);
            c.playerData.PendingJump = new JumpRecords.Flight(now + (uint)remainingMs, c.playerData, null, null, (20950, 495, 6400));
            byte[] cancel = JumpRecords.Encode(new JumpRecords.Cancel(At(20951, 495, 6400)))[2..];
            new AbilityHandler().processHyperJumpCancel(ref cancel);
            Assert.Null(c.playerData.PendingJump);
            Assert.Empty(c.messageQueue.RPCMessagesQueue);
            if (landing) Assert.Empty(c.messageQueue.ObjectMessagesQueue);
            else Assert.Single(c.messageQueue.ObjectMessagesQueue);
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(c.playerInstance.Position.getValue(), ref x, ref y, ref z);
            Assert.Equal(landing ? 20950 : 20951, x);
            Assert.Equal(495, y);
            Assert.Equal(6400, z);
        }
        finally { Store.currentClient = oldClient; Store.world = oldWorld; }
    }

    [Fact]
    public void FullPeakRedirectOriginFollowsTheActiveFlightAcrossClockWrap()
    {
        var destination = new JumpRecords.Destination(unchecked((ulong)BitConverter.DoubleToInt64Bits(20950)),
            unchecked((ulong)BitConverter.DoubleToInt64Bits(495)), unchecked((ulong)BitConverter.DoubleToInt64Bits(6400)), 1250, 2, 0);
        uint start = uint.MaxValue - 999;
        var route = new JumpRecords.Route(start, (17100, 495, 6400), destination, 10300);
        const uint end = 3000;
        Assert.Equal((17100f, 495f, 6400f), route.PositionAt(start, end));
        Assert.Equal((19025f, 10795f, 6400f), route.PositionAt(1000, end));
        Assert.Equal((20950f, 495f, 6400f), route.PositionAt(end, end));
    }

    [Theory]
    [InlineData(10300f, 7846.40096, 9811)]
    [InlineData(245.828125f, 7872.00121, 9808)]
    public void TravelTimeMatchesRetailAtBothFullAndLowPeak(float peak, double distance, int measuredMilliseconds)
    {
        uint end = AbilityHandler.JumpTravelEndTime(100, peak, distance, hyperJump: true);
        Assert.InRange(end, (uint)(100000 + measuredMilliseconds - 40), (uint)(100000 + measuredMilliseconds + 40));
    }

    [Fact]
    public void OrdinaryAndNearVerticalStartsRetainTheirExistingTimer()
    {
        const float peak = 1000;
        Assert.Equal(AbilityHandler.JumpEndTime(100, peak),
            AbilityHandler.JumpTravelEndTime(100, peak, 8.45, hyperJump: true));
        Assert.Equal(AbilityHandler.JumpEndTime(100, peak),
            AbilityHandler.JumpTravelEndTime(100, peak, 1800, hyperJump: false));
    }

    [Theory]
    [InlineData(0, 0, false, 0xfffffff0u)]
    [InlineData(1, 1, false, 0x80000000u)]
    [InlineData(20, 0, true, 0u)]
    [InlineData(304, 284, true, 0xffffffffu)]
    [InlineData(65535, 65515, true, 0xffffffffu)]
    public void HyperJumpAloneDebitsIsAndCancelPreservesIt(int inner, int remaining, bool hyper, uint sample)
    {
        var oldClient=Store.currentClient;var oldWorld=Store.world;
        try
        {
            var c = JumpClient(inner);
            Store.currentClient=c;Store.world=(WorldServer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
            byte[] start=Convert.FromHexString(RetailStart)[2..],cancel=Convert.FromHexString(RetailCancel)[2..];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(start.AsSpan(35),sample);
            start[34] = hyper ? (byte)1 : (byte)0;
            if (hyper) c.playerData.passives.Loaded.Add(11, 10);
            var handler=new AbilityHandler();handler.ProcessJump(ref start);
            Assert.Equal((ushort)remaining,AbilityHandler.CurrentInnerStrength(c));
            var rpc=Assert.Single(c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>());
            Assert.Empty(c.messageQueue.ObjectMessagesQueue);
            var packet=new WorldPacket(c.playerData);
            Assert.True(packet.addRPCContent(rpc));
            Assert.True(packet.timed);
            byte[] movement=Assert.Single(packet.ObjectMessages).content;
            Assert.Same(rpc,Assert.Single(packet.RPCMessages));
            int mask=movement.AsSpan().IndexOf(Convert.FromHexString("0200030108008080bc"));Assert.True(mask>=0);
            int resource=mask+9+4+2+4+24;Assert.Equal(0x10,movement[resource]);
            Assert.Equal((ushort)remaining,BitConverter.ToUInt16(movement,resource+1));
            int snapshot = mask + 46;
            if (hyper)
            {
                Assert.Equal(Convert.FromHexString("020003090800"), movement.AsSpan(snapshot, 6).ToArray());
                Assert.Equal(17100f, BitConverter.ToSingle(movement, snapshot + 6));
                Assert.Equal(495f, BitConverter.ToSingle(movement, snapshot + 10));
                Assert.Equal(6400f, BitConverter.ToSingle(movement, snapshot + 14));
                Assert.Equal(0xff, movement[snapshot + 22]);
                Assert.Equal(Convert.FromHexString("0654657374657200"), movement.AsSpan(snapshot + 37, 8).ToArray());
                Assert.False(c.playerInstance.RealFirstName.isActive());
            }
            handler.processHyperJumpCancel(ref cancel);
            Assert.Equal((ushort)remaining,AbilityHandler.CurrentInnerStrength(c));
            Assert.Same(rpc,Assert.Single(c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()));
            Assert.Equal(new byte[]{0x80,0xc3}.Concat(BitConverter.GetBytes(sample)).ToArray(),rpc.content.Skip(1).ToArray());
            double x=0,y=0,z=0;NumericalUtils.LtVector3dToDoubles(c.playerInstance.Position.getValue(),ref x,ref y,ref z);
            JumpRecords.TryCancel(Convert.FromHexString(RetailCancel),out var expected);
            Assert.Equal(expected.Destination.X,x);Assert.Equal(expected.Destination.Y,y);Assert.Equal(expected.Destination.Z,z);
            var cancelView = Assert.Single(c.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>()).content;
            Assert.Equal(Convert.FromHexString(RetailCancel).AsSpan(2, 24).ToArray(), cancelView.AsSpan(31, 24).ToArray());
            if (hyper)
            {
                Assert.Equal(BitConverter.ToUInt32(movement, mask + 15), BitConverter.ToUInt32(cancelView, 27));
                Assert.True(JumpRecords.TryStart(Convert.FromHexString(RetailStart), out var accepted));
                float startX = BitConverter.ToSingle(cancelView, 6);
                Assert.InRange(startX, (float)accepted.Destination.X, 17100f);
                Assert.NotEqual((float)accepted.Destination.X, startX);
            }
            Assert.Equal(0x10, cancelView[^6]);
            Assert.Equal((ushort)remaining, BitConverter.ToUInt16(cancelView, cancelView.Length - 5));
            c.messageQueue = new();
            handler.processHyperJumpCancel(ref cancel);
            Assert.Empty(c.messageQueue.RPCMessagesQueue);
            Assert.Empty(c.messageQueue.ObjectMessagesQueue);
        }
        finally{Store.currentClient=oldClient;Store.world=oldWorld;}
    }

    [Theory]
    [InlineData(19, true, false, AbilityRefusals.InsufficientInnerStrength, 1u)]
    [InlineData(35, true, true, AbilityRefusals.MustBeOutsideCombat, 0u)]
    [InlineData(35, false, false, AbilityRefusals.NotLoaded, 0u)]
    public void RefusedHyperJumpKeepsIsAndPosition(int inner, bool loaded, bool combat, uint reason, uint deficit)
    {
        var previous = Store.currentClient;
        var c = JumpClient(inner);
        try
        {
            Store.currentClient = c;
            if (loaded) c.playerData.passives.Loaded.Add(11, 10);
            CombatManager.GetHandler(c).isCombatRunning = combat;
            byte[] start = Convert.FromHexString(RetailStart)[2..];
            byte[] position = c.playerInstance.Position.getValue().ToArray();
            new AbilityHandler().ProcessJump(ref start);
            var rpc = Assert.Single(c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>());
            Assert.True(AbilityRefusals.TryDecode(rpc.content.AsSpan(1), out var refusal));
            Assert.Equal(11u, refusal.AbilityID);
            Assert.Equal(reason, refusal.ErrorID);
            Assert.Equal(deficit, refusal.InnerstrengthDeficit);
            Assert.Empty(c.messageQueue.ObjectMessagesQueue);
            Assert.Equal(position, c.playerInstance.Position.getValue());
            Assert.Equal((ushort)inner, AbilityHandler.CurrentInnerStrength(c));
        }
        finally
        {
            CombatManager.GetHandler(c).isCombatRunning = false;
            Store.currentClient = previous;
        }
    }

    private static WorldClient JumpClient(int inner)
    {
        var c=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());
        c.playerData=new();c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);
        c.playerInstance=new();c.messageQueue=new();c.viewMan=new();c.playerInstance.InnerStrengthAvailable.setValue((ushort)inner);
        c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17100, 495, 6400));
        c.playerInstance.RealLastName.setValue("Tester");
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!.SetValue(c, long.MaxValue);
        return c;
    }
}
