using System;
using System.Collections.Generic;
using System.Text;
using hds;
using hds.world.Structures;

namespace hds.Tests;

public class PacketSerializationAndSubwayTests
{
    [Fact]
    public void DynamicArrayAppendInsertBeforeAndGrowthMatchExactBytes()
    {
        var arr = new DynamicArray();
        Assert.Equal(0, arr.getSize());
        Assert.Empty(arr.getBytes());

        arr.append(0x03);
        arr.append(new byte[] { 0x04, 0x05 });
        arr.insertBefore(0x02);
        arr.insertBefore(new byte[] { 0x00, 0x01 });

        Assert.Equal(6, arr.getSize());
        Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 }, arr.getBytes());

        // Force multiple resizes via both append and insertBefore.
        byte[] tail = new byte[100];
        for (int i = 0; i < tail.Length; i++) tail[i] = (byte)(i + 10);
        arr.append(tail);

        byte[] head = new byte[40];
        for (int i = 0; i < head.Length; i++) head[i] = (byte)(200 + i);
        arr.insertBefore(head);

        byte[] finalBytes = arr.getBytes();
        Assert.Equal(146, arr.getSize());
        Assert.Equal(146, finalBytes.Length);
        Assert.Equal(head, finalBytes[..40]);
        Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 }, finalBytes[40..46]);
        Assert.Equal(tail, finalBytes[46..]);
    }

    [Fact]
    public void PacketContentAndPacketReaderRoundTripAllPrimitivesAndStrings()
    {
        var pak = new PacketContent();
        pak.AddByte(0xAB);
        pak.AddUShort(0x1234); // low byte only (0x34)
        pak.AddInt16(-12345, 1);
        pak.AddInt16(-12345, 0);
        pak.AddUint16(0xBEEF, 1);
        pak.AddUint16(0xBEEF, 0);
        pak.AddInt32(-0x1234567, 1);
        pak.AddInt32(-0x1234567, 0);
        pak.AddUint32(0x12345678u, 1);
        pak.AddUint32(0x12345678u, 0);
        pak.AddFloat(123.5f, 1);
        pak.AddFloat(-45.25f, 0);
        pak.AddDouble(17020.25, 1);
        pak.AddDouble(-2693.75, 0);
        pak.AddFloatLtVector3f(1.5f, -2.5f, 3.25f);
        pak.AddDoubleLtVector3d(10.5, 20.25, -30.75);
        pak.AddSizedString("Neo");
        pak.AddSizedTerminatedString("Trinity");
        pak.AddStringWithFixedSized("ab", 5); // shorter -> 5 + 1 = 6 bytes
        pak.AddStringWithFixedSized("exact", 5); // equal -> 5 bytes

        byte[] wire = pak.ReturnFinalPacket();
        var reader = new PacketReader(wire);

        Assert.Equal(0xABu, reader.ReadUint8());
        Assert.Equal(0x34u, reader.ReadUint8());
        Assert.Equal(unchecked((ushort)(short)-12345), reader.ReadUInt16(1));
        Assert.Equal(unchecked((ushort)(short)-12345), reader.ReadUInt16(0));
        Assert.Equal((ushort)0xBEEF, reader.ReadUInt16(1));
        Assert.Equal((ushort)0xBEEF, reader.ReadUInt16(0));
        Assert.Equal(unchecked((uint)-0x1234567), reader.ReadUInt32(1));
        Assert.Equal(unchecked((uint)-0x1234567), reader.ReadUInt32(0));
        Assert.Equal(0x12345678u, reader.ReadUInt32(1));
        Assert.Equal(0x12345678u, reader.ReadUInt32(0));
        Assert.Equal(123.5f, reader.ReadFloat(1));
        Assert.Equal(-45.25f, reader.ReadFloat(0));
        Assert.Equal(17020.25, reader.ReadDouble(1));
        Assert.Equal(-2693.75, reader.ReadDouble(0));

        float fx = 0, fy = 0, fz = 0;
        NumericalUtils.LtVector3fToFloats(reader.ReadBytes(12), ref fx, ref fy, ref fz);
        Assert.Equal(1.5f, fx);
        Assert.Equal(-2.5f, fy);
        Assert.Equal(3.25f, fz);

        double dx = 0, dy = 0, dz = 0;
        NumericalUtils.LtVector3dToDoubles(reader.ReadBytes(24), ref dx, ref dy, ref dz);
        Assert.Equal(10.5, dx);
        Assert.Equal(20.25, dy);
        Assert.Equal(-30.75, dz);

        Assert.Equal("Neo", reader.ReadSizedString());
        Assert.Equal("Trinity", reader.ReadSizedZeroTerminatedString());
        Assert.Equal(new byte[] { (byte)'a', (byte)'b', 0, 0, 0, 0 }, reader.ReadBytes(6));
        Assert.Equal(Encoding.ASCII.GetBytes("exact"), reader.ReadBytes(5));
        Assert.Equal(wire.Length, reader.GetOffset());
    }

    [Fact]
    public void PacketReaderZeroLengthTerminatedStringReturnsEmptyWithoutOverflow()
    {
        var reader = new PacketReader(new byte[] { 0x00, 0x00, 0xFF });
        Assert.Equal(string.Empty, reader.ReadSizedZeroTerminatedString());
        Assert.Equal(2, reader.GetOffset());
    }

    [Fact]
    public void NumericalUtilsEndianConversionsMatchExactByteLayouts()
    {
        Assert.Equal(new byte[] { 0x34, 0x12 }, NumericalUtils.uint16ToByteArray(0x1234, 1));
        Assert.Equal(new byte[] { 0x12, 0x34 }, NumericalUtils.uint16ToByteArray(0x1234, 0));
        Assert.Equal(new byte[] { 0x34 }, NumericalUtils.uint16ToByteArrayShort(0x1234));
        Assert.Equal((ushort)0x1234, NumericalUtils.ByteArrayToUint16(new byte[] { 0x34, 0x12 }, 1));
        Assert.Equal((ushort)0x1234, NumericalUtils.ByteArrayToUint16(new byte[] { 0x12, 0x34 }, 0));

        Assert.Equal(new byte[] { 0x78, 0x56, 0x34, 0x12 }, NumericalUtils.uint32ToByteArray(0x12345678u, 1));
        Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x78 }, NumericalUtils.uint32ToByteArray(0x12345678u, 0));
        Assert.Equal(0x12345678u, NumericalUtils.ByteArrayToUint32(new byte[] { 0x78, 0x56, 0x34, 0x12 }, 1));
        Assert.Equal(0x12345678u, NumericalUtils.ByteArrayToUint32(new byte[] { 0x12, 0x34, 0x56, 0x78 }, 0));

        byte[] fLe = NumericalUtils.floatToByteArray(3.5f, 1);
        byte[] fBe = NumericalUtils.floatToByteArray(3.5f, 0);
        Assert.Equal(3.5f, NumericalUtils.byteArrayToFloat(fLe, 1));
        Assert.Equal(3.5f, NumericalUtils.byteArrayToFloat(fBe, 0));
        Assert.Equal(fLe[0], fBe[3]);
        Assert.Equal(fLe[3], fBe[0]);

        byte[] dLe = NumericalUtils.doubleToByteArray(-9876.125, 1);
        byte[] dBe = NumericalUtils.doubleToByteArray(-9876.125, 0);
        Assert.Equal(-9876.125, NumericalUtils.byteArrayToDouble(dLe, 1));
        Assert.Equal(-9876.125, NumericalUtils.byteArrayToDouble(dBe, 0));
        Assert.Equal(dLe[0], dBe[7]);
        Assert.Equal(dLe[7], dBe[0]);
    }

    [Fact]
    public void ArrayUtilsEqualCopyAndFastCopyClampSafely()
    {
        Assert.True(ArrayUtils.equal(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }));
        Assert.False(ArrayUtils.equal(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 }));
        Assert.False(ArrayUtils.equal(new byte[] { 1, 2 }, new byte[] { 1, 2, 3 }));

        byte[] src = { 10, 20, 30, 40 };
        byte[] dst = { 99, 99, 99, 99, 99 };
        ArrayUtils.copy(src, 2, dst, 1, 10); // clamps to 2 bytes available in src
        Assert.Equal(new byte[] { 99, 30, 40, 99, 99 }, dst);

        byte[] fastDst = new byte[4];
        ArrayUtils.fastCopy(src, fastDst, 4);
        Assert.Equal(src, fastDst);
    }

    [Fact]
    public void AttributeTypedSettersWriteLittleEndianZeroPaddedAndMatchVector3d()
    {
        var a4 = new Attribute(4, "Test4");
        a4.setValue(0x11223344u);
        Assert.True(a4.isActive());
        Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, a4.getValue());

        // Shorter value must zero-pad trailing bytes just like setValue(byte[]).
        a4.setValue((ushort)0x0506);
        Assert.Equal(new byte[] { 0x06, 0x05, 0x00, 0x00 }, a4.getValue());

        a4.setValue(true);
        Assert.Equal(new byte[] { 0x01, 0x00, 0x00, 0x00 }, a4.getValue());

        a4.setValue("AB");
        Assert.Equal(new byte[] { (byte)'A', (byte)'B', 0x00, 0x00 }, a4.getValue());

        a4.setValue("ABCDEF"); // truncated to size 4
        Assert.Equal(new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D' }, a4.getValue());

        var pos = new Attribute(24, "Position");
        pos.SetVector3d(123.5, 495.25, -88.75);
        Assert.True(pos.isActive());
        Assert.True(pos.EqualsVector3d(123.5, 495.25, -88.75));
        Assert.False(pos.EqualsVector3d(123.5, 495.25, -88.5));
        Assert.Equal(NumericalUtils.doublesToLtVector3d(123.5, 495.25, -88.75), pos.getValue());
    }

    [Fact]
    public void GameObjectCreationUpdateAndSelfUpdateEncodingMatchSparseGroupFormat()
    {
        // 16 attributes -> 3 groups: [0..6], [7..13], [14..15]
        var go = new GameObject(16, 16, "SparseTest", 999, 0xFFFFFFFF);
        var attrs = new Attribute[16];
        for (int i = 0; i < 16; i++)
        {
            attrs[i] = new Attribute(2, "A" + i);
            go.AddAttribute(ref attrs[i], i, i);
        }

        // All disabled -> single 0x00 count byte.
        Assert.Equal(new byte[] { 0x00 }, go.GetCreationAttributes());
        Assert.Equal(new byte[] { 0x00 }, go.GetUpdateAttributes(new List<Attribute> { attrs[0] }));

        // Enable index 1 (group 0, bit 1) and index 14 (group 2, bit 0), leaving group 1 completely empty.
        attrs[1].setValue((ushort)0x0102);
        attrs[14].setValue((ushort)0x0304);

        byte[] expected =
        {
            0x02,       // 2 attributes total
            0x82,       // group 0: continuation (0x80) | bit 1 (0x02)
            0x02, 0x01, // attrs[1]
            0x80,       // group 1: continuation (0x80), no bits set
            0x01,       // group 2: terminal (no 0x80), bit 0 (0x01)
            0x04, 0x03  // attrs[14]
        };
        Assert.Equal(expected, go.GetCreationAttributes());
        Assert.Equal(expected, go.GetUpdateAttributes(new List<Attribute> { attrs[14], attrs[1] }));

        // Selected subset without count prefix (mirrors PlayerCharacter.GetSelfUpdateAttributes(..., false)).
        Assert.Empty(GameObject.EncodeSelectedAttributes(attrs, 3, new List<Attribute>(), false));
        Assert.Equal(expected[1..], GameObject.EncodeSelectedAttributes(attrs, 3, new List<Attribute> { attrs[1], attrs[14] }, false));
    }

    [Fact]
    public void SubwayCountdownStartStopAndNullWorldUpdateAreSafe()
    {
        var car = new Subway(new StaticWorldObject
        {
            mxoStaticId = 42,
            metrId = 1,
            quat = "0000000000000000000000000000803f"
        });

        // Must not throw NullReferenceException when world is null.
        car.UpdateSubwayState(null);
        car.StartCountdown();
        car.StartCountdown(); // idempotent restart disposes previous timer
        car.DecreaseSeconds(car, null);
        car.StopCountdown();
        car.StopCountdown(); // idempotent stop
    }
}
