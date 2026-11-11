using System;
using hds;
using hds.shared;

namespace hds.Tests;

// Expected bytes are retail's: the two-person capture (community-intake/20260607T022135Z-hdneo-public-two-person-actions-*,
// a /manhug), d_PacketsChillinginMaraNoticingSpawns (another player's accepted hug), WhiteHallwaysWarboyTeleportDoors
// (another player sitting) and mxopackets1_entilsar_enti_lastday2 (one's own sit).
public class AnimationPacketTests
{
    private static string H(byte[] bytes) => Convert.ToHexString(bytes);
    private static string H(string hex) => hex.Replace(" ", "").ToUpperInvariant();
    private static byte[] Hex(string hex) => StringUtils.hexStringToBytes(hex.Replace(" ", ""));

    private static AnimationPackets.Animation At(string position, byte counter, byte kind, byte stance, ushort? action)
    {
        byte[] p = Hex(position);
        return new AnimationPackets.Animation
        {
            Counter = counter, Kind = kind, Stance = stance, Action = action,
            X = BitConverter.ToSingle(p, 0), Y = BitConverter.ToSingle(p, 4), Z = BitConverter.ToSingle(p, 8)
        };
    }

    [Fact]
    public void PairedRequestMatchesBothSidesOfRetailManhug()
    {
        const string pos = "ba 36 83 46 00 80 f7 43 0e 9e e6 44";
        AnimationPackets.Animation request = At(pos, 0x08, AnimationPackets.KindRequest, 0, 0xd5);
        // The requester's own view: the target as the requester knows it (view 0x17, spawn 0x96), then the sim time
        Assert.Equal(H("02 00 01 28 08 01 00 25 d5 00 00 10 17 00 96 00" + pos + "44 33 22 11"),
            H(AnimationPackets.Own(request, 0x00960017, 0x11223344)));
        // The target's view of the requester: the target itself is view 2; a 00 action byte, no time
        Assert.Equal(H("01 2a 08 01 00 25 d5 00 00 10 02 00 96 00 00" + pos),
            H(AnimationPackets.Other(request, 0x00960002)));
    }

    [Fact]
    public void AcceptedPairedEmoteMatchesRetail()
    {
        // d_PacketsChillinginMara... L11673: 3d 00 01 2a 0f 02 00 25 ca 00 00 10 33 00 81 00 <0,0,140,pi> 00 <pos>
        const string pos = "ba 36 83 46 00 80 f7 43 0e 9e e6 44";
        AnimationPackets.Animation accepted = At(pos, 0x0f, AnimationPackets.KindAccepted, 0, 0xca);
        Assert.Equal(H("01 2a 0f 02 00 25 ca 00 00 10 33 00 81 00 00 00 00 00 00 00 00 00 00 00 0c 43 db 0f 49 40 00" + pos),
            H(AnimationPackets.Other(accepted, 0x00810033)));
    }

    [Fact]
    public void PlainEmoteIsTheSameBlockAsTheOwnViewBuilder()
    {
        const string pos = "34 49 84 c7 00 00 be 42 27 a4 7f 46";
        AnimationPackets.Animation clap = At(pos, 0x06, AnimationPackets.KindPlain, 0, 0x04);
        byte[] p = Hex(pos);
        Assert.Equal(H(ServerPackets.BuildEmoteUpdate(0x06, 0x04, BitConverter.ToSingle(p, 0), BitConverter.ToSingle(p, 4),
                BitConverter.ToSingle(p, 8), 0x185ea6b3)),
            H(AnimationPackets.Own(clap, 0, 0x185ea6b3)));
        // d_PacketsChillinginMara... L17238, another player's emote: 01 2a 01 40 00 25 7f 00 00 10 00 <pos>
        Assert.Equal(H("01 2a 01 40 00 25 7f 00 00 10 00" + pos),
            H(AnimationPackets.Other(At(pos, 0x01, AnimationPackets.KindPlain, 0, 0x7f), 0)));
    }

    [Fact]
    public void SittingMatchesRetailOwnAndOtherViews()
    {
        // WhiteHallways... L4303: 04 00 03 2a 0b 40 0a 00 00 10 00 <pos> 80 80 80 80 04 00 00 20 00
        const string pos = "c1 71 35 48 00 40 62 c4 d9 5d 43 47";
        AnimationPackets.Animation sit = At(pos, 0x0b, AnimationPackets.KindPlain, AnimationPackets.StanceSitting, null);
        Assert.Equal(H("03 2a 0b 40 0a 00 00 10 00" + pos + "80 80 80 80 04 00 00 20 00"),
            H(AnimationPackets.Other(sit, 0, AnimationPackets.SittingFlag)));

        // entilsar L38899: 02 00 03 28 12 40 0a 00 00 10 <pos> <time> 80 80 80 80 80 80 01 00 00 28 01
        const string own = "60 91 7a 47 00 00 aa 42 b4 86 b3 c7";
        AnimationPackets.Animation mine = At(own, 0x12, AnimationPackets.KindPlain, AnimationPackets.StanceSitting, null);
        Assert.Equal(H("02 00 03 28 12 40 0a 00 00 10" + own + "5d a5 fd 06 80 80 80 80 80 80 01 00 00 28 01"),
            H(AnimationPackets.Own(mine, 0, 0x06fda55d, 0x01280000)));

        // A dance has no attributes: ProxyM2_m2_packets L90016, 02 00 01 28 08 40 13 00 00 10 <pos> <time>
        Assert.Equal(H("02 00 01 28 08 40 13 00 00 10 da 6b 1d 47 00 00 be 42 6f f8 a5 c6 2f 7c 8d 07"),
            H(AnimationPackets.Own(At("da 6b 1d 47 00 00 be 42 6f f8 a5 c6", 0x08, AnimationPackets.KindPlain, 0x13, null), 0, 0x078d7c2f)));
    }

    [Fact]
    public void EmoteRequestAndAnswerMatchRetail()
    {
        Assert.Equal(H("31 f5 0d 00 58 0f 00 51 00"), H(AnimationPackets.EmoteRequest(0x58000df5, 0x0051000f)));
        Assert.Equal(H("32 01 f5 0d 00 58 17 00 96 00 00 00 00 00 01"),
            H(AnimationPackets.EmoteAnswer(1, 0x58000df5, 0x00960017, 0, true)));
        Assert.Equal((213, 214, 215), EmoteHandler.Paired[0x58000df5]);
    }
}
